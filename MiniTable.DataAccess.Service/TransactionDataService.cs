using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.DataAccess.Service
{
    [Access(Name = "transaction-data", IsPublic = false)]
    public class TransactionDataService
    {
        string m_CommonMerchantDb = "MerchantDB";

        async Task<string> CallMerchantApi(RequestContext ctx, string serviceName, string actionName, string jsonParam)
        {
            //System.Diagnostics.Debugger.Break();
            var svcs = ctx.LocalServices;
            if (svcs.InternalServices.ContainsKey(serviceName))
            {
                var svc = svcs.InternalServices[serviceName];
                object ret = await svc.LocalCall(actionName, jsonParam);
                if (ret != null) return ret.ToString();
            }
            else
            {
                ctx.Logger.Error("Merchant API service not found: " + serviceName + "|" + actionName);
            }
            return null;
        }

        [Access(Name = "cancel-bet-debit")]
        public async Task CancelBetDebit(RequestContext ctx)
        {
            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string betUuid = req.bet_uuid;
            string merchantCode = req.merchant_code.ToString(); // maybe need to get db name with merchant code

            var totalCount = 0;
            decimal totalAmount = 0;

            Dictionary<string, List<dynamic>> debitItems = new Dictionary<string, List<dynamic>>();
            Dictionary<string, dynamic> merchants = new Dictionary<string, dynamic>();
            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_CommonMerchantDb))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@bet_uuid", betUuid);

                    // select records which need to cancel
                    cmd.CommandText = " select debit_uuid, bet_uuid, player_id, client_id, session_id, "
                                    + " merchant_code, currency_code, debit_amount from tbl_trans_debit "
                                    + " where is_cancelled = 0 and bet_uuid = @bet_uuid ";

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string debitUuid = reader["debit_uuid"].ToString();
                            //string betUuid = reader["bet_uuid"].ToString();
                            string playerId = reader["player_id"].ToString();
                            string clientId = reader["client_id"].ToString();
                            string sessionId = reader["session_id"].ToString();
                            string currentMerchant = reader["merchant_code"].ToString();
                            string currencyCode = reader["currency_code"].ToString();
                            decimal debitAmount = Convert.ToDecimal(reader["debit_amount"].ToString());

                            var reqIp = clientId;
                            if (reqIp.Contains(":")) reqIp = reqIp.Split(':')[0];

                            var item = new
                            {
                                debit_uuid = debitUuid,
                                trans_uuid = debitUuid + "-cancel",
                                bet_uuid = betUuid,
                                merchant_code = currentMerchant,
                                currency_code = currencyCode,
                                player_id = playerId,
                                player_ip = reqIp,
                                session_id = sessionId,
                                amount = debitAmount
                            };

                            string merchantKey = item.merchant_code + item.currency_code;

                            if (debitItems.ContainsKey(merchantKey))
                            {
                                var list = debitItems[merchantKey];
                                list.Add(item);
                            }
                            else
                            {
                                var list = new List<dynamic>();
                                list.Add(item);
                                debitItems.Add(merchantKey, list);
                            }

                        }
                    }
                }

                foreach (var item in debitItems)
                {
                    if (!merchants.ContainsKey(item.Key))
                    {
                        string merchantInfo = await RemoteCaller.RandomCall(ctx.RemoteServices,
                                        "merchant-data", "get-merchant-info", item.Key);

                        if (String.IsNullOrEmpty(merchantInfo) || !merchantInfo.Contains('{') || !merchantInfo.Contains(':'))
                        {
                            continue;
                        }

                        dynamic merchant = ctx.JsonHelper.ToJsonObject(merchantInfo);
                        if (merchant != null) merchants.Add(item.Key, merchant);
                    }

                    if (!merchants.ContainsKey(item.Key)) continue;

                    string apiUrl = merchants[item.Key].url.ToString();
                    string apiSvc = merchants[item.Key].service.ToString();

                    var list = item.Value;

                    foreach (var debit in list)
                    {
                        var apiReq = new
                        {
                            merchant_url = apiUrl,
                            debit.trans_uuid,
                            debit.debit_uuid,
                            debit.bet_uuid,
                            debit.merchant_code,
                            debit.currency_code,
                            debit.player_id,
                            debit.player_ip,
                            debit.session_id,
                            debit.amount

                        };

                        dynamic ret = null;
                        try
                        {
                            string retJson = await CallMerchantApi(ctx, apiSvc, "cancel-debit", ctx.JsonHelper.ToJsonString(apiReq));
                            ret = string.IsNullOrEmpty(retJson) ? null : ctx.JsonHelper.ToJsonObject(retJson);
                        }
                        catch (Exception ex)
                        {
                            ret = null;
                            ctx.Logger.Error("Failed to call cancel debit: " + ex.Message);
                        }

                        try
                        {
                            if (ret != null)
                            {
                                int respCode = ret.error_code;

                                var sql = " update tbl_trans_debit "
                                    + " set network_error = 0 , response_error = " + respCode;
                                if (respCode == 0) sql += " , is_cancelled = 1 ";
                                sql += " , update_time = NOW() ";
                                sql += " where debit_uuid = @debit_uuid ";

                                var okay = false;
                                var trans = cnn.BeginTransaction();

                                using (var cmd = cnn.CreateCommand())
                                {
                                    cmd.Transaction = trans;

                                    dbhelper.AddParam(cmd, "@debit_uuid", debit.debit_uuid);
                                    cmd.CommandText = sql;

                                    okay = cmd.ExecuteNonQuery() > 0;
                                }

                                if (okay)
                                {
                                    sql = " update tbl_bet_record set cancel_state = 1 ";
                                    sql += " , update_time = CURRENT_TIMESTAMP ";
                                    if (respCode == 0) sql += " , cancel_time = CURRENT_TIMESTAMP ";
                                    sql += " where bet_uuid = @bet_uuid ";

                                    using (var cmd = cnn.CreateCommand())
                                    {
                                        cmd.Transaction = trans;

                                        dbhelper.AddParam(cmd, "@bet_uuid", debit.bet_uuid);

                                        cmd.CommandText = sql;

                                        okay = okay && cmd.ExecuteNonQuery() > 0;
                                    }
                                }

                                if (okay) trans.Commit();
                                else trans.Rollback();

                                if (okay) totalAmount += debit.amount;

                            }

                            totalCount++;
                        }
                        catch (Exception ex)
                        {
                            ret = null;
                            ctx.Logger.Error("Failed to call cancel debit: " + ex.Message);
                        }


                    } // end of debits of same merchant

                } // end of all debits

            } // end of using db cnn

            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                error_code = 0,
                total_amount = totalAmount,
                error_message = totalCount.ToString()
            }));
        }

        [Access(Name = "request-to-cancel-debit")]
        public async Task RequestToCancelDebit(RequestContext ctx)
        {
            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            int updateRows = 0;
            string betUuid = req.bet_uuid;
            string merchantCode = req.merchant_code.ToString(); // maybe need to get db name with merchant code

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_CommonMerchantDb)) // should do it in specify merchant db...
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@bet_uuid", betUuid);

                    cmd.CommandText = " update tbl_trans_debit "
                                    + " set is_success = 0 , is_cancelled = 0 , network_error = 1 "
                                    + " where is_cancelled = 0 and bet_uuid = @bet_uuid "
                                    ;

                    updateRows = cmd.ExecuteNonQuery();
                }
            }

            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                error_code = 0,
                update_rows = updateRows,
                error_message = updateRows.ToString()
            }));
        }

        [Access(Name = "get-bet-credit")]
        public async Task GetBetCredit(RequestContext ctx)
        {
            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string betUuid = req.bet_uuid;
            string merchantCode = req.merchant_code.ToString(); // maybe need to get db name with merchant code

            string creditUuid = "";
            decimal payoutAmount = 0;

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_CommonMerchantDb)) // should do it in specify merchant db...
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@bet_uuid", betUuid);

                    cmd.CommandText = " select credit_uuid , credit_amount from tbl_trans_credit "
                                    + " where bet_uuid = @bet_uuid "
                                    ;

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            creditUuid = reader["credit_uuid"].ToString();
                            payoutAmount = Convert.ToDecimal(reader["credit_amount"].ToString());
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(creditUuid))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -2,
                    error_message = "Credit not found - Bet UUID: " + betUuid
                }));
            }
            else
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = 0,
                    credit = new
                    {
                        credit_uuid = creditUuid,
                        credit_amount = payoutAmount
                    }
                }));
            }
        }


        [Access(Name = "create-debit")]
        public async Task CreateDebit(RequestContext ctx)
        {
            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            bool okay = false;
            string debitId = req.bet_uuid + "-debit";
            string gameCode = "Mini-" + req.table_code;
            string roundId = req.table_code + "-" + req.shoe_code + "-" + req.round_number;

            string providerCode = "mini";

            string merchantCode = req.merchant_code.ToString();

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_CommonMerchantDb))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@debit_uuid", debitId);
                    dbhelper.AddParam(cmd, "@bet_uuid", req.bet_uuid);

                    dbhelper.AddParam(cmd, "@game_code", gameCode);
                    dbhelper.AddParam(cmd, "@round_id", roundId);
                    dbhelper.AddParam(cmd, "@bet_pool", req.bet_pool);

                    dbhelper.AddParam(cmd, "@provider_code", providerCode);

                    dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                    dbhelper.AddParam(cmd, "@player_id", req.player_id);

                    dbhelper.AddParam(cmd, "@client_id", req.client_id);
                    dbhelper.AddParam(cmd, "@session_id", req.session_id);

                    dbhelper.AddParam(cmd, "@debit_amount", req.bet_amount);

                    dbhelper.AddParam(cmd, "@request_url", "");

                    cmd.CommandText = " insert into tbl_trans_debit "
                                    + " ( debit_uuid, bet_uuid, game_code, round_id, bet_pool, provider_code, merchant_code, player_id, debit_amount, client_id, session_id ) values "
                                    + " ( @debit_uuid, @bet_uuid, @game_code, @round_id , @bet_pool , @provider_code , @merchant_code , @player_id , @debit_amount , @client_id , @session_id ) "
                                    ;

                    okay = cmd.ExecuteNonQuery() > 0;
                }
            }

            if (okay)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = 0,
                    debit_uuid = debitId,
                    round_id = roundId
                    //request_url = requestUrl
                }));
            }
            else await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                error_code = -1,
                error_message = "Failed to update database"
            }));
        }

        [Access(Name = "update-debit")]
        public async Task UpdateDebit(RequestContext ctx)
        {
            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            bool okay = false;
            string debitId = req.debit_uuid;
            string betId = req.bet_uuid;
            string merchantCode = req.merchant_code.ToString();

            int reqTimes = req.request_times;

            int isSuccess = req.is_success;
            int networkErr = req.network_error;
            int replyErr = req.response_error;

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_CommonMerchantDb))
            {
                var trans = cnn.BeginTransaction();

                using (var cmd = cnn.CreateCommand())
                {
                    cmd.Transaction = trans;

                    dbhelper.AddParam(cmd, "@debit_uuid", debitId);

                    dbhelper.AddParam(cmd, "@request_times", reqTimes);

                    dbhelper.AddParam(cmd, "@is_success", isSuccess);
                    dbhelper.AddParam(cmd, "@network_error", networkErr);
                    dbhelper.AddParam(cmd, "@response_error", replyErr);

                    cmd.CommandText = " update tbl_trans_debit "
                                    + " set request_times = @request_times, "
                                    + " is_success = @is_success, "
                                    + " network_error = @network_error, "
                                    + " response_error = @response_error, "
                                    + " update_time = NOW() "
                                    + " where debit_uuid = @debit_uuid "
                                    ;

                    okay = cmd.ExecuteNonQuery() > 0;
                }

                if (okay && networkErr == 0)
                {
                    using (var cmd = cnn.CreateCommand())
                    {
                        cmd.Transaction = trans;

                        dbhelper.AddParam(cmd, "@bet_uuid", betId);

                        cmd.CommandText = " update tbl_bet_record "
                                        + " set debit_state = 1 "
                                        + " , update_time = NOW() "
                                        + " where bet_uuid = @bet_uuid "
                                        ;

                        okay = okay && cmd.ExecuteNonQuery() > 0;
                    }
                }

                if (okay) trans.Commit();
                else trans.Rollback();
            }

            if (okay)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = 0
                }));
            }
            else await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                error_code = -1,
                error_message = "Failed to update database"
            }));
        }

        [Access(Name = "create-credit")]
        public async Task CreateCredit(RequestContext ctx)
        {
            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            bool okay = false;
            string creditId = req.bet_uuid + "-credit";
            string gameCode = "Mini-" + req.table_code;
            string roundId = req.table_code + "-" + req.shoe_code + "-" + req.round_number;

            string providerCode = "mini";

            string merchantCode = req.merchant_code.ToString();

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_CommonMerchantDb))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@credit_uuid", creditId);
                    dbhelper.AddParam(cmd, "@bet_uuid", req.bet_uuid);

                    dbhelper.AddParam(cmd, "@game_code", gameCode);
                    dbhelper.AddParam(cmd, "@round_id", roundId);
                    dbhelper.AddParam(cmd, "@bet_pool", req.bet_pool);

                    dbhelper.AddParam(cmd, "@provider_code", providerCode);

                    dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                    dbhelper.AddParam(cmd, "@player_id", req.player_id);

                    dbhelper.AddParam(cmd, "@client_id", req.client_id);
                    dbhelper.AddParam(cmd, "@session_id", req.session_id);

                    dbhelper.AddParam(cmd, "@credit_amount", req.pay_amount);

                    dbhelper.AddParam(cmd, "@request_url", "");

                    cmd.CommandText = " insert into tbl_trans_credit "
                                    + " ( credit_uuid, bet_uuid, game_code, round_id, bet_pool, provider_code, merchant_code, player_id, credit_amount, client_id, session_id ) values "
                                    + " ( @credit_uuid, @bet_uuid, @game_code, @round_id , @bet_pool , @provider_code , @merchant_code , @player_id , @credit_amount , @client_id , @session_id ) "
                                    ;

                    okay = cmd.ExecuteNonQuery() > 0;
                }
            }

            if (okay)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = 0,
                    credit_uuid = creditId,
                    round_id = roundId
                    //request_url = requestUrl
                }));
            }
            else await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                error_code = -1,
                error_message = "Failed to update database"
            }));
        }

        [Access(Name = "update-credit")]
        public async Task UpdateCredit(RequestContext ctx)
        {
            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            bool okay = false;
            string creditId = req.credit_uuid;
            string merchantCode = req.merchant_code.ToString();

            string betId = req.bet_uuid;

            int reqTimes = req.request_times;

            int isSuccess = req.is_success;
            int networkErr = req.network_error;
            int replyErr = req.response_error;

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_CommonMerchantDb))
            {
                var trans = cnn.BeginTransaction();

                using (var cmd = cnn.CreateCommand())
                {
                    cmd.Transaction = trans;

                    dbhelper.AddParam(cmd, "@credit_uuid", creditId);

                    dbhelper.AddParam(cmd, "@request_times", reqTimes);

                    dbhelper.AddParam(cmd, "@is_success", isSuccess);
                    dbhelper.AddParam(cmd, "@network_error", networkErr);
                    dbhelper.AddParam(cmd, "@response_error", replyErr);


                    cmd.CommandText = " update tbl_trans_credit "
                                    + " set request_times = @request_times, "
                                    + " is_success = @is_success, "
                                    + " network_error = @network_error, "
                                    + " response_error = @response_error, "
                                    + " update_time = NOW() "
                                    + " where credit_uuid = @credit_uuid "
                                    ;

                    okay = cmd.ExecuteNonQuery() > 0;
                }

                if (okay && networkErr == 0)
                {
                    using (var cmd = cnn.CreateCommand())
                    {
                        cmd.Transaction = trans;

                        dbhelper.AddParam(cmd, "@bet_uuid", betId);

                        cmd.CommandText = " update tbl_bet_record "
                                        + " set credit_state = 1 "
                                        + " , update_time = NOW() "
                                        + " where bet_uuid = @bet_uuid "
                                        ;

                        okay = okay && cmd.ExecuteNonQuery() > 0;
                    }
                }

                if (okay) trans.Commit();
                else trans.Rollback();
            }

            if (okay)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = 0
                }));
            }
            else await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                error_code = -1,
                error_message = "Failed to update database"
            }));
        }

    }
}
