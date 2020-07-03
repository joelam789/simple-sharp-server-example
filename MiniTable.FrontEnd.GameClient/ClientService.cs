using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.FrontEnd.GameClient
{
    [Access(Name = "fes-client", IsPublic = false)]
    public class ClientService
    {
        ClientHolder m_Clients = null;
        protected IServerNode m_LocalNode = null;
        private string m_MainCache = "MainCache";

        [Access(Name = "on-load", IsLocal = true)]
        public async Task<string> Load(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            m_LocalNode = node;

            await Task.Delay(50);
            m_Clients = new ClientHolder(node);
            await Task.Delay(50);

            node.GetLogger().Info(this.GetType().Name + " service started");

            return "";
        }

        [Access(Name = "on-unload", IsLocal = true)]
        public async Task<string> Unload(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            await Task.Delay(50);
            if (m_Clients != null) m_Clients.Clear();
            m_Clients = null;
            await Task.Delay(50);

            return "";
        }

        [Access(Name = "on-connect", IsLocal = true)]
        public void OnConnect(IWebSession session)
        {
            //Console.WriteLine(m_LocalNode.GetName() + " - OnConnect: " + session.GetRemoteAddress());
            //m_LocalNode.GetLogger().Info("OnClientConnect: " + session.GetRequestPath());

            if (m_Clients == null)
            {
                session.CloseConnection();
                return;
            }

            var count = 0;
            var playerId = "";
            var merchantCode = "";
            var currencyCode = "";
            var sessionId = "";
            var parts = session.GetRequestPath().Split('/');
            foreach (var part in parts)
            {
                if (part.Length <= 0) continue;
                count++;
                if (count == 1) merchantCode = part;
                if (count == 2) currencyCode = part;
                if (count == 3) playerId = part;
                if (count == 4) sessionId = part;
                if (count > 4) break;
            }

            var okay = false;
            var clientToken = "";

            if (!String.IsNullOrEmpty(merchantCode)
                && !String.IsNullOrEmpty(currencyCode)
                && !String.IsNullOrEmpty(playerId)
                && !String.IsNullOrEmpty(sessionId))
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

                    if (okay)
                    {
                        clientToken = Guid.NewGuid().ToString();
                        using (var cmd = cnn.CreateCommand())
                        {
                            dbhelper.AddParam(cmd, "@client_token", clientToken);
                            dbhelper.AddParam(cmd, "@session_id", sessionId);
                            dbhelper.AddParam(cmd, "@merchant_code", merchantCode);
                            dbhelper.AddParam(cmd, "@currency_code", currencyCode);
                            dbhelper.AddParam(cmd, "@player_id", playerId);

                            cmd.CommandText = " update tbl_player_session "
                                                   + " set client_token = @client_token, update_time = NOW() "
                                                   + " where merchant_code = @merchant_code "
                                                   + " and currency_code = @currency_code "
                                                   + " and player_id = @player_id "
                                                   + " and session_id = @session_id "
                                                   ;

                            okay = cmd.ExecuteNonQuery() > 0;
                        }
                    }
                }

                
            }

            //if (okay) m_LocalNode.GetLogger().Info("Client session is ok: " + sessionId);
            //else m_LocalNode.GetLogger().Info("Invalid session: " + sessionId);

            if (okay && m_Clients != null)
            {
                m_Clients.AddClient(session.GetRemoteAddress(), merchantCode, currencyCode, playerId, session);

                var clientMsg = new
                {
                    msg = "client_info",

                    client_id = session.GetRemoteAddress(),
                    front_end = m_LocalNode.GetName(),
                    client_token = clientToken,
                    action = "connect"
                };

                var server = m_LocalNode.GetPublicServer();
                if (server != null && server.IsWorking())
                    session.Send(m_LocalNode.GetJsonHelper().ToJsonString(clientMsg));
            }
            else session.CloseConnection();

        }

        [Access(Name = "on-disconnect", IsLocal = true)]
        public void OnDisconnect(IWebSession session)
        {
            //Console.WriteLine(m_LocalNode.GetName() + " - OnDisconnect: " + session.GetRemoteAddress());

            if (m_Clients == null) return;

            if (m_Clients != null) m_Clients.RemoveClient(session.GetRemoteAddress());
        }

        [Access(Name = "kick-merchants")]
        public async Task KickMerchants(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

            if (m_Clients == null)
            {
                await ctx.Session.Send("Service not available");
                return;
            }

            string merchants = ctx.Data.ToString();
            if (merchants.Trim().Length <= 0)
            {
                await ctx.Session.Send("Invalid request");
                return;
            }

            if (merchants == "*")
            {
                ctx.Logger.Info("Kick all players ... ");
                m_Clients.KickAll();
            }
            else
            {
                var merchantCodes = merchants.Split(',');
                foreach (var merchantCode in merchantCodes)
                {
                    ctx.Logger.Info("Kick merchant: " + merchantCode + " ... ");
                    m_Clients.KickMerchant(merchantCode.Trim());
                }
            }

            await ctx.Session.Send("ok");

        }

        [Access(Name = "kick-player")]
        public async Task KickPlayer(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

            if (m_Clients == null)
            {
                await ctx.Session.Send("Service not available");
                return;
            }

            string merchantAndPlayer = ctx.Data.ToString();
            if (merchantAndPlayer.Trim().Length <= 0 || !merchantAndPlayer.Contains('|'))
            {
                await ctx.Session.Send("Invalid request");
                return;
            }

            var merchantAndPlayerParts = merchantAndPlayer.Split('|');

            if (merchantAndPlayerParts.Length >= 2)
            {
                var merchant = merchantAndPlayerParts[0];
                var player = merchantAndPlayerParts[1];

                if (!string.IsNullOrEmpty(merchant) && !string.IsNullOrEmpty(player))
                {
                    ctx.Logger.Info("Kick player - " + merchantAndPlayer);
                    m_Clients.KickPlayer(merchant, player);
                }
            }

            await ctx.Session.Send("ok");

        }

        [Access(Name = "send-player-msg")]
        public async Task SendPlayerMsg(RequestContext ctx)
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

            if (m_Clients == null)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -2,
                    error_message = "Service not available"
                }));
                return;
            }

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string playerId = req.player_id;
            string merchantCode = req.merchant_code;
            string currencyCode = req.currency_code;
            dynamic msgData = req.data;

            var client = m_Clients.FindClient(merchantCode, currencyCode, playerId);
            if (client == null)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -3,
                    error_message = "Client not found"
                }));
                return;
            }

            string msgstr = ctx.JsonHelper.ToJsonString(msgData);
            await client.Session.Send(msgstr);
            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                error_code = 0,
                error_message = "ok"
            }));

        }

        [Access(Name = "check-online")]
        public async Task CheckOnline(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send("Invalid request");
                return;
            }

            if (m_Clients == null)
            {
                await ctx.Session.Send("Service not available");
                return;
            }

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string playerId = req.player_id;
            string merchantCode = req.merchant_code;
            string currencyCode = req.currency_code;

            var client = m_Clients.FindClient(merchantCode, currencyCode, playerId);
            if (client == null)
            {
                await ctx.Session.Send("false");
            }
            else
            {
                await ctx.Session.Send("true");
            }
        }
    }
}
