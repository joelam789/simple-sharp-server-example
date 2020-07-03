using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.Login.Service
{
    [Access(Name = "login")]
    public class LoginService
    {
        protected string m_MainCache = "MainCache";

        async Task<string> CallMerchantApi(RequestContext ctx, string serviceName, string actionName, string jsonParam)
        {
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

        [Access(Name = "on-load", IsLocal = true)]
        public async Task<string> Load(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            await Task.Delay(50);
            node.GetLogger().Info(this.GetType().Name + " service started");
            await Task.Delay(50);

            return "";
        }

        [Access(Name = "player-login")]
        public async Task PlayerLogin(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

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

            var reqIp = ctx.ClientAddress;
            if (reqIp.Contains(":")) reqIp = reqIp.Split(':')[0];

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string merchantCode = req.merchant_code.ToString();
            string currencyCode = req.currency_code.ToString();
            string playerId = req.player_id.ToString();

            string merchantInfo = await RemoteCaller.RandomCall(ctx.RemoteServices,
                                        "merchant-data", "get-merchant-info", merchantCode + currencyCode);

            if (String.IsNullOrEmpty(merchantInfo) || !merchantInfo.Contains('{') || !merchantInfo.Contains(':'))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Merchant info not found: " + req.merchant_code.ToString()
                }));
                return;
            }

            bool merchantOK = false;

            dynamic merchant = ctx.JsonHelper.ToJsonObject(merchantInfo);
            string merchantUrl = "";
            string merchantService = "";

            try
            {
                merchantUrl = merchant.url.ToString();
                merchantService = merchant.service.ToString();

                if (merchantUrl.Length > 0 && merchantService.Length > 0)
                {
                    if (merchant.active > 0 && merchant.maintaining == 0)
                    {
                        merchantOK = true;
                    }
                }
            }
            catch
            {
                merchantOK = false;
            }

            if (!merchantOK)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Merchant is not available: " + req.merchant_code.ToString()
                }));
                return;
            }


            string loginToken = "";
            try
            {
                loginToken = req.login_token.ToString();
            }
            catch { }

            ctx.Logger.Info("Player login - [" + req.merchant_code.ToString() + "] " + req.player_id.ToString());
            ctx.Logger.Info("Merchant URL - " + merchantUrl);

            var apiReq = new
            {
                merchant_url = merchantUrl,
                player_ip = reqIp,
                req.merchant_code,
                req.currency_code,
                req.player_id,
                req.login_token
            };

            string retJson = await CallMerchantApi(ctx, merchantService, "player-login", ctx.JsonHelper.ToJsonString(apiReq));
            dynamic ret = string.IsNullOrEmpty(retJson) ? null : ctx.JsonHelper.ToJsonObject(retJson);

            if (ret == null || ret.error_code != 0)
            {
                ctx.Logger.Error("Three-Way Login Error: " + (ret == null ? "Failed to call merchant API" : ret.error_message));
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Three-Way Login Error"
                }));
                return;
            }

            var okay = false;
            var sessionId = Guid.NewGuid().ToString();
            if (loginToken.Length >= 30) sessionId = loginToken;

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_MainCache))
            {
                var trans = cnn.BeginTransaction();
                using (var cmd = cnn.CreateCommand())
                {
                    cmd.Transaction = trans;

                    dbhelper.AddParam(cmd, "@session_id", sessionId);
                    dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                    dbhelper.AddParam(cmd, "@currency_code", req.currency_code);
                    dbhelper.AddParam(cmd, "@player_id", req.player_id);

                    cmd.CommandText = " delete from tbl_player_session "
                                           + " where merchant_code = @merchant_code "
                                           + " and currency_code = @currency_code "
                                           + " and player_id = @player_id ";

                    okay = cmd.ExecuteNonQuery() > 0;

                    using (var cmd2 = cnn.CreateCommand())
                    {
                        cmd2.Transaction = trans;

                        dbhelper.AddParam(cmd2, "@session_id", sessionId);
                        dbhelper.AddParam(cmd2, "@merchant_code", req.merchant_code);
                        dbhelper.AddParam(cmd2, "@currency_code", req.currency_code);
                        dbhelper.AddParam(cmd2, "@player_id", req.player_id);

                        cmd2.CommandText = " insert into tbl_player_session "
                                               + " ( session_id , merchant_code, currency_code, player_id, update_time ) values "
                                               + " ( @session_id, @merchant_code, @currency_code, @player_id, NOW() ) ";

                        okay = cmd2.ExecuteNonQuery() > 0;
                    }
                }

                if (okay) trans.Commit();
                else trans.Rollback();
            }

            if (!okay)
            {
                ctx.Logger.Error("Three-Way Login Failed!");
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Three-Way Login Failed"
                }));
                return;
            }
            else
            {
                // try to close old player connection...

                string data = merchantCode + "|" + playerId;
                await RemoteCaller.BroadcastCall(ctx.RemoteServices, "fes-client", "kick-player", data);

            }

            var remoteServices = ctx.RemoteServices;

            var frontEndUrl = RemoteCaller.RandomPickPublicServiceUrl(remoteServices, "fes-table");
            //var betServerUrl = RemoteCaller.RandomPickPublicServiceUrl(remoteServices, "accept-bet");
            //var frontEndApiUrl = RemoteCaller.RandomPickPublicServiceUrl(remoteServices, "fes");
            //var chatServerUrl = RemoteCaller.RandomPickPublicServiceUrl(remoteServices, "chat");

            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                req.merchant_code,
                req.currency_code,
                req.player_id,
                ret.player_balance,
                session_id = sessionId,
                merchant.cpc,
                merchant.bpl,
                front_end = frontEndUrl,
                //front_end_api = frontEndApiUrl,
                //bet_server = betServerUrl,
                //chat_server = chatServerUrl,
                error_code = 0,
                error_message = "Three-Way Login Okay"
            }));

        }

        [Access(Name = "bo-login")]
        public async Task BackOfficeUserLogin(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

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

            var dataReq = new
            {
                req.account,
                req.merchant,
                req.password
            };

            string dataReplyString = await RemoteCaller.RandomCall(ctx.RemoteServices,
                                        "bo-data", "check-account", ctx.JsonHelper.ToJsonString(dataReq));

            if (String.IsNullOrEmpty(dataReplyString))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Failed to check backoffice account from db: [" + req.merchant.ToString() + "]" + req.account.ToString()
                }));
                return;
            }

            dynamic dataReply = ctx.JsonHelper.ToJsonObject(dataReplyString);
            if (dataReply.error_code != 0)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    dataReply.error_code,
                    error_message = "Failed to validate backoffice account from db: [" + req.merchant.ToString() + "]"
                                    + req.account.ToString() + " - " + dataReply.error_message.ToString()
                }));
                return;
            }

            ctx.Logger.Info("Backoffice user login - [" + req.merchant.ToString() + "] " + req.account.ToString());

            var okay = false;
            var sessionId = Guid.NewGuid().ToString();

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_MainCache))
            {
                var trans = cnn.BeginTransaction();
                using (var cmd = cnn.CreateCommand())
                {
                    cmd.Transaction = trans;

                    dbhelper.AddParam(cmd, "@session_id", sessionId);
                    dbhelper.AddParam(cmd, "@account_id", req.account);
                    dbhelper.AddParam(cmd, "@merchant_code", req.merchant);

                    cmd.CommandText = " delete from tbl_bo_session "
                                           + " where merchant_code = @merchant_code and account_id = @account_id ";

                    okay = cmd.ExecuteNonQuery() > 0;

                    using (var cmd2 = cnn.CreateCommand())
                    {
                        cmd2.Transaction = trans;

                        dbhelper.AddParam(cmd2, "@session_id", sessionId);
                        dbhelper.AddParam(cmd2, "@account_id", req.account);
                        dbhelper.AddParam(cmd2, "@merchant_code", req.merchant);

                        cmd2.CommandText = " insert into tbl_bo_session "
                                            + " ( session_id , account_id , merchant_code, last_access_time ) values "
                                            + " ( @session_id, @account_id, @merchant_code, NOW() ) ";

                        okay = cmd2.ExecuteNonQuery() > 0;
                    }
                }

                if (okay) trans.Commit();
                else trans.Rollback();
            }

            if (!okay)
            {
                ctx.Logger.Error("Failed to let backoffice user login: cache error");
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Failed to let backoffice user login: cache error"
                }));
                return;
            }

            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                session_id = sessionId,
                req.account,
                req.merchant,
                error_code = 0,
                error_message = "ok"
            }));

        }


        [Access(Name = "bo-logout")]
        public async Task BackOfficeUserLogout(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

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

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string sessionId = req.sessionId;
            string accountId = req.accountId;
            string merchantCode = req.merchantCode;

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_MainCache))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@session_id", sessionId);
                    dbhelper.AddParam(cmd, "@account_id", accountId);
                    dbhelper.AddParam(cmd, "@merchant_code", merchantCode);

                    cmd.CommandText = " delete from tbl_bo_session "
                                           + " where session_id = @session_id and merchant_code = @merchant_code and account_id = @account_id ";

                    okay = cmd.ExecuteNonQuery() > 0;

                }

            }

            if (!okay)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Failed to let backoffice user logout: cache error"
                }));
                return;
            }
            else
            {
                ctx.Logger.Info("Backoffice user logout: " + accountId + "|" + merchantCode + "|" + sessionId);
            }

            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                error_code = 0,
                error_message = "ok"
            }));

        }

    }
}
