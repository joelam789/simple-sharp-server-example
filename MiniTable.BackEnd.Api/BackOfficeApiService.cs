using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.BackEnd.Api
{
    [Access(Name = "bo-api")]
    public class BackOfficeApiService
    {
        protected string m_MainCache = "MainCache";

        [Access(Name = "on-load", IsLocal = true)]
        public async Task<string> Load(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            await Task.Delay(50);
            node.GetLogger().Info(this.GetType().Name + " service started");
            await Task.Delay(50);

            return "";
        }

        [Access(Name = "validate-request")]
        public string ValidateRequest(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                return "Invalid request";
            }

            //dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);
            var req = ctx.JsonHelper.ToDictionary(reqstr);
            string sessionId = req.ContainsKey("session_id") ? req["session_id"].ToString()
                                : (req.ContainsKey("sessionId") ? req["sessionId"].ToString() : "");

            var okay = false;

            if (!String.IsNullOrEmpty(sessionId))
            {
                var dbhelper = ctx.DataHelper;
                using (var cnn = dbhelper.OpenDatabase(m_MainCache))
                {
                    using (var cmd = cnn.CreateCommand())
                    {
                        dbhelper.AddParam(cmd, "@session_id", sessionId);

                        cmd.CommandText = " UPDATE tbl_bo_session "
                                               + " SET last_access_time = NOW() "
                                               + " WHERE session_id = @session_id "
                                               + " AND TIMESTAMPDIFF(SECOND, last_access_time, NOW()) <= 1800 " // session timeout in 30 mins
                                               ;

                        okay = cmd.ExecuteNonQuery() > 0;
                    }
                }
            }

            if (!okay)
            {
                ctx.Logger.Info("Invalid or expired backoffice session: " + sessionId);
                return "Invalid or expired backoffice session";
            }
            else ctx.Logger.Info("Backoffice session is ok: " + sessionId);

            return "";
        }

        [Access(Name = "check-session")]
        public async Task CheckSession(RequestContext ctx)
        {
            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                error = 0,
                error_code = 0
            }));
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

            var req = ctx.JsonHelper.ToDictionary(reqstr);
            IDictionary<string, object> queryParam = req.ContainsKey("queryParam") ? req["queryParam"] as IDictionary<string, object> : null;

            if (queryParam == null)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Failed to set new password: missing params",
                }));
                return;
            }

            string accountId = queryParam.ContainsKey("userId") ? queryParam["userId"].ToString() : "";
            string merchantCode = queryParam.ContainsKey("merchantCode") ? queryParam["merchantCode"].ToString() : "";
            string oldPassword = queryParam.ContainsKey("oldPassword") ? queryParam["oldPassword"].ToString() : "";
            string newPassword = queryParam.ContainsKey("newPassword") ? queryParam["newPassword"].ToString() : "";

            var dbReq = new
            {
                accountId,
                merchantCode,
                oldPassword,
                newPassword
            };
            string replystr = await RemoteCaller.RandomCall(ctx.RemoteServices,
                "bo-data", "change-user-password", ctx.JsonHelper.ToJsonString(dbReq));

            if (String.IsNullOrEmpty(replystr))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Failed to set new password in DB",
                }));
            }
            else
            {
                await ctx.Session.Send(replystr);
            }
        }

        [Access(Name = "get-game-results")]
        public async Task GetGameResults(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                return;
            }

            //dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);
            var req = ctx.JsonHelper.ToDictionary(reqstr);

            string sessionId = req.ContainsKey("sessionId") ? req["sessionId"].ToString() : "";

            IDictionary<string, object> queryParam = req.ContainsKey("queryParam") ? req["queryParam"] as IDictionary<string, object> : null;

            if (queryParam == null)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    total = 0,
                    rows = new List<dynamic>(),
                    error_code = -2,
                    error_message = "Failed to get game results: missing params",
                }));
                return;
            }

            string pageSize = queryParam.ContainsKey("rows") ? queryParam["rows"].ToString() : "1";
            string pageNumber = queryParam.ContainsKey("page") ? queryParam["page"].ToString() : "1";

            string merchantCode = queryParam.ContainsKey("merchantCode") ? queryParam["merchantCode"].ToString() : "";
            string userId = queryParam.ContainsKey("userId") ? queryParam["userId"].ToString() : "";
            string fromDateTime = queryParam.ContainsKey("fromDateTime") ? queryParam["fromDateTime"].ToString() : "";
            string toDateTime = queryParam.ContainsKey("toDateTime") ? queryParam["toDateTime"].ToString() : "";

            DateTime dtStart = DateTime.MinValue;
            DateTime dtEnd = DateTime.MinValue;

            if (!DateTime.TryParseExact(fromDateTime, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dtStart)
                || !DateTime.TryParseExact(toDateTime, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dtEnd))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    total = 0,
                    rows = new List<dynamic>(),
                    error_code = -3,
                    error_message = "Failed to get game results: missing or invalid datetime",
                }));
                return;
            }

            var dbReq = new
            {
                pageSize,
                pageNumber,
                fromGameTime = fromDateTime,
                toGameTime = toDateTime
            };
            string replystr = await RemoteCaller.RandomCall(ctx.RemoteServices,
                "bo-data", "get-game-results", ctx.JsonHelper.ToJsonString(dbReq));

            if (String.IsNullOrEmpty(replystr))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    total = 0,
                    rows = new List<dynamic>(),
                    error_code = -5,
                    error_message = "Failed to get game results from DB",
                }));
            }
            else
            {
                await ctx.Session.Send(replystr);
            }
        }

        [Access(Name = "get-bet-trans")]
        public async Task GetBetTransactions(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();
            //ctx.Logger.Info("bo-api | get-bet-trans");

            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                return;
            }

            var req = ctx.JsonHelper.ToDictionary(reqstr);

            string sessionId = req.ContainsKey("sessionId") ? req["sessionId"].ToString() : "";

            IDictionary<string, object> queryParam = req.ContainsKey("queryParam") ? req["queryParam"] as IDictionary<string, object> : null;

            if (queryParam == null)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    total = 0,
                    rows = new List<dynamic>(),
                    error_code = -2,
                    error_message = "Failed to get bet trans: missing params",
                }));
                return;
            }

            string merchantCode = queryParam.ContainsKey("merchantCode") ? queryParam["merchantCode"].ToString() : "";
            //string currencyCode = queryParam.ContainsKey("queryCurrency") ? queryParam["queryCurrency"].ToString() : "";
            string betId = queryParam.ContainsKey("betId") ? queryParam["betId"].ToString() : "";


            var dbReq = new
            {
                betId
            };
            string replystr = await RemoteCaller.RandomCall(ctx.RemoteServices,
                "bo-data", "get-bet-trans", ctx.JsonHelper.ToJsonString(dbReq));

            if (String.IsNullOrEmpty(replystr))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    total = 0,
                    rows = new List<dynamic>(),
                    error_code = -5,
                    error_message = "Failed to get bet transactions from DB",
                }));
            }
            else
            {
                await ctx.Session.Send(replystr);
            }
        }

        [Access(Name = "get-bet-records")]
        public async Task GetBetRecords(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

            //ctx.Logger.Info("bo-api | get-bet-records");

            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                return;
            }

            //dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);
            var req = ctx.JsonHelper.ToDictionary(reqstr);

            string sessionId = req.ContainsKey("sessionId") ? req["sessionId"].ToString() : "";

            IDictionary<string, object> queryParam = req.ContainsKey("queryParam") ? req["queryParam"] as IDictionary<string, object> : null;

            if (queryParam == null)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    total = 0,
                    rows = new List<dynamic>(),
                    error_code = -2,
                    error_message = "Failed to get bet records: missing params",
                }));
                return;
            }

            string pageSize = queryParam.ContainsKey("rows") ? queryParam["rows"].ToString() : "1";
            string pageNumber = queryParam.ContainsKey("page") ? queryParam["page"].ToString() : "1";

            string currentMerchant = queryParam.ContainsKey("merchantCode") ? queryParam["merchantCode"].ToString() : "";

            string merchantCode = queryParam.ContainsKey("queryMerchant") ? queryParam["queryMerchant"].ToString() : "";
            string currencyCode = queryParam.ContainsKey("queryCurrency") ? queryParam["queryCurrency"].ToString() : "";
            string playerId = queryParam.ContainsKey("queryPlayer") ? queryParam["queryPlayer"].ToString() : "";

            string betId = queryParam.ContainsKey("betId") ? queryParam["betId"].ToString() : "";

            string userId = queryParam.ContainsKey("userId") ? queryParam["userId"].ToString() : "";
            string fromDateTime = queryParam.ContainsKey("fromDateTime") ? queryParam["fromDateTime"].ToString() : "";
            string toDateTime = queryParam.ContainsKey("toDateTime") ? queryParam["toDateTime"].ToString() : "";

            if (currentMerchant != "-" && currentMerchant != merchantCode)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    total = 0,
                    rows = new List<dynamic>(),
                    error_code = -4,
                    error_message = "Failed to get bet records: missing or invalid merchant",
                }));
                return;
            }

            DateTime dtStart = DateTime.MinValue;
            DateTime dtEnd = DateTime.MinValue;

            if (!DateTime.TryParseExact(fromDateTime, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dtStart)
                || !DateTime.TryParseExact(toDateTime, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dtEnd))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    total = 0,
                    rows = new List<dynamic>(),
                    error_code = -3,
                    error_message = "Failed to get bet records: missing or invalid datetime",
                }));
                return;
            }

            var dbReq = new
            {
                pageSize,
                pageNumber,

                merchantCode,
                currencyCode,
                playerId,

                betId,

                fromDateTime,
                toDateTime
            };
            string replystr = await RemoteCaller.RandomCall(ctx.RemoteServices,
                "bo-data", "get-bet-records", ctx.JsonHelper.ToJsonString(dbReq));

            if (String.IsNullOrEmpty(replystr))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    total = 0,
                    rows = new List<dynamic>(),
                    error_code = -5,
                    error_message = "Failed to get bet records from DB",
                }));
            }
            else
            {
                await ctx.Session.Send(replystr);
            }
        }


    }
}
