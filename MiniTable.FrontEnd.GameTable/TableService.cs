using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.FrontEnd.GameTable
{
    [Access(Name = "fes-table")]
    public class TableService
    {
        TableInfoDeliverer m_Deliverer = null;
        protected IServerNode m_LocalNode = null;
        private string m_MainCache = "MainCache";

        [Access(Name = "on-load", IsLocal = true)]
        public async Task<string> Load(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            m_LocalNode = node;
            if (m_Deliverer == null) m_Deliverer = new TableInfoDeliverer(node);

            await Task.Delay(50);
            if (m_Deliverer != null) await m_Deliverer.Start();
            await Task.Delay(50);

            node.GetLogger().Info(this.GetType().Name + " service started");

            return "";
        }

        [Access(Name = "on-unload", IsLocal = true)]
        public async Task<string> Unload(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            await Task.Delay(100);
            if (m_Deliverer != null)
            {
                await m_Deliverer.Stop();
                m_Deliverer = null;
            }
            await Task.Delay(100);

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

            var okay = false;

            try
            {
                dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);
                string playerId = req.player_id;
                string merchantCode = req.merchant_code;
                string currencyCode = req.currency_code;
                string sessionId = req.session_id;
                string clientToken = req.client_token;

                if (!String.IsNullOrEmpty(merchantCode)
                    && !String.IsNullOrEmpty(currencyCode)
                    && !String.IsNullOrEmpty(playerId)
                    && !String.IsNullOrEmpty(sessionId)
                    && !String.IsNullOrEmpty(clientToken))
                {
                    var dbhelper = m_LocalNode.GetDataHelper();
                    using (var cnn = dbhelper.OpenDatabase(m_MainCache))
                    {
                        using (var cmd = cnn.CreateCommand())
                        {
                            dbhelper.AddParam(cmd, "@session_id", sessionId);
                            dbhelper.AddParam(cmd, "@merchant_code", merchantCode);
                            dbhelper.AddParam(cmd, "@currency_code", currencyCode);
                            dbhelper.AddParam(cmd, "@player_id", playerId);
                            dbhelper.AddParam(cmd, "@client_token", clientToken);

                            cmd.CommandText = " select * from tbl_player_session "
                                                   + " where merchant_code = @merchant_code "
                                                   + " and currency_code = @currency_code "
                                                   + " and player_id = @player_id "
                                                   + " and session_id = @session_id "
                                                   + " and client_token = @client_token "
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
            }
            catch (Exception ex)
            {
                okay = false;
                ctx.Logger.Error("Exception found when check front-end session: " + ex.Message);
            }

            if (!okay)
            {
                ctx.Logger.Error("Failed to check front-end session");
                return "Invalid or expired front-end session";
            }

            return "";
        }

        [Access(Name = "join-table")]
        public async Task JoinTable(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

            string replyMsgName = "join_table_reply";

            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            ctx.Logger.Info("Join Table - " + reqstr);

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string playerId = req.player_id;
            string merchantCode = req.merchant_code;
            string currencyCode = req.currency_code;
            string tableCode = req.table_code;

            string frontEnd = m_LocalNode.GetName();

            if (string.IsNullOrEmpty(frontEnd))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -2,
                    error_message = "Service not available"
                }));
                return;
            }

            var remoteInfo = "";
            var rets = await RemoteCaller.BroadcastCall(ctx.RemoteServices, "game-table", "find-game-table", tableCode);
            foreach (var item in rets)
            {
                if (item.Value == "ok")
                {
                    remoteInfo = item.Key;
                    break;
                }
            }
            if (string.IsNullOrEmpty(remoteInfo))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -2,
                    error_message = "Service not available"
                }));
                return;
            }

            var tablereq = new
            {
                player_id = playerId,
                merchant_code = merchantCode,
                currency_code = currencyCode,
                table_code = tableCode,
                front_end = frontEnd
            };

            var reply = await RemoteCaller.SpecifiedCall(ctx.RemoteServices, RemoteCaller.GetServerNameFromRemoteInfo(remoteInfo),
                                            "game-table", "join-game-table", ctx.JsonHelper.ToJsonString(tablereq));

            if (string.IsNullOrEmpty(reply))
            {
                reply = ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -3,
                    error_message = "Failed to call join table function"
                });
            }
            else
            {
                dynamic ret = ctx.JsonHelper.ToJsonObject(reply);
                ret.msg = replyMsgName;
                reply = ctx.JsonHelper.ToJsonString(ret);
            }

            await ctx.Session.Send(reply);

        }

        [Access(Name = "leave-table")]
        public async Task LeaveTable(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

            string replyMsgName = "leave_table_reply";

            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            ctx.Logger.Info("Leave Table - " + reqstr);

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string playerId = req.player_id;
            string merchantCode = req.merchant_code;
            string currencyCode = req.currency_code;
            string tableCode = req.table_code;

            string frontEnd = m_LocalNode.GetName();

            if (string.IsNullOrEmpty(frontEnd))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -2,
                    error_message = "Service not available"
                }));
                return;
            }

            var remoteInfo = "";
            var rets = await RemoteCaller.BroadcastCall(ctx.RemoteServices, "game-table", "find-game-table", tableCode);
            foreach (var item in rets)
            {
                if (item.Value == "ok")
                {
                    remoteInfo = item.Key;
                    break;
                }
            }
            if (string.IsNullOrEmpty(remoteInfo))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -2,
                    error_message = "Service not available"
                }));
                return;
            }

            var tablereq = new
            {
                player_id = playerId,
                merchant_code = merchantCode,
                currency_code = currencyCode,
                table_code = tableCode,
                front_end = frontEnd
            };

            var reply = await RemoteCaller.SpecifiedCall(ctx.RemoteServices, RemoteCaller.GetServerNameFromRemoteInfo(remoteInfo),
                                            "game-table", "leave-game-table", ctx.JsonHelper.ToJsonString(tablereq));

            if (string.IsNullOrEmpty(reply))
            {
                reply = ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -3,
                    error_message = "Failed to call leave table function"
                });
            }
            else
            {
                dynamic ret = ctx.JsonHelper.ToJsonObject(reply);
                ret.msg = replyMsgName;
                reply = ctx.JsonHelper.ToJsonString(ret);
            }

            await ctx.Session.Send(reply);

        }

        [Access(Name = "place-bet")]
        public async Task PlaceBet(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

            string replyMsgName = "place_bet_reply";

            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            ctx.Logger.Info("Place Bet - " + reqstr);

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string playerId = req.player_id;
            string merchantCode = req.merchant_code;
            string currencyCode = req.currency_code;
            string tableCode = req.table_code;
            string clientId = req.client_id;
            string sessionId = req.session_id;
            //string clientToken = req.client_token;

            string frontEnd = m_LocalNode.GetName();

            if (string.IsNullOrEmpty(frontEnd))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -2,
                    error_message = "Service not available"
                }));
                return;
            }

            var remoteInfo = "";
            var rets = await RemoteCaller.BroadcastCall(ctx.RemoteServices, "game-table", "find-game-table", tableCode);
            foreach (var item in rets)
            {
                if (item.Value == "ok")
                {
                    remoteInfo = item.Key;
                    break;
                }
            }
            if (string.IsNullOrEmpty(remoteInfo))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -2,
                    error_message = "Service not available"
                }));
                return;
            }

            var tablereq = new
            {
                player_id = playerId,
                merchant_code = merchantCode,
                currency_code = currencyCode,
                table_code = tableCode,
                client_id = clientId,
                session_id = sessionId,
                front_end = frontEnd
            };

            var reply = await RemoteCaller.SpecifiedCall(ctx.RemoteServices, RemoteCaller.GetServerNameFromRemoteInfo(remoteInfo),
                                            "game-table", "place-game-bet", ctx.JsonHelper.ToJsonString(tablereq));

            if (string.IsNullOrEmpty(reply))
            {
                reply = ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -3,
                    error_message = "Failed to call place bet function"
                });
            }
            else
            {
                dynamic ret = ctx.JsonHelper.ToJsonObject(reply);
                ret.msg = replyMsgName;
                reply = ctx.JsonHelper.ToJsonString(ret);
            }

            await ctx.Session.Send(reply);

        }

        [Access(Name = "chat")]
        public async Task Chat(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

            string replyMsgName = "chat_reply";

            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            ctx.Logger.Info("Chat - " + reqstr);

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string playerId = req.player_id;
            string merchantCode = req.merchant_code;
            string currencyCode = req.currency_code;
            string tableCode = req.table_code;
            string chatMsg = req.message;

            string frontEnd = m_LocalNode.GetName();

            if (string.IsNullOrEmpty(frontEnd))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -2,
                    error_message = "Service not available"
                }));
                return;
            }

            var remoteInfo = "";
            var rets = await RemoteCaller.BroadcastCall(ctx.RemoteServices, "game-table", "find-game-table", tableCode);
            foreach (var item in rets)
            {
                if (item.Value == "ok")
                {
                    remoteInfo = item.Key;
                    break;
                }
            }
            if (string.IsNullOrEmpty(remoteInfo))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -2,
                    error_message = "Service not available"
                }));
                return;
            }

            var tablereq = new
            {
                player_id = playerId,
                merchant_code = merchantCode,
                currency_code = currencyCode,
                table_code = tableCode,
                message = chatMsg
            };

            var reply = await RemoteCaller.SpecifiedCall(ctx.RemoteServices, RemoteCaller.GetServerNameFromRemoteInfo(remoteInfo),
                                            "game-table", "chat", ctx.JsonHelper.ToJsonString(tablereq));

            if (string.IsNullOrEmpty(reply))
            {
                reply = ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -3,
                    error_message = "Failed to call place bet function"
                });
            }
            else
            {
                dynamic ret = ctx.JsonHelper.ToJsonObject(reply);
                ret.msg = replyMsgName;
                reply = ctx.JsonHelper.ToJsonString(ret);
            }

            await ctx.Session.Send(reply);

        }

        [Access(Name = "play-game")]
        public async Task PlayGame(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

            string replyMsgName = "play_game_reply";

            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -1,
                    error_message = "Invalid request"
                }));
                return;
            }

            ctx.Logger.Info("Play Cards - " + reqstr);

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string playerId = req.player_id;
            string merchantCode = req.merchant_code;
            string currencyCode = req.currency_code;
            string tableCode = req.table_code;
            string gameInput = req.game_input;

            string frontEnd = m_LocalNode.GetName();

            if (string.IsNullOrEmpty(frontEnd))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -2,
                    error_message = "Service not available"
                }));
                return;
            }

            var remoteInfo = "";
            var rets = await RemoteCaller.BroadcastCall(ctx.RemoteServices, "game-table", "find-game-table", tableCode);
            foreach (var item in rets)
            {
                if (item.Value == "ok")
                {
                    remoteInfo = item.Key;
                    break;
                }
            }
            if (string.IsNullOrEmpty(remoteInfo))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -2,
                    error_message = "Service not available"
                }));
                return;
            }

            var tablereq = new
            {
                player_id = playerId,
                merchant_code = merchantCode,
                currency_code = currencyCode,
                table_code = tableCode,
                game_input = gameInput
            };

            var reply = await RemoteCaller.SpecifiedCall(ctx.RemoteServices, RemoteCaller.GetServerNameFromRemoteInfo(remoteInfo),
                                            "game-table", "play-game", ctx.JsonHelper.ToJsonString(tablereq));

            if (string.IsNullOrEmpty(reply))
            {
                reply = ctx.JsonHelper.ToJsonString(new
                {
                    msg = replyMsgName,
                    error_code = -8,
                    error_message = "Failed to call play game function"
                });
            }
            else
            {
                dynamic ret = ctx.JsonHelper.ToJsonObject(reply);
                ret.msg = replyMsgName;
                reply = ctx.JsonHelper.ToJsonString(ret);
            }

            await ctx.Session.Send(reply);

        }
    }
}
