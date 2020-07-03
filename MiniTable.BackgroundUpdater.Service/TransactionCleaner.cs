using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.BackgroundUpdater.Service
{
    public class TransactionCleaner
    {
        static CommonRng m_Rng = new CommonRng();

        private Timer m_Timer = null;

        private IServerNode m_Node = null;
        private IServerLogger m_Logger = null;

        private bool m_IsRunning = false;
        private bool m_IsWorking = false;

        //private string m_MainCache = "MainCache";
        private string m_CommonMerchantDb = "MerchantDB";

        private string m_ServerName = "";

        public TransactionCleaner(IServerNode node)
        {
            m_Node = node;
            m_Logger = m_Node.GetLogger();

            m_ServerName = m_Node.GetName();

            m_IsWorking = false;
            m_IsRunning = false;
        }

        async Task<string> CallMerchantApi(string serviceName, string actionName, string jsonParam)
        {
            //System.Diagnostics.Debugger.Break();
            var svcs = m_Node.GetLocalServices();
            if (svcs.InternalServices.ContainsKey(serviceName))
            {
                var svc = svcs.InternalServices[serviceName];
                object ret = await svc.LocalCall(actionName, jsonParam);
                if (ret != null) return ret.ToString();
            }
            else
            {
                m_Logger.Error("Merchant API service not found: " + serviceName + "|" + actionName);
            }
            return null;
        }

        public async Task Start()
        {
            await Stop();
            m_IsWorking = true;
            m_IsRunning = false;
            m_Timer = new Timer(Tick, m_Rng, 5000, 1000 * 60);
        }

        public async Task Stop()
        {
            m_IsWorking = false;
            if (m_Timer != null)
            {
                await Task.Delay(500);
                m_Timer.Dispose();
                m_Timer = null;
                await Task.Delay(300);
            }
        }

        private async void Tick(object param)
        {
            if (!m_IsWorking) return;
            if (m_IsRunning) return;
            m_IsRunning = true;
            try
            {
                await CleanUp();
            }
            catch (Exception ex)
            {
                m_Logger.Error(ex.ToString());
                m_Logger.Error(ex.StackTrace);
            }
            finally
            {
                m_IsRunning = false;
            }

        }

        private async Task CleanUp()
        {
            try
            {
                await TryToCancelDebits();
            }
            catch (Exception ex)
            {
                m_Logger.Error("Errors found in TryToCancelDebits function: " + ex.Message);
            }

            try
            {
                await TryToRedoCredits();
            }
            catch (Exception ex)
            {
                m_Logger.Error("Errors found in TryToRedoCredits function: " + ex.Message);
            }

        }

        private async Task TryToCancelDebits()
        {
            //System.Diagnostics.Debugger.Break();

            //m_Logger.Info("TryToCancelDebits - START -");

            var totalCount = 0;

            Dictionary<string, List<dynamic>> debitItems = new Dictionary<string, List<dynamic>>();
            Dictionary<string, dynamic> merchants = new Dictionary<string, dynamic>();
            var dbhelper = m_Node.GetDataHelper();
            using (var cnn = dbhelper.OpenDatabase(m_CommonMerchantDb))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    // select records which need to cancel
                    cmd.CommandText = " select debit_uuid, bet_uuid, player_id, client_id, session_id, "
                                    + " merchant_code, currency_code, debit_amount from tbl_trans_debit "
                                    + " where is_cancelled = 0 and network_error <> 0 ";

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string debitUuid = reader["debit_uuid"].ToString();
                            string betUuid = reader["bet_uuid"].ToString();
                            string playerId = reader["player_id"].ToString();
                            string clientId = reader["client_id"].ToString();
                            string sessionId = reader["session_id"].ToString();
                            string merchantCode = reader["merchant_code"].ToString();
                            string currencyCode = reader["currency_code"].ToString();
                            decimal debitAmount = Convert.ToDecimal(reader["debit_amount"].ToString());

                            var reqIp = clientId;
                            if (reqIp.Contains(":")) reqIp = reqIp.Split(':')[0];

                            var item = new
                            {
                                debit_uuid = debitUuid,
                                trans_uuid = debitUuid + "-cancel",
                                bet_uuid = betUuid,
                                merchant_code = merchantCode,
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
                        string merchantInfo = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                                        "merchant-data", "get-merchant-info", item.Key);

                        if (String.IsNullOrEmpty(merchantInfo) || !merchantInfo.Contains('{') || !merchantInfo.Contains(':'))
                        {
                            continue;
                        }

                        dynamic merchant = m_Node.GetJsonHelper().ToJsonObject(merchantInfo);
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
                            string retJson = await CallMerchantApi(apiSvc, "cancel-debit", m_Node.GetJsonHelper().ToJsonString(apiReq));
                            ret = string.IsNullOrEmpty(retJson) ? null : m_Node.GetJsonHelper().ToJsonObject(retJson);
                        }
                        catch (Exception ex)
                        {
                            ret = null;
                            m_Node.GetLogger().Error("Failed to call cancel debit: " + ex.Message);
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

                            }

                            totalCount++;
                        }
                        catch (Exception ex)
                        {
                            ret = null;
                            m_Node.GetLogger().Error("Failed to call cancel debit: " + ex.Message);
                        }


                    } // end of debits of same merchant

                } // end of all debits

            } // end of using db cnn

            if (totalCount > 0) m_Logger.Info("TryToCancelDebits - DONE (" + totalCount + ")");

        }

        private async Task TryToRedoCredits()
        {
            //m_Logger.Info("TryToRedoCredits - START -");

            var totalCount = 0;

            Dictionary<string, List<dynamic>> creditItems = new Dictionary<string, List<dynamic>>();
            Dictionary<string, dynamic> merchants = new Dictionary<string, dynamic>();
            var dbhelper = m_Node.GetDataHelper();
            using (var cnn = dbhelper.OpenDatabase(m_CommonMerchantDb))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    //int dtColumnIndex = 11;

                    // select records which need to retry
                    cmd.CommandText = " select credit_uuid, merchant_code, currency_code, "
                                    + " bet_uuid, player_id, client_id, session_id, round_id, request_times, "
                                    + " bet_pool, credit_amount, create_time "
                                    + " from tbl_trans_credit "
                                    + " where is_cancelled = 0 and is_success = 0 "
                                    + " and (network_error <> 0 || (request_times = 0 and TIMESTAMPDIFF(SECOND, create_time, NOW()) > 60)) ";

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string creditUuid = reader["credit_uuid"].ToString();
                            string merchantCode = reader["merchant_code"].ToString();
                            string currencyCode = reader["currency_code"].ToString();

                            string betUuid = reader["bet_uuid"].ToString();
                            string playerId = reader["player_id"].ToString();
                            string roundId = reader["round_id"].ToString();

                            string clientId = reader["client_id"].ToString();
                            string sessionId = reader["session_id"].ToString();

                            int betPool = Convert.ToInt32(reader["bet_pool"].ToString());
                            decimal creditAmount = Convert.ToDecimal(reader["credit_amount"].ToString());

                            int reqTimes = Convert.ToInt32(reader["request_times"].ToString());

                            //var creditTime = reader.GetDateTime(dtColumnIndex);
                            //var creditdt = creditTime.ToString("yyyy-MM-dd HH:mm:ss");

                            var creditdt = Convert.ToDateTime(reader["create_time"]).ToString("yyyy-MM-dd HH:mm:ss");

                            var reqIp = clientId;
                            if (reqIp.Contains(":")) reqIp = reqIp.Split(':')[0];

                            var item = new
                            {
                                credit_uuid = creditUuid,
                                bet_uuid = betUuid,
                                merchant_code = merchantCode,
                                currency_code = currencyCode,
                                player_id = playerId,
                                player_ip = reqIp,
                                session_id = sessionId,
                                round_id = roundId,
                                bet_pool = betPool,
                                credit_amount = creditAmount,
                                bet_settle_time = creditdt,
                                request_times = reqTimes + 1,
                                //is_cancelled = false
                            };

                            string merchantKey = item.merchant_code + item.currency_code;

                            if (creditItems.ContainsKey(merchantKey))
                            {
                                var list = creditItems[merchantKey];
                                list.Add(item);
                            }
                            else
                            {
                                var list = new List<dynamic>();
                                list.Add(item);
                                creditItems.Add(merchantKey, list);
                            }

                        }
                    }
                }

                foreach (var item in creditItems)
                {
                    if (!merchants.ContainsKey(item.Key))
                    {
                        string merchantInfo = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                                        "merchant-data", "get-merchant-info", item.Key);

                        if (String.IsNullOrEmpty(merchantInfo) || !merchantInfo.Contains('{') || !merchantInfo.Contains(':'))
                        {
                            continue;
                        }

                        dynamic merchant = m_Node.GetJsonHelper().ToJsonObject(merchantInfo);
                        if (merchant != null) merchants.Add(item.Key, merchant);
                    }

                    if (!merchants.ContainsKey(item.Key)) continue;

                    string apiUrl = merchants[item.Key].url.ToString();
                    string apiSvc = merchants[item.Key].service.ToString();

                    var list = item.Value;

                    foreach (var credit in list)
                    {
                        
                        var apiReq = new
                        {
                            merchant_url = apiUrl,
                            credit.credit_uuid,
                            credit.bet_uuid,
                            credit.merchant_code,
                            credit.currency_code,
                            credit.player_id,
                            credit.player_ip,
                            credit.session_id,
                            credit.round_id,
                            credit.bet_pool,
                            credit.credit_amount,
                            credit.bet_settle_time,
                            credit.request_times,
                            is_cancelled = false
                        };

                        dynamic ret = null;
                        try
                        {
                            string retJson = await CallMerchantApi(apiSvc, "credit-for-settling", m_Node.GetJsonHelper().ToJsonString(apiReq));
                            ret = string.IsNullOrEmpty(retJson) ? null : m_Node.GetJsonHelper().ToJsonObject(retJson);
                        }
                        catch (Exception ex)
                        {
                            ret = null;
                            m_Node.GetLogger().Error("Failed to call redo credit: " + ex.Message);
                        }

                        try
                        {
                            if (ret != null)
                            {
                                int respCode = ret.error_code;

                                var sql = " update tbl_trans_credit "
                                    + " set network_error = 0 , response_error = " + respCode;
                                if (respCode == 0) sql += " , is_success = 1 ";
                                sql += " , request_times = request_times + 1 , update_time = NOW()  ";
                                sql += " where credit_uuid = @credit_uuid ";

                                var okay = false;
                                var trans = cnn.BeginTransaction();

                                using (var cmd = cnn.CreateCommand())
                                {
                                    cmd.Transaction = trans;

                                    dbhelper.AddParam(cmd, "@credit_uuid", credit.credit_uuid);
                                    cmd.CommandText = sql;

                                    okay = cmd.ExecuteNonQuery() > 0;
                                }

                                if (okay)
                                {
                                    sql = " update tbl_bet_record set credit_state = 1 ";
                                    sql += " , update_time = CURRENT_TIMESTAMP ";
                                    sql += " where bet_uuid = @bet_uuid ";

                                    using (var cmd = cnn.CreateCommand())
                                    {
                                        cmd.Transaction = trans;

                                        dbhelper.AddParam(cmd, "@bet_uuid", credit.bet_uuid);

                                        cmd.CommandText = sql;

                                        okay = okay && cmd.ExecuteNonQuery() > 0;
                                    }
                                }

                                if (okay) trans.Commit();
                                else trans.Rollback();

                            }
                            else
                            {
                                var sql = " update tbl_trans_credit ";
                                sql += " set request_times = request_times + 1 , update_time = NOW()  ";
                                sql += " where credit_uuid = @credit_uuid ";

                                using (var cmd = cnn.CreateCommand())
                                {
                                    dbhelper.AddParam(cmd, "@credit_uuid", credit.credit_uuid);
                                    cmd.CommandText = sql;
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            totalCount++;
                        }
                        catch (Exception ex)
                        {
                            ret = null;
                            m_Node.GetLogger().Error("Failed to call redo credit: " + ex.Message);
                        }


                    } // end of credits of same merchant

                } // end of all credits

            } // end of using db cnn

            if (totalCount > 0) m_Logger.Info("TryToRedoCredits - DONE (" + totalCount + ")");
        }
    }
}
