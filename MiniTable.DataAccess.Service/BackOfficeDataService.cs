using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.DataAccess.Service
{
    [Access(Name = "bo-data", IsPublic = false)]
    public class BackOfficeDataService
    {
        private string m_MainDatabase = "MainDB";
        private string m_CommonMerchantDb = "MerchantDB";

        [Access(Name = "on-load", IsLocal = true)]
        public async Task<string> Load(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            await Task.Delay(50);
            node.GetLogger().Info(this.GetType().Name + " service started");
            await Task.Delay(50);

            return "";
        }

        [Access(Name = "check-account")]
        public async Task CheckAccount(RequestContext ctx)
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

            bool okay = false;
            //string sessionId = "";

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_MainDatabase))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@account_id", req.account);
                    dbhelper.AddParam(cmd, "@merchant_code", req.merchant);
                    dbhelper.AddParam(cmd, "@account_pwd", req.password);

                    cmd.CommandText = " select * from tbl_bo_account "
                                            + " where account_id = @account_id and merchant_code = @merchant_code "
                                            + " and account_pwd = @account_pwd and is_active > 0 "
                                            ;

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read()) okay = true;
                    }
                }
            }

            if (okay)
            {
                //sessionId = Guid.NewGuid().ToString();
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = 0,
                    //session_id = sessionId,
                    error_message = "ok"
                }));
            }
            else
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = 2,
                    error_message = "Invalid account or password"
                }));
            }
        }

        [Access(Name = "change-user-password")]
        public async Task ChangeUserPassword(RequestContext ctx)
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

            bool okay = false;
            //string sessionId = "";

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_MainDatabase))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@account_id", req.accountId);
                    dbhelper.AddParam(cmd, "@merchant_code", req.merchantCode);
                    dbhelper.AddParam(cmd, "@account_pwd", req.oldPassword);
                    dbhelper.AddParam(cmd, "@new_pwd", req.newPassword);

                    cmd.CommandText = " update tbl_bo_account set account_pwd = @new_pwd "
                                            + " where account_id = @account_id and merchant_code = @merchant_code "
                                            + " and account_pwd = @account_pwd and is_active > 0 "
                                            ;

                    okay = cmd.ExecuteNonQuery() > 0;
                }
            }

            if (okay)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = 0,
                    error_message = "ok"
                }));
            }
            else
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -2,
                    error_message = "Invalid account or password"
                }));
            }
        }

        [Access(Name = "get-game-results")]
        public async Task GetGameResults(RequestContext ctx)
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

            bool okay = false;

            dynamic reply = new ExpandoObject();
            reply.rows = new List<ExpandoObject>();

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string pageSizeStr = req.pageSize;
            string pageNumberStr = req.pageNumber;

            int pageSize = Convert.ToInt32(pageSizeStr);
            int pageNumber = Convert.ToInt32(pageNumberStr);

            if (pageSize <= 0) pageSize = 1;
            if (pageNumber <= 0) pageNumber = 1;

            string sqlwhere = " where round_state >= 7 "
                            + " and round_start_time >= @first_game_time and round_start_time <= @last_game_time "
                            ;

            string sqlorder = " order by round_start_time desc ";

            int total = 0;
            int pageCount = 0;

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_MainDatabase))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@first_game_time", req.fromGameTime);
                    dbhelper.AddParam(cmd, "@last_game_time", req.toGameTime);

                    cmd.CommandText = " select count(game_id) from tbl_game_record " + sqlwhere;
                    total = Convert.ToInt32(cmd.ExecuteScalar());
                }

                reply.total = total >= 0 ? total : 0;

                if (total > 0)
                {
                    int rest = total % pageSize;
                    pageCount = (total - rest) / pageSize;
                    if (rest > 0) pageCount += 1;

                    int offset = pageSize * (pageNumber - 1);
                    if (offset >= total)
                    {
                        pageNumber = pageCount;
                        offset = pageSize * (pageNumber - 1);
                    }

                    if (offset + pageSize > total) pageSize = total - offset;

                    using (var cmd = cnn.CreateCommand())
                    {
                        dbhelper.AddParam(cmd, "@first_game_time", req.fromGameTime);
                        dbhelper.AddParam(cmd, "@last_game_time", req.toGameTime);

                        cmd.CommandText = " select * from tbl_game_record ";
                        cmd.CommandText += sqlwhere + sqlorder;
                        cmd.CommandText += " limit " + offset + "," + pageSize;

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                dynamic row = new ExpandoObject();

                                row.game_id = reader["table_code"].ToString()
                                            + "-" + reader["shoe_code"].ToString()
                                            + "-" + reader["round_number"].ToString();

                                row.game_time = Convert.ToDateTime(reader["round_start_time"]).ToString("yyyy-MM-dd HH:mm:ss");

                                row.game_result = reader["game_result"].ToString();

                                reply.rows.Add(row);
                            }
                        }
                    }
                }
                okay = true;
            }

            if (okay)
            {
                reply.error_code = 0;
                reply.error_message = "ok";
            }
            else
            {
                reply.error_code = 1;
                reply.error_message = "Failed to get game results from DB";
            }

            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(reply));
        }

        [Access(Name = "get-bet-trans")]
        public async Task GetBetTransactions(RequestContext ctx)
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

            bool okay = false;

            dynamic reply = new ExpandoObject();
            reply.rows = new List<dynamic>();

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string betId = req.betId;

            if (string.IsNullOrEmpty(betId))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            string sqlselect1 = " select a.debit_uuid as trans_uuid, 'debit' as trans_type, (0 - a.debit_amount) as trans_amount, "
                                + " a.is_success, a.is_cancelled, a.network_error, a.response_error ";
            string sqlfrom1 = " from tbl_trans_debit a ";
            string sqlwhere1 = " where a.bet_uuid = @betId ";

            string sqlselect2 = " select a.credit_uuid as trans_uuid, 'credit' as trans_type, a.credit_amount as trans_amount, "
                                + " a.is_success, a.is_cancelled, a.network_error, a.response_error ";
            string sqlfrom2 = " from tbl_trans_credit a ";
            string sqlwhere2 = " where a.bet_uuid = @betId ";

            string sql1 = " ( " + sqlselect1 + sqlfrom1 + sqlwhere1 + " ) ";
            string sql2 = " ( " + sqlselect2 + sqlfrom2 + sqlwhere2 + " ) ";

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_CommonMerchantDb)) // ...
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@betId", betId);

                    cmd.CommandText = " select trans.* from ( " + sql1 + " union all " + sql2 + " ) trans ";

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new
                            {
                                trans_uuid = reader["trans_uuid"].ToString(),
                                trans_type = reader["trans_type"].ToString(),
                                trans_amount = Convert.ToDecimal(reader["trans_amount"].ToString()),
                                is_success = Convert.ToInt32(reader["is_success"].ToString()),
                                is_cancelled = Convert.ToInt32(reader["is_cancelled"].ToString()),
                                network_error = Convert.ToInt32(reader["network_error"].ToString()),
                                response_error = Convert.ToInt32(reader["response_error"].ToString())

                            };

                            reply.rows.Add(item);
                        }
                    }
                }
                okay = true;
            }

            if (okay)
            {
                reply.error_code = 0;
                reply.error_message = "ok";
            }
            else
            {
                reply.error_code = 1;
                reply.error_message = "Failed to get trans records from DB";
            }

            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(reply));

        }

        [Access(Name = "get-bet-records")]
        public async Task GetBetRecords(RequestContext ctx)
        {
            //ctx.Logger.Info("bo-data | get-bet-records");

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

            bool okay = false;

            dynamic reply = new ExpandoObject();
            reply.rows = new List<dynamic>();

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string pageSizeStr = req.pageSize;
            string pageNumberStr = req.pageNumber;

            int pageSize = Convert.ToInt32(pageSizeStr);
            int pageNumber = Convert.ToInt32(pageNumberStr);

            if (pageSize <= 0) pageSize = 1;
            if (pageNumber <= 0) pageNumber = 1;

            string startDtStr = req.fromDateTime;
            string endDtStr = req.toDateTime;

            string merchantCode = req.merchantCode;
            string currencyCode = req.currencyCode;
            string playerId = req.playerId;
            string betId = req.betId;

            string sqlwhere = " where debit_state = 1 and credit_state = 1 "
                            + " and bet_time >= @start_dt and bet_time <= @end_dt "
                            ;

            if (!string.IsNullOrEmpty(merchantCode)) sqlwhere += " and merchant_code = @merchant_code ";
            if (!string.IsNullOrEmpty(currencyCode)) sqlwhere += " and currency_code = @currency_code ";
            if (!string.IsNullOrEmpty(playerId)) sqlwhere += " and player_id = @player_id ";
            if (!string.IsNullOrEmpty(betId)) sqlwhere += " and bet_uuid = @bet_id ";

            string sqlorder = " order by bet_time desc ";

            int total = 0;
            int pageCount = 0;

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_CommonMerchantDb)) // ...
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@start_dt", startDtStr);
                    dbhelper.AddParam(cmd, "@end_dt", endDtStr);

                    if (!string.IsNullOrEmpty(merchantCode)) dbhelper.AddParam(cmd, "@merchant_code", merchantCode);
                    if (!string.IsNullOrEmpty(currencyCode)) dbhelper.AddParam(cmd, "@currency_code", currencyCode);
                    if (!string.IsNullOrEmpty(playerId)) dbhelper.AddParam(cmd, "@player_id", playerId);
                    if (!string.IsNullOrEmpty(betId)) dbhelper.AddParam(cmd, "@bet_id", betId);

                    cmd.CommandText = " select count(bet_uuid) from tbl_bet_record " + sqlwhere;
                    total = Convert.ToInt32(cmd.ExecuteScalar());
                }

                reply.total = total >= 0 ? total : 0;

                if (total > 0)
                {
                    int rest = total % pageSize;
                    pageCount = (total - rest) / pageSize;
                    if (rest > 0) pageCount += 1;

                    int offset = pageSize * (pageNumber - 1);
                    if (offset >= total)
                    {
                        pageNumber = pageCount;
                        offset = pageSize * (pageNumber - 1);
                    }

                    if (offset + pageSize > total) pageSize = total - offset;

                    using (var cmd = cnn.CreateCommand())
                    {
                        dbhelper.AddParam(cmd, "@start_dt", startDtStr);
                        dbhelper.AddParam(cmd, "@end_dt", endDtStr);

                        if (!string.IsNullOrEmpty(merchantCode)) dbhelper.AddParam(cmd, "@merchant_code", merchantCode);
                        if (!string.IsNullOrEmpty(currencyCode)) dbhelper.AddParam(cmd, "@currency_code", currencyCode);
                        if (!string.IsNullOrEmpty(playerId)) dbhelper.AddParam(cmd, "@player_id", playerId);
                        if (!string.IsNullOrEmpty(betId)) dbhelper.AddParam(cmd, "@bet_id", betId);

                        cmd.CommandText = " select * from tbl_bet_record ";
                        cmd.CommandText += sqlwhere + sqlorder;
                        cmd.CommandText += " limit " + offset + "," + pageSize;

                        //ctx.Logger.Info(cmd.CommandText);
                        //ctx.Logger.Info(merchantCode);
                        //ctx.Logger.Info(currencyCode);
                        //ctx.Logger.Info(playerId);
                        //ctx.Logger.Info(startDtStr);
                        //ctx.Logger.Info(endDtStr);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string betdt = "";
                                try
                                {
                                    var betTime = Convert.ToDateTime(reader["bet_time"]);
                                    betdt = betTime.ToString("yyyy-MM-dd HH:mm:ss");
                                }
                                catch { }

                                var item = new
                                {
                                    merchant = reader["merchant_code"].ToString(),
                                    currency = reader["currency_code"].ToString(),
                                    player = reader["player_id"].ToString(),

                                    game_id = reader["table_code"].ToString()
                                                + "-" + reader["shoe_code"].ToString()
                                                + "-" + reader["round_number"].ToString(),

                                    game_result = reader["game_result"].ToString(),
                                    //game_output = reader["game_output"].ToString(),

                                    bet_id = reader["bet_uuid"].ToString(),
                                    bet_time = betdt,

                                    bet_pool = Convert.ToInt32(reader["bet_type"].ToString()),
                                    bet_input = reader["game_input"].ToString(),
                                    betted_lines = Convert.ToInt32(reader["betted_lines"].ToString()),

                                    bet_amount = Convert.ToDecimal(reader["bet_amount"].ToString()),
                                    pay_amount = Convert.ToDecimal(reader["pay_amount"].ToString()),

                                };

                                reply.rows.Add(item);
                            }
                        }
                    }
                }
                okay = true;
            }

            if (okay)
            {
                reply.error_code = 0;
                reply.error_message = "ok";
            }
            else
            {
                reply.error_code = 1;
                reply.error_message = "Failed to get bet records from DB";
            }

            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(reply));
        }
    }
}
