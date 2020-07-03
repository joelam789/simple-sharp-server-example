using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.TableGame.Service
{
    [Access(Name = "game-table", IsPublic = false)]
    public class GameTableService
    {
        Dictionary<string, TurnBasedGameTable> m_GameTables = new Dictionary<string, TurnBasedGameTable>();

        private TurnBasedGameTable FindGameTable(string tableCode)
        {
            TurnBasedGameTable gameTable = null;
            lock (m_GameTables)
            {
                if (m_GameTables.ContainsKey(tableCode)) gameTable = m_GameTables[tableCode];
            }
            return gameTable;
        }

        private List<TurnBasedGameTable> GetGameTables()
        {
            List<TurnBasedGameTable> tables = new List<TurnBasedGameTable>();
            lock (m_GameTables)
            {
                foreach (var item in m_GameTables) tables.Add(item.Value);
            }
            return tables;
        }

        [Access(Name = "on-load", IsLocal = true)]
        public async Task<string> Load(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            node.GetLogger().Info(this.GetType().Name + " is loading settings from config...");

            TableGroupSetting tableSettings = null;

            try
            {
                ConfigurationManager.RefreshSection("appSettings");
                await Task.Delay(100);

                var keys = ConfigurationManager.AppSettings.Keys;
                foreach (var key in keys)
                {
                    if (key.ToString() == "GameTableSetting")
                    {
                        string settings = ConfigurationManager.AppSettings["GameTableSetting"].ToString();
                        tableSettings = node.GetJsonHelper().ToJsonObject<TableGroupSetting>(settings);
                        continue;
                    }

                }

            }
            catch (Exception ex)
            {
                node.GetLogger().Error("Failed to load settings from config for GameServerService: ");
                node.GetLogger().Error(ex.ToString());
            }

            //if (m_Game != null) await m_Game.Open();

            if (tableSettings != null)
            {
                lock (m_GameTables)
                {
                    foreach (var setting in tableSettings.Tables)
                    {
                        if (m_GameTables.ContainsKey(setting.TableCode)) continue;

                        TurnBasedGameTable gameTable = null;
                        if (tableSettings.GameType == GameLogicBigTwo.GAME_TYPE)
                            gameTable = new TurnBasedGameTable(node, new GameLogicBigTwo());

                        if (gameTable != null)
                        {
                            gameTable.TableCode = setting.TableCode;
                            gameTable.TableType = tableSettings.TableType;
                            gameTable.TableName = setting.TableName;
                            gameTable.TestMerchant = tableSettings.TestMerchant;
                            gameTable.TimeToBet = tableSettings.BettingTime;
                            m_GameTables.Add(gameTable.TableCode, gameTable);
                        }
                    }

                    
                }
                    
            }
            else
            {
                node.GetLogger().Info("Failed to load game tables from app setting");
            }

            var tables = GetGameTables();
            if (tables.Count <= 0)
            {
                node.GetLogger().Info("No game table created from app setting");
            }
            else
            {
                node.GetLogger().Info(tables.Count + " game table(s) created from app setting");
                foreach (var gameTable in tables)
                {
                    node.GetLogger().Info("--------------------------------------");
                    node.GetLogger().Info("Table Code: " + gameTable.TableCode);
                    node.GetLogger().Info("Betting Time: " + gameTable.TimeToBet);
                    node.GetLogger().Info("Test Merchant Code: " + gameTable.TestMerchant);
                    await gameTable.Open();
                }
                node.GetLogger().Info("--------------------------------------");
            }

            await Task.Delay(100);

            node.GetLogger().Info(this.GetType().Name + " started");

            return "";
        }

        [Access(Name = "on-unload", IsLocal = true)]
        public async Task<string> Unload(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            await Task.Delay(100);
            var tables = GetGameTables();
            if (tables.Count > 0)
            {
                node.GetLogger().Info("Closing " + tables.Count + " game table(s)...");
                foreach (var gameTable in tables)
                {
                    await gameTable.Close();
                }
                node.GetLogger().Info("Done");
            }
            lock (m_GameTables)
            {
                m_GameTables.Clear();
            }
            await Task.Delay(100);

            return "";
        }

        [Access(Name = "find-game-table")]
        public async Task FindGameTable(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

            string tableCode = ctx.Data.ToString();
            if (tableCode.Trim().Length <= 0)
            {
                await ctx.Session.Send("Table code is invalid");
                return;
            }

            var gameTable = FindGameTable(tableCode);

            if (gameTable != null) await ctx.Session.Send("ok");
            else await ctx.Session.Send("Table code not found");

        }

        [Access(Name = "join-game-table")]
        public async Task JoinGameTable(RequestContext ctx)
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

            ctx.Logger.Info("Join Game Table - " + reqstr);

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string tableCode = req.table_code;

            var gameTable = FindGameTable(tableCode);

            if (gameTable == null)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -2,
                    error_message = "Table not found"
                }));
                return;
            }

            string playerId = req.player_id;
            string merchantCode = req.merchant_code;
            string currencyCode = req.currency_code;
            
            string frontEnd = req.front_end;

            if (gameTable.JoinTable(merchantCode, currencyCode, playerId, frontEnd))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = 0,
                    error_message = "ok",
                    table_code = tableCode
                }));

                if (gameTable.GetGameState() == "PlayingTime")
                {
                    await Task.Delay(500);
                    await gameTable.ResendGameStart(merchantCode, currencyCode, playerId);
                }
            }
            else
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -5,
                    error_message = "Failed to take seat"
                }));
            }

        }

        [Access(Name = "leave-game-table")]
        public async Task LeaveGameTable(RequestContext ctx)
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

            ctx.Logger.Info("Leave Game Table - " + reqstr);

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string tableCode = req.table_code;

            var gameTable = FindGameTable(tableCode);

            if (gameTable == null)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -2,
                    error_message = "Table not found"
                }));
                return;
            }

            string playerId = req.player_id;
            string merchantCode = req.merchant_code;
            string currencyCode = req.currency_code;

            string frontEnd = req.front_end;

            var errmsg = await gameTable.LeaveTable(merchantCode, currencyCode, playerId);

            if (errmsg == "ok")
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = 0,
                    error_message = "ok",
                    table_code = tableCode
                }));
            }
            else
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -5,
                    error_message = "Failed to leave table: " + errmsg
                }));
            }

        }

        [Access(Name = "place-game-bet")]
        public async Task PlaceGameBet(RequestContext ctx)
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

            ctx.Logger.Info("Place Game Bet - " + reqstr);

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string tableCode = req.table_code;

            var gameTable = FindGameTable(tableCode);

            if (gameTable == null)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -2,
                    error_message = "Table not found"
                }));
                return;
            }

            string playerId = req.player_id;
            string merchantCode = req.merchant_code;
            string currencyCode = req.currency_code;

            string clientId = req.client_id;
            string sessionId = req.session_id;
            string frontEnd = req.front_end;

            dynamic ret = await gameTable.PlaceBet(merchantCode, currencyCode, playerId, frontEnd, clientId, sessionId);
            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(ret));

        }

        [Access(Name = "chat")]
        public async Task Chat(RequestContext ctx)
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

            ctx.Logger.Info("Chat Message - " + reqstr);

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string tableCode = req.table_code;

            var gameTable = FindGameTable(tableCode);

            if (gameTable == null)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -2,
                    error_message = "Table not found"
                }));
                return;
            }

            string playerId = req.player_id;
            string merchantCode = req.merchant_code;
            string currencyCode = req.currency_code;

            string chatMsg = req.message;

            await gameTable.Chat(merchantCode, currencyCode, playerId, chatMsg);

            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                error_code = 0,
                error_message = "ok"
            }));

        }

        [Access(Name = "play-game")]
        public async Task PlayGame(RequestContext ctx)
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

            ctx.Logger.Info("Play Game - " + reqstr);

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string tableCode = req.table_code;

            var gameTable = FindGameTable(tableCode);

            if (gameTable == null)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -2,
                    error_message = "Table not found"
                }));
                return;
            }

            string playerId = req.player_id;
            string merchantCode = req.merchant_code;
            string currencyCode = req.currency_code;

            string gameInput = req.game_input;

            List<int> cardIndexes = null;
            try
            {
                cardIndexes = gameInput == null ? null : new List<int>();
                if (cardIndexes != null && gameInput.Length > 0)
                {
                    var cards = gameInput.Split(',');
                    foreach (var card in cards)
                    {
                        cardIndexes.Add(Convert.ToInt32(card.Trim()));
                    }
                }
                
            }
            catch
            {
                if (cardIndexes != null) cardIndexes.Clear();
                cardIndexes = null;
            }

            if (cardIndexes == null)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -5,
                    error_message = "Invalid input"
                }));
                return;
            }

            var result = gameTable.AcceptPlay(merchantCode, currencyCode, playerId, cardIndexes);

            if (result == null || result.okay == false)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    result = result,
                    error_code = -5,
                    error_message = "Invalid input"
                }));
            }
            else
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    result = result,
                    error_code = 0,
                    error_message = "ok"
                }));
            }

        }
    }
}
