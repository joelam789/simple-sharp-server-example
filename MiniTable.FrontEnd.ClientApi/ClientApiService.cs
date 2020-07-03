using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.FrontEnd.ClientApi
{
    [Access(Name = "fes-api")]
    public class ClientApiService
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
                ctx.Logger.Error("Merchant API service not found: " + actionName);
            }
            return null;
        }

        [Access(Name = "on-load", IsLocal = true)]
        public async Task<string> Load(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            await Task.Delay(50);
            //m_LocalNode = node;
            await Task.Delay(50);

            node.GetLogger().Info(this.GetType().Name + " service started");

            return "";
        }

        [Access(Name = "validate-request")]
        public string ValidateRequest(RequestContext ctx)
        {
            string betstr = ctx.Data.ToString();
            if (betstr.Trim().Length <= 0)
            {
                return "Invalid request";
            }

            dynamic betreq = ctx.JsonHelper.ToJsonObject(betstr);

            string playerId = betreq.player_id;
            string merchantCode = betreq.merchant_code;
            string currencyCode = betreq.currency_code;
            string sessionId = betreq.session_id;

            var okay = false;

            if (!String.IsNullOrEmpty(merchantCode)
                && !String.IsNullOrEmpty(currencyCode)
                && !String.IsNullOrEmpty(playerId)
                && !String.IsNullOrEmpty(sessionId))
            {
                var dbhelper = ctx.DataHelper;
                using (var cnn = dbhelper.OpenDatabase(m_MainCache))
                {
                    using (var cmd = cnn.CreateCommand())
                    {
                        dbhelper.AddParam(cmd, "@session_id", sessionId);
                        dbhelper.AddParam(cmd, "@merchant_code", merchantCode);
                        dbhelper.AddParam(cmd, "@currency_code", currencyCode);
                        dbhelper.AddParam(cmd, "@player_id", playerId);

                        cmd.CommandText = " select * from tbl_player_session "
                                               + " where merchant_code = @merchant_code "
                                               + " and currency_code = @currency_code "
                                               + " and player_id = @player_id "
                                               + " and session_id = @session_id "
                                               ;

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                okay = true;
                            }
                        }
                    }
                }
            }

            if (!okay)
            {
                ctx.Logger.Info("Invalid session: " + sessionId);
                return "Invalid session";
            }

            return "";
        }

        [Access(Name = "get-player-balance")]
        public async Task GetPlayerBalance(RequestContext ctx)
        {
            string msgName = "player_balance";

            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = msgName,
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            var reqIp = ctx.ClientAddress;
            if (reqIp.Contains(":")) reqIp = reqIp.Split(':')[0];

            ctx.Logger.Info("Client Ip - " + reqIp);

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string merchantCode = req.merchant_code.ToString();
            string currencyCode = req.currency_code.ToString();

            string merchantInfo = await RemoteCaller.RandomCall(ctx.RemoteServices,
                                        "merchant-data", "get-merchant-info", merchantCode + currencyCode);

            if (String.IsNullOrEmpty(merchantInfo) || !merchantInfo.Contains('{') || !merchantInfo.Contains(':'))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = msgName,
                    error_code = -1,
                    error_message = "Merchant info not found: " + req.merchant_code.ToString()
                }));
                return;
            }

            dynamic merchant = ctx.JsonHelper.ToJsonObject(merchantInfo);
            string merchantUrl = merchant.url.ToString();
            string merchantService = merchant.service.ToString();

            //ctx.Logger.Info("Merchant URL - " + merchantUrl);

            var apiReq = new
            {
                merchant_url = merchantUrl,
                req.merchant_code,
                req.currency_code,
                req.player_id,
                req.session_id,
                player_ip = reqIp
            };
            string retJson = await CallMerchantApi(ctx, merchantService, "get-player-balance", ctx.JsonHelper.ToJsonString(apiReq));
            dynamic ret = string.IsNullOrEmpty(retJson) ? null : ctx.JsonHelper.ToJsonObject(retJson);

            if (ret == null || ret.error_code != 0)
            {
                ctx.Logger.Error("Get Player Balance Error: " + (ret == null ? "Failed to call merchant API" : ret.error_message));
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = msgName,
                    error_code = -1,
                    error_message = "Get Player Balance Error"
                }));
                return;
            }

            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                msg = msgName,
                req.merchant_code,
                req.currency_code,
                req.player_id,
                ret.player_balance,
                error_code = 0,
                error_message = "ok"
            }));

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

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string startDtStr = req.start_dt;
            string endDtStr = req.end_dt;

            if (String.IsNullOrEmpty(startDtStr) || String.IsNullOrEmpty(endDtStr))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            DateTime dtStart = DateTime.MinValue;
            DateTime dtEnd = DateTime.MinValue;

            if (!DateTime.TryParseExact(startDtStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dtStart)
                || !DateTime.TryParseExact(endDtStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dtEnd))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            var dbReq = new
            {
                start_dt = startDtStr,
                end_dt = endDtStr
            };

            string dbReplyStr = await RemoteCaller.RandomCall(ctx.RemoteServices,
                                        "game-data", "query-game-results", ctx.JsonHelper.ToJsonString(dbReq));

            if (String.IsNullOrEmpty(dbReplyStr))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Failed to query DB"
                }));
                return;
            }

            await ctx.Session.Send(dbReplyStr);

        }

        [Access(Name = "get-player-bets")]
        public async Task GetPlayerBets(RequestContext ctx)
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

            string startDtStr = req.start_dt;
            string endDtStr = req.end_dt;

            if (String.IsNullOrEmpty(startDtStr) || String.IsNullOrEmpty(endDtStr))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            DateTime dtStart = DateTime.MinValue;
            DateTime dtEnd = DateTime.MinValue;

            if (!DateTime.TryParseExact(startDtStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dtStart)
                || !DateTime.TryParseExact(endDtStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dtEnd))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            var dbReq = new
            {
                req.merchant_code,
                req.currency_code,
                req.player_id,
                start_dt = startDtStr,
                end_dt = endDtStr
            };

            string dbReplyStr = await RemoteCaller.RandomCall(ctx.RemoteServices,
                                        "bet-data", "get-player-bets", ctx.JsonHelper.ToJsonString(dbReq));

            if (String.IsNullOrEmpty(dbReplyStr))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Failed to query DB"
                }));
                return;
            }

            await ctx.Session.Send(dbReplyStr);

        }
    }
}
