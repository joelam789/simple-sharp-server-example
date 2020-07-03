using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.TableGame.Service
{
    public class TurnBasedGameTable
    {
        public enum GAME_STATUS
        {
            Unknown = 0,
            NotWorking,       // 1
            GetGameReady,     // 2 
            StartNewRound,    // 3
            BettingTime,      // 4
            PlayingTime,      // 5
            EndCurrentRound,  // 6 
            OutputGameResult  // 7
        };

        private List<string> m_GameStatus = new List<string>
        {
            "Unknown",
            "NotWorking",
            "GetGameReady",
            "StartNewRound",
            "BettingTime",
            "PlayingTime",
            "EndCurrentRound",
            "OutputGameResult"
        };

        static CommonRng m_Rng = new CommonRng();

        private Timer m_Timer = null;

        private IServerNode m_Node = null;
        private IServerLogger m_Logger = null;

        private GAME_STATUS m_GameState = GAME_STATUS.Unknown;
        //private int m_StateStage = 0;

        private int m_GameReadyCountdown = -1;
        private int m_BettingTimeCountdown = -1;
        private int m_PlayerTurnCountdown = -1;

        private bool m_IsCountdownActive = true;

        private int m_RuntimeErrors = 0;
        private bool m_IsServerReady = false;
        private bool m_IsServerWorking = false;
        private bool m_IsRunningGameLoop = false;

        private string m_ShoeCode = "";
        private int m_RoundIndex = 0;

        private string m_BaseBetCode = "";
        private int m_BetIndex = 0;

        private DateTime m_RoundStartTime = DateTime.MinValue;
        private DateTime m_RoundUpdateTime = DateTime.MinValue;

        private string m_CurrentGameResult = "";
        private Dictionary<string, int> m_CurrentGamePlayerScore = new Dictionary<string, int>();
        private Dictionary<string, decimal> m_CurrentGamePlayerBet = new Dictionary<string, decimal>();
        private List<string> m_FinalCards = new List<string>();

        //private Queue<string> m_History = new Queue<string>();
        //private string m_CurrentGameRemark = "";

        private string m_MainCache = "MainCache";

        private ITurnBasedGameLogic m_GameLogic = null;

        private List<GamePlayer> m_Players = new List<GamePlayer>();

        public int TimeToBet { get; set; } = 30;
        public int TimeToPrepare { get; set; } = 10;
        public int TimeForPlayerTurn { get; set; } = 15;

        public static readonly int MAX_HIST_LENGTH = 10;

        public int TableType { get; set; } = 1;
        public string TableCode { get; set; } = "b0";
        public string TableName { get; set; } = "b0";

        public string TestMerchant { get; set; } = "";

        public TurnBasedGameTable(IServerNode node, ITurnBasedGameLogic gameLogic)
        {
            m_Node = node;
            m_GameLogic = gameLogic;

            m_Logger = m_Node.GetLogger();

            m_GameState = GAME_STATUS.Unknown;

            m_RuntimeErrors = 0;
            m_IsServerReady = false;
            m_IsServerWorking = false;
            m_IsRunningGameLoop = false;

            m_IsCountdownActive = true;

            m_ShoeCode = "";
            m_RoundIndex = 0;
            //m_StateStage = 0;

            m_Players.Clear();

            if (String.IsNullOrEmpty(m_BaseBetCode))
                m_BaseBetCode = Guid.NewGuid().ToString();
        }

        public string GetGameId()
        {
            return m_Node.GetName() + "-" + m_ShoeCode + "-" + m_RoundIndex;
        }

        private string GetBetId()
        {
            if (String.IsNullOrEmpty(m_BaseBetCode))
                m_BaseBetCode = Guid.NewGuid().ToString();

            Interlocked.Increment(ref m_BetIndex);

            return m_BaseBetCode + "-" + m_BetIndex;
        }

        private int GetCurrentCountdown()
        {
            int result = -1;
            switch (m_GameState)
            {
                case (GAME_STATUS.Unknown): break;
                case (GAME_STATUS.NotWorking): break;
                case (GAME_STATUS.GetGameReady):
                    result = m_GameReadyCountdown;
                    break;
                case (GAME_STATUS.StartNewRound):
                    break;
                case (GAME_STATUS.BettingTime):
                    result = m_BettingTimeCountdown;
                    break;
                case (GAME_STATUS.PlayingTime):
                    result = m_PlayerTurnCountdown;
                    break;
                case (GAME_STATUS.EndCurrentRound):
                    break;
                case (GAME_STATUS.OutputGameResult):
                    break;
            }
            return result;
        }

        public string GetCurrentOutputString(bool detailed = false)
        {
            var outputData = GetCurrentOutput(detailed);
            if (outputData == null) return "";
            else return m_Node.GetJsonHelper().ToJsonString(outputData);
        }

        public dynamic GetCurrentOutput(bool detailed = false)
        {
            if (m_GameState < GAME_STATUS.BettingTime) return null;
            else if (m_GameState == GAME_STATUS.BettingTime)
            {
                var playerNames = new List<string>();
                var playerScores = new List<decimal>();
                lock (m_Players)
                {
                    foreach (var player in m_Players)
                    {
                        playerNames.Add(player.GetShortId());
                        playerScores.Add(player.GamePoints);
                    }
                }
                var playerData = new { players = playerNames, scores = playerScores };
                return playerData;
            }
            else
            {
                var gamedata = m_GameLogic.GetCurrentGameData(detailed);
                if (gamedata == null) return null;

                List<string> players = gamedata.players;
                List<int> scores = gamedata.scores;
                List<string> cards = gamedata.cards;

                List<string> finalPlayers = new List<string>();
                List<int> finalScores = new List<int>();
                List<string> finalCards = new List<string>();

                lock (m_Players)
                {
                    foreach (var player in m_Players)
                    {
                        var playerId = player.GetShortId();
                        finalPlayers.Add(playerId);
                        var idx = players.IndexOf(playerId);
                        if (idx >= 0)
                        {
                            finalScores.Add(scores[idx]);
                            finalCards.Add(cards[idx]);
                        }
                        else
                        {
                            finalScores.Add(0);
                            finalCards.Add(detailed ? "" : "-1");
                        }
                    }
                }

                gamedata.players.Clear();
                gamedata.players.AddRange(finalPlayers);

                gamedata.scores.Clear();
                gamedata.scores.AddRange(finalScores);

                gamedata.cards.Clear();
                gamedata.cards.AddRange(finalCards);

                return gamedata;
            }
        }

        public decimal BetToCoins(decimal credit, decimal cpc = 1.0m, decimal bpl = 1.0m)
        {
            //var cpc = 1.0m; // coins per credit
            //var bpl = 1.0m; // bets per line item
            return credit * cpc * bpl;
        }

        public decimal CoinsToPoints(decimal coins, decimal cpc = 1.0m, decimal bpl = 1.0m)
        {
            //var cpc = 1.0m; // coins per credit
            //var bpl = 1.0m; // bets per line item
            return coins / cpc / bpl;
        }

        public GamePlayer FindGamePlayer(string shortId)
        {
            var parts = shortId.Split('|');
            if (parts == null || parts.Length < 3) return null;

            string merchantCode = parts[0];
            string currencyCode = parts[1];
            string playerId = parts[2];

            GamePlayer foundPlayer = null;

            lock (m_Players)
            {
                foreach (var player in m_Players)
                {
                    if (player.PlayerId == playerId
                        && player.MerchantCode == merchantCode
                        && player.CurrencyCode == currencyCode)
                    {
                        foundPlayer = player;
                        break;
                    }
                }
            }

            return foundPlayer;
        }

        public dynamic AcceptPlay(string merchantCode, string currencyCode, string playerId, List<int> play)
        {
            lock (m_GameLogic)
            {
                var errorResult = new
                {
                    okay = false,
                    cards = ""
                };

                if (m_GameState != GAME_STATUS.PlayingTime
                    || m_GameLogic.GetCurrentGameStatus() != "PlayingCards") return errorResult;

                m_IsCountdownActive = false;

                var input = new
                {
                    player = GamePlayer.GetShortId(merchantCode, currencyCode, playerId),
                    cards = play
                };

                var result = m_GameLogic.AcceptPlay(input);

                if (result != null && result.okay == true)
                {
                    ResetCommonTurnCountdown();
                }

                m_IsCountdownActive = true;

                return result;

            }

        }

        public async Task<dynamic> PlaceBet(string merchantCode, string currencyCode, string playerId, 
                                            string frontEnd, string clientId, string sessionId)
        {
            //System.Diagnostics.Debugger.Break();

            //string replyMsgType = "place_bet_reply";
            decimal playerBalance = -1;
            int replyErrorCode = 0;
            string replyErroMsg = "";

            var cpc = 1.0m; // coins per credit
            var bpl = 1.0m; // bets per line item

            GamePlayer foundPlayer = null;

            lock (m_Players)
            {
                if (m_GameState == GAME_STATUS.BettingTime)
                {
                    foreach (var player in m_Players)
                    {
                        if (player.PlayerId == playerId
                            && player.MerchantCode == merchantCode
                            && player.CurrencyCode == currencyCode)
                        {
                            foundPlayer = player;
                            break;
                        }
                    }
                }
            }

            if (foundPlayer == null) return new
            {
                //msg = replyMsgType,
                player_balance = playerBalance,
                error_code = -1,
                error_message = "Player not found"
            };

            string betUuid = "";
            decimal betPoints = 0;
            decimal betAmount = 0;

            lock (foundPlayer)
            {
                if (foundPlayer.BetIds.Count >= m_GameLogic.GetMaxBetCount())
                    return new
                    {
                        //msg = replyMsgType,
                        player_balance = playerBalance,
                        error_code = -2,
                        error_message = "Exceed max bet count"
                    };
                else
                {
                    betUuid = GetBetId();
                    foundPlayer.BetIds.Add(betUuid);

                    betPoints = m_GameLogic.GetGamePointBaseLine();
                    foundPlayer.GamePoints += betPoints;

                    betAmount = BetToCoins(betPoints, cpc, bpl);
                    foundPlayer.BetAmounts.Add(betAmount);
                }
            }

            m_Logger.Info("Saving bet record to database...");

            var saveReq = new
            {
                bet_id = betUuid,

                server_code = m_Node.GetName(),
                table_code = TableCode,
                shoe_code = m_ShoeCode,
                round_number = m_RoundIndex,
                client_id = clientId,
                front_end = frontEnd,

                session_id = sessionId,

                merchant_code = merchantCode,
                currency_code = currencyCode,
                player_id = playerId,

                bet_pool = 1,
                bet_amount = betAmount,

                bet_type = 1,

                bet_cpc = cpc,
                bet_bpl = bpl,

                bet_lines = 1,
                bet_input = ""

            };

            string betTime = "";
            string betGuid = "";
            string retStr = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                         "bet-data", "save-record", m_Node.GetJsonHelper().ToJsonString(saveReq));

            if (retStr.Contains("{") && retStr.Contains("-"))
            {
                dynamic ret = m_Node.GetJsonHelper().ToJsonObject(retStr);
                betGuid = ret.bet_uuid;
                betTime = ret.bet_time;
                m_Logger.Info("Update database successfully");
            }
            else
            {
                m_Logger.Error("Failed to save bet data in database");
            }

            if (betGuid.Length > 0 && betTime.Length > 0)
            {
                // call single wallet

                m_Logger.Info("Call single wallet...");

                var swReq = new
                {
                    bet_uuid = betGuid,
                    table_code = saveReq.table_code,
                    shoe_code = saveReq.shoe_code,
                    round_number = saveReq.round_number,
                    bet_pool = saveReq.bet_pool,
                    merchant_code = saveReq.merchant_code,
                    currency_code = saveReq.currency_code,
                    player_id = saveReq.player_id,
                    client_id = clientId,
                    session_id = sessionId,
                    bet_amount = saveReq.bet_amount,
                    bet_time = betTime
                };

                string swReplyStr = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                         "single-wallet", "debit-for-placing-bet", m_Node.GetJsonHelper().ToJsonString(swReq));

                if (String.IsNullOrEmpty(swReplyStr))
                {
                    replyErrorCode = -5;
                    replyErroMsg = "Failed to call single-wallet service";
                }
                else
                {
                    dynamic ret = m_Node.GetJsonHelper().ToJsonObject(swReplyStr);

                    if (ret.error_code == 0)
                    {
                        playerBalance = ret.player_balance;
                    }
                    else
                    {
                        replyErrorCode = -5;
                        replyErroMsg = "Failed to debit from merchant";
                    }
                }
            }
            else
            {
                replyErrorCode = -4;
                replyErroMsg = "Failed to add it to db";
            }

            if (replyErrorCode >= 0 && playerBalance >= 0)
            {
                var dbhelper = m_Node.GetDataHelper();
                using (var cnn = dbhelper.OpenDatabase(m_MainCache))
                {
                    using (var cmd = cnn.CreateCommand())
                    {
                        dbhelper.AddParam(cmd, "@bet_uuid", betGuid);

                        dbhelper.AddParam(cmd, "@merchant_code", saveReq.merchant_code);
                        dbhelper.AddParam(cmd, "@currency_code", saveReq.currency_code);
                        dbhelper.AddParam(cmd, "@player_id", saveReq.player_id);

                        dbhelper.AddParam(cmd, "@server_code", saveReq.server_code);
                        dbhelper.AddParam(cmd, "@table_code", saveReq.table_code);
                        dbhelper.AddParam(cmd, "@shoe_code", saveReq.shoe_code);
                        dbhelper.AddParam(cmd, "@round_number", saveReq.round_number);
                        dbhelper.AddParam(cmd, "@client_id", saveReq.client_id);
                        dbhelper.AddParam(cmd, "@front_end", saveReq.front_end);
                        dbhelper.AddParam(cmd, "@bet_pool", saveReq.bet_pool);
                        dbhelper.AddParam(cmd, "@bet_amount", saveReq.bet_amount);

                        dbhelper.AddParam(cmd, "@session_id", saveReq.session_id);

                        dbhelper.AddParam(cmd, "@bet_type", saveReq.bet_type);
                        dbhelper.AddParam(cmd, "@coins_per_credit", saveReq.bet_cpc);
                        dbhelper.AddParam(cmd, "@bets_per_line", saveReq.bet_bpl);
                        dbhelper.AddParam(cmd, "@betted_lines", saveReq.bet_lines);
                        dbhelper.AddParam(cmd, "@game_input", saveReq.bet_input);

                        cmd.CommandText = " insert into tbl_bet_record "
                                        + " ( bet_uuid, merchant_code, currency_code, player_id, server_code, table_code, shoe_code, round_number, client_id, front_end, session_id, "
                                        + "   bet_pool, bet_amount, bet_type, coins_per_credit, bets_per_line, betted_lines, game_input, bet_time ) values "
                                        + " ( @bet_uuid, @merchant_code, @currency_code, @player_id, @server_code , @table_code , @shoe_code , @round_number , @client_id , @front_end , @session_id , "
                                        + "   @bet_pool, @bet_amount , @bet_type, @coins_per_credit, @bets_per_line, @betted_lines, @game_input, CURRENT_TIMESTAMP ) "
                                        ;

                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            replyErrorCode = 3;
                            replyErroMsg = "Added to cache";
                            m_Logger.Info("Added bet to cache: " + betGuid);
                        }
                        else
                        {
                            replyErrorCode = -3;
                            replyErroMsg = "Failed to add it to cache";
                            m_Logger.Error("Failed to add bet to cache: " + betGuid);
                        }
                    }
                }
            }
            else
            {
                lock (foundPlayer)
                {
                    //foundPlayer.GamePoints = 0;

                    if (!string.IsNullOrEmpty(betUuid))
                    {
                        var betIdx = foundPlayer.BetIds.IndexOf(betUuid);
                        if (betIdx >= 0)
                        {
                            foundPlayer.BetIds.RemoveAt(betIdx);
                            foundPlayer.BetAmounts.RemoveAt(betIdx);
                            foundPlayer.GamePoints -= betPoints;
                        }
                    }
                }
            }

            if (replyErrorCode >= 0)
            {
                return new
                {
                    //msg = replyMsgType,
                    bet_id = betGuid,
                    player_balance = playerBalance,
                    error_code = 0,
                    error_message = "ok"
                };
            }
            else return new
            {
                //msg = replyMsgType,
                player_balance = playerBalance,
                error_code = replyErrorCode,
                error_message = replyErroMsg
            };
        }

        public bool JoinTable(string merchantCode, string currencyCode, string playerId, string frontEnd)
        {
            lock (m_Players)
            {
                if (m_GameState == GAME_STATUS.BettingTime 
                    || m_GameState == GAME_STATUS.PlayingTime)
                {
                    GamePlayer foundPlayer = null;
                    foreach (var player in m_Players)
                    {
                        if (player.PlayerId == playerId
                            && player.MerchantCode == merchantCode
                            && player.CurrencyCode == currencyCode)
                        {
                            foundPlayer = player;
                            break;
                        }
                    }

                    if (foundPlayer == null)
                    {
                        if (m_GameState == GAME_STATUS.BettingTime)
                        {
                            if (m_Players.Count < m_GameLogic.GetMaxPlayerCount())
                            {
                                foundPlayer = new GamePlayer(merchantCode, currencyCode, playerId);
                                m_Players.Add(foundPlayer);
                            }
                        }
                    }

                    if (foundPlayer != null)
                    {
                        foundPlayer.FrontEndServer = frontEnd;
                        return true;
                    }
                    
                }
            }

            return false;
        }

        public async Task<string> LeaveTable(string merchantCode, string currencyCode, string playerId)
        {
            string errmsg = "ok";
            GamePlayer foundPlayer = null;

            lock (m_Players)
            {
                if (m_GameState == GAME_STATUS.BettingTime)
                {
                    foreach (var player in m_Players)
                    {
                        if (player.PlayerId == playerId
                            && player.MerchantCode == merchantCode
                            && player.CurrencyCode == currencyCode)
                        {
                            foundPlayer = player;
                            break;
                        }
                    }

                    if (foundPlayer != null)
                    {
                        m_Players.Remove(foundPlayer);
                    }

                }
            }

            if (foundPlayer != null)
            {
                int errCount = 0;
                foreach (var betId in foundPlayer.BetIds)
                {
                    var cancelDebitReq = new
                    {
                        bet_uuid = betId,
                        merchant_code = foundPlayer.MerchantCode
                    };

                    string cancelDebitReplyStr = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                    "transaction-data", "cancel-bet-debit", m_Node.GetJsonHelper().ToJsonString(cancelDebitReq));

                    if (string.IsNullOrEmpty(cancelDebitReplyStr))
                    {
                        errCount++;
                        m_Logger.Error("Failed to cancel bet debit when leaving table - " + betId);
                    }
                }
                if (errCount > 0)
                {
                    errmsg = "Failed to cancel some bets";
                }

            }
            else errmsg = "Player not found";

            return errmsg;
        }

        private int GetPlayerCount()
        {
            lock (m_Players)
            {
                return m_Players.Count;
            }
        }

        private int GetGameType()
        {
            return m_GameLogic.GetGameType();
        }

        public bool IsCountdownActive()
        {
            return m_IsCountdownActive;
        }
        public void SetCountdownActive(bool value)
        {
            m_IsCountdownActive = value;
        }

        public int ResetCommonTurnCountdown(int countdown = -1)
        {
            if (countdown >= 0) m_PlayerTurnCountdown = countdown;
            else m_PlayerTurnCountdown = TimeForPlayerTurn;

            return m_PlayerTurnCountdown;
        }

        private dynamic Snapshot()
        {
            var currentGameState = new
            {
                server = m_Node.GetName(),
                table = TableCode,
                label = TableName,
                shoe = m_ShoeCode,
                round = m_RoundIndex,
                game = GetGameType(),
                state = (int)m_GameState,
                status = m_GameStatus[(int)m_GameState],
                players = GetPlayerCount(),
                countdown = GetCurrentCountdown(),
                starttime = m_RoundStartTime,
                updatetime = m_RoundUpdateTime,
                //history = String.Join(";", m_History.ToArray()),
                remark = GetCurrentOutputString(true),
                output = GetCurrentOutputString(false),
                result = m_CurrentGameResult
            };

            return currentGameState;
        }

        public string GetGameState()
        {
            return m_GameStatus[(int)m_GameState];
        }

        public async Task Open(string tableName = "")
        {
            await Close();

            m_RuntimeErrors = 0;
            m_IsServerReady = false;
            m_IsServerWorking = false;

            m_RoundIndex = 0;
            //m_GameCode = Guid.NewGuid().ToString();
            m_ShoeCode = DateTime.Now.ToString("yyyyMMddHHmmss");
            //m_History.Clear();
            m_GameReadyCountdown = TimeToPrepare;
            m_GameState = GAME_STATUS.GetGameReady;
            //m_StateStage = 0;
            m_CurrentGameResult = "";
            //m_CurrentGameRemark = "";
            m_CurrentGamePlayerBet.Clear();
            m_CurrentGamePlayerScore.Clear();
            m_IsRunningGameLoop = false;

            if (!string.IsNullOrEmpty(tableName))
                TableName = tableName;

            m_IsServerWorking = true;
            m_Timer = new Timer(Tick, m_Rng, 500, 1000 * 1);
        }

        public async Task Close()
        {
            m_IsServerWorking = false;
            m_GameState = GAME_STATUS.NotWorking;
            //m_StateStage = -1;

            if (m_Timer != null)
            {
                await Task.Delay(500);
                m_Timer.Dispose();
                m_Timer = null;
            }

            m_GameReadyCountdown = -1;
            m_BettingTimeCountdown = -1;
            m_PlayerTurnCountdown = -1;

            m_IsRunningGameLoop = false;
            m_IsServerReady = false;
            m_RuntimeErrors = 0;
        }

        private void UpdateRoundState(GAME_STATUS nextState)
        {
            if (m_GameState != GAME_STATUS.Unknown && m_GameState != GAME_STATUS.NotWorking)
            {
                m_RoundUpdateTime = DateTime.Now;
                dynamic currentRoundState = Snapshot();

                if (currentRoundState != null)
                {
                    try
                    {
                        CacheSnapshot(currentRoundState);
                    }
                    catch (Exception ex)
                    {
                        m_RuntimeErrors++;
                        m_Logger.Error("Faild to cache snapshot: " + ex.ToString());
                        m_Logger.Error(ex.StackTrace);
                    }

                }

                if ((m_GameState != nextState)
                    && (m_GameState == GAME_STATUS.BettingTime || nextState == GAME_STATUS.BettingTime))
                {
                    lock (m_Players)
                    {
                        m_GameState = nextState;
                    }
                }
                else
                {
                    m_GameState = nextState;
                }

                m_Logger.Info("CurrentRoundState - [" + currentRoundState.status + "]");
            }
        }

        private void CacheSnapshot(dynamic snapshot)
        {
            var dbhelper = m_Node.GetDataHelper();
            using (var cnn = dbhelper.OpenDatabase(m_MainCache))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@game_type", snapshot.game);
                    dbhelper.AddParam(cmd, "@server_code", snapshot.server);
                    dbhelper.AddParam(cmd, "@table_code", snapshot.table);
                    dbhelper.AddParam(cmd, "@table_name", snapshot.label);
                    dbhelper.AddParam(cmd, "@shoe_code", snapshot.shoe);
                    dbhelper.AddParam(cmd, "@round_number", snapshot.round);
                    dbhelper.AddParam(cmd, "@round_state", snapshot.state);
                    dbhelper.AddParam(cmd, "@player_count", snapshot.players);
                    dbhelper.AddParam(cmd, "@round_state_text", snapshot.status);
                    dbhelper.AddParam(cmd, "@current_countdown", snapshot.countdown);
                    dbhelper.AddParam(cmd, "@game_output", snapshot.output);
                    dbhelper.AddParam(cmd, "@game_result", snapshot.result);
                    //dbhelper.AddParam(cmd, "@game_history", snapshot.history);
                    dbhelper.AddParam(cmd, "@game_remark", snapshot.remark);
                    dbhelper.AddParam(cmd, "@round_start_time", snapshot.starttime);
                    dbhelper.AddParam(cmd, "@round_update_time", snapshot.updatetime);

                    switch (m_GameState)
                    {
                        case (GAME_STATUS.Unknown): break;
                        case (GAME_STATUS.NotWorking): break;
                        case (GAME_STATUS.GetGameReady):
                            cmd.CommandText = "update tbl_round_state "
                                            + " set bet_time_countdown = -1 "
                                            + " , gaming_countdown = -1 "
                                            + " , next_game_countdown = @current_countdown "
                                            + " , game_type = @game_type "
                                            + " , player_count = @player_count "
                                            + " , round_update_time = @round_update_time "
                                            + " where server_code = @server_code and table_code = @table_code "
                                            + " and shoe_code = @shoe_code and round_number = @round_number "
                                            ;
                            cmd.ExecuteNonQuery();
                            break;
                        case (GAME_STATUS.StartNewRound):
                            cmd.CommandText = "update tbl_round_state "
                                            + " set backup_number = backup_number + 1 , init_flag = state_id "
                                            + " where server_code = @server_code and table_code = @table_code ; ";
                            cmd.CommandText = cmd.CommandText + "delete from tbl_round_state "
                                            + " where server_code = @server_code and table_code = @table_code and backup_number > 3 ; ";
                            cmd.CommandText = cmd.CommandText + " insert into tbl_round_state "
                                            + " ( server_code, table_code, table_name, shoe_code, round_number, round_state, round_state_text, bet_time_countdown, "
                                            + "   game_output, game_result, game_remark, init_flag, game_type, player_count, round_start_time, round_update_time ) values "
                                            + " ( @server_code , @table_code , @table_name , @shoe_code , @round_number , @round_state , @round_state_text , @current_countdown , "
                                            + "   '' , '', '', 0, @game_type, @player_count, @round_start_time, @round_update_time ) "
                                            ;

                            cmd.ExecuteNonQuery();
                            break;
                        case (GAME_STATUS.BettingTime):
                            cmd.CommandText = "update tbl_round_state "
                                            + " set round_state = @round_state "
                                            + " , round_state_text = @round_state_text "
                                            + " , bet_time_countdown = @current_countdown "
                                            + " , game_type = @game_type "
                                            + " , table_name = @table_name "
                                            + " , player_count = @player_count "
                                            + " , game_output = @game_output "
                                            + " , round_update_time = @round_update_time "
                                            + " where server_code = @server_code and table_code = @table_code "
                                            + " and shoe_code = @shoe_code and round_number = @round_number "
                                            ;
                            cmd.ExecuteNonQuery();
                            break;
                        case (GAME_STATUS.PlayingTime):
                        case (GAME_STATUS.EndCurrentRound):
                        case (GAME_STATUS.OutputGameResult):
                            cmd.CommandText = "update tbl_round_state "
                                            + " set round_state = @round_state "
                                            + " , round_state_text = @round_state_text "
                                            + " , gaming_countdown = @current_countdown "
                                            + " , game_output = @game_output "
                                            + " , game_result = @game_result "
                                            + " , game_remark = @game_remark "
                                            + " , game_type = @game_type "
                                            + " , player_count = @player_count "
                                            + " , round_update_time = @round_update_time "
                                            + " where server_code = @server_code and table_code = @table_code "
                                            + " and shoe_code = @shoe_code and round_number = @round_number "
                                            ;
                            cmd.ExecuteNonQuery();
                            break;
                    }
                }

            }
        }

        public async Task GameLoop()
        {
            switch (m_GameState)
            {
                case (GAME_STATUS.Unknown): break;
                case (GAME_STATUS.NotWorking): break;
                case (GAME_STATUS.GetGameReady):
                    await GetGameReady();
                    break;
                case (GAME_STATUS.StartNewRound):
                    await StartNewRound();
                    break;
                case (GAME_STATUS.BettingTime):
                    await ProcessBettingTime();
                    break;
                case (GAME_STATUS.PlayingTime):
                    var ret = "";
                    lock (m_GameLogic)
                    {
                        ret = ProcessPlayingTime();
                    }
                    if (!string.IsNullOrEmpty(ret))
                        await RemoteCaller.BroadcastCall(m_Node.GetRemoteServices(), "fes-client", "send-player-msg", ret);
                    break;
                case (GAME_STATUS.EndCurrentRound):
                    EndRound();
                    break;
                case (GAME_STATUS.OutputGameResult):
                    await OutputGameResult();
                    break;
            }

        }

        private async void Tick(object param)
        {
            if (m_RuntimeErrors > 0) return;
            if (!m_IsServerWorking) return;

            if (m_IsRunningGameLoop) return;
            m_IsRunningGameLoop = true;
            try
            {
                if (!m_IsServerReady)
                {
                    await Task.Delay(2000);

                    m_Logger.Info("Checking basic services...");

                    await Task.Delay(1000);

                    await GetServerReady();

                    if (!m_IsServerReady)
                    {
                        await Task.Delay(5000); // just take it easy
                    }
                    else
                    {
                        m_Logger.Info("Try to clean up last (incomplete) game...");
                        await CleanUpLastIncompleteGame();
                        m_Logger.Info("Done");
                    }
                }
                else
                {
                    await GameLoop();
                }
            }
            catch (Exception ex)
            {
                m_RuntimeErrors++;
                m_Logger.Error(ex.ToString());
                m_Logger.Error(ex.StackTrace);
            }
            finally
            {
                m_IsRunningGameLoop = false;
            }

        }

        public async Task GetServerReady()
        {
            if (m_IsServerReady) return;

            await Task.Delay(100);

            var foundError = false;

            m_Logger.Info("Check internal online servers...");
            try
            {
                var services = m_Node.GetRemoteServices();

                if (!services.ContainsKey("game-data")
                    || !services.ContainsKey("bet-data")
                    || !services.ContainsKey("merchant-data")
                    || !services.ContainsKey("transaction-data"))
                {
                    foundError = true;
                    m_Logger.Error("Data Access Server not found");
                }

                if (!services.ContainsKey("single-wallet"))
                {
                    foundError = true;
                    m_Logger.Error("Single Wallet Server not found");
                }

                await Task.Delay(100);
                m_Logger.Info("Done");
            }
            catch (Exception ex)
            {
                foundError = true;
                m_Logger.Error("Failed to get online internal servers: ");
                m_Logger.Error(ex.ToString());
            }

            if (foundError) return;

            m_Logger.Info("Check cache server and clean up...");
            try
            {
                var dbhelper = m_Node.GetDataHelper();
                using (var cnn = dbhelper.OpenDatabase(m_MainCache))
                {
                    using (var cmd = cnn.CreateCommand())
                    {
                        dbhelper.AddParam(cmd, "@server_code", m_Node.GetName());
                        dbhelper.AddParam(cmd, "@table_code", TableCode);

                        cmd.CommandText = " delete from tbl_round_state "
                                                + " where server_code = @server_code ; ";
                        cmd.CommandText = cmd.CommandText + " delete from tbl_round_state "
                                                + " where table_code = @table_code ; ";

                        cmd.ExecuteNonQuery();
                    }
                }

                await Task.Delay(100);
                m_Logger.Info("Done");
            }
            catch (Exception ex)
            {
                foundError = true;
                m_Logger.Error("Failed to clean up old cache: ");
                m_Logger.Error(ex.ToString());
            }

            if (foundError) return;

            if (TestMerchant.Length > 0)
            {
                var defaultCurrency = "CNY";
                var merchantCode = String.Copy(TestMerchant);
                var merchantKey = merchantCode + defaultCurrency;

                m_Logger.Info("Check DAL with test merchant code - " + merchantCode + "|" + defaultCurrency);

                try
                {
                    string merchantInfo = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                                        "merchant-data", "get-merchant-info", merchantKey);

                    if (String.IsNullOrEmpty(merchantInfo) || !merchantInfo.Contains('{') || !merchantInfo.Contains(':'))
                    {
                        foundError = true;
                        m_Logger.Error("Failed to get merchant info from DAL - " + merchantCode);
                    }
                    else
                    {
                        dynamic merchant = m_Node.GetJsonHelper().ToJsonObject(merchantInfo);
                        string merchantUrl = merchant.url.ToString();

                        m_Logger.Info("URL for Merchant Code " + merchantCode + " - " + merchantUrl);
                    }



                    await Task.Delay(100);
                    m_Logger.Info("Done");
                }
                catch (Exception ex)
                {
                    foundError = true;
                    m_Logger.Error("Failed to get merchant info from DAL - " + merchantCode);
                    m_Logger.Error(ex.ToString());
                }
            }

            if (foundError) return;

            if (!foundError) m_IsServerReady = true;
        }

        public async Task<string> GetGameReady()
        {
            if (m_GameReadyCountdown >= 0)
            {
                UpdateRoundState(GAME_STATUS.GetGameReady);
                m_Logger.Info("Getting ready - " + m_GameReadyCountdown);
                m_GameReadyCountdown--;
            }
            else
            {
                // check game live setting...

                try
                {
                    var checkReq = new
                    {
                        server_code = m_Node.GetName(),
                        table_code = TableCode,
                    };

                    string replystr = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                                        "game-data", "get-table-setting", m_Node.GetJsonHelper().ToJsonString(checkReq));
                    dynamic reply = m_Node.GetJsonHelper().ToJsonObject(replystr);
                    if (reply.error_code == 0)
                    {
                        if (reply.setting.setting_id > 0)
                        {
                            if (reply.setting.is_maintained > 0)
                            {
                                m_Logger.Info("Stay in preparing state because table is under maintenance... ");
                                m_GameReadyCountdown = TimeToPrepare;
                                return "";
                            }
                        }
                    }
                }
                catch { }

                // going to next state ...

                m_GameLogic.GetGameReady();
                m_IsCountdownActive = true;

                m_GameReadyCountdown = -1;
                UpdateRoundState(GAME_STATUS.StartNewRound);
            }
            return "";
        }

        public async Task<string> StartNewRound()
        {
            m_Logger.Info("Start a new round...");

            m_RoundStartTime = DateTime.Now;

            //m_RoundIndex++;
            var shoe = DateTime.Now.ToString("yyyyMMddHHmmss");
            if (m_ShoeCode.CompareTo(shoe) == 0)
            {
                if (m_RoundIndex <= 0) m_RoundIndex = 1;
                else m_RoundIndex++;
            }
            else
            {
                m_ShoeCode = shoe;
                m_RoundIndex = 1;
            }

            m_CurrentGameResult = "";
            //m_CurrentGameRemark = "";

            lock (m_Players)
            {
                foreach (var player in m_Players)
                {
                    var shortId = player.GetShortId();
                    if (m_CurrentGamePlayerScore.ContainsKey(shortId))
                    {
                        lock (player)
                        {
                            player.GamePoints = 0; // reset it...
                            player.ClearBets();
                        }
                    }
                }
            }

            m_FinalCards.Clear();
            m_CurrentGamePlayerBet.Clear();
            m_CurrentGamePlayerScore.Clear();

            var newGameId = GetGameId();
            m_Logger.Info("New round ID: " + newGameId);

            m_Logger.Info("Saving new game record to database...");

            var saveReq = new
            {
                server = m_Node.GetName(),
                table = TableCode,
                shoe = m_ShoeCode,
                round = m_RoundIndex,
                state = (int)m_GameState,
                starttime = m_RoundStartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                updatetime = m_RoundStartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                output = GetCurrentOutputString(),
                result = m_CurrentGameResult
            };
            string replystr = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                "game-data", "save-record", m_Node.GetJsonHelper().ToJsonString(saveReq));

            if (replystr == "ok") m_Logger.Info("Update database successfully");
            else
            {
                m_RuntimeErrors++;
                m_Logger.Error("Failed to save game data to database");
                m_Logger.Error(m_Node.GetJsonHelper().ToJsonString(saveReq));
            }

            m_GameLogic.ResetSeats();

            m_GameReadyCountdown = -1;
            m_BettingTimeCountdown = TimeToBet;
            UpdateRoundState(GAME_STATUS.BettingTime);

            m_Logger.Info("Start betting time...");

            return "";
        }

        public async Task Chat(string merchantCode, string currencyCode, string playerId, string chatMsg)
        {
            var message = chatMsg;
            if (string.IsNullOrEmpty(message)) message = "";
            var players = new List<GamePlayer>();
            lock (m_Players)
            {
                players.AddRange(m_Players);
            }
            foreach (var player in players)
            {
                var msgdata = new
                {
                    msg = "chat_message",
                    player_id = player.PlayerId,
                    merchant_code = player.MerchantCode,
                    currency_code = player.CurrencyCode,
                    message = message
                };

                var req = new
                {
                    player_id = player.PlayerId,
                    merchant_code = player.MerchantCode,
                    currency_code = player.CurrencyCode,
                    data = msgdata
                };

                await RemoteCaller.BroadcastCall(m_Node.GetRemoteServices(), "fes-client", "send-player-msg",
                    m_Node.GetJsonHelper().ToJsonString(req));
            }

        }

        public async Task<string> ProcessBettingTime()
        {
            //m_Logger.Info("Start betting time...");

            if (m_BettingTimeCountdown >= 0)
            {
                UpdateRoundState(GAME_STATUS.BettingTime);
                m_Logger.Info("Betting time - " + m_BettingTimeCountdown);
                m_BettingTimeCountdown--;
            }
            else
            {
                bool canStarGame = false;
                var paidPlayers = new List<GamePlayer>();
                var baseline = m_GameLogic.GetGamePointBaseLine();
                lock (m_Players)
                {
                    foreach (var player in m_Players)
                    {
                        if (player.GamePoints >= baseline)
                        {
                            paidPlayers.Add(player);
                        }
                    }
                    if (paidPlayers.Count >= m_GameLogic.GetMinPlayerCount()
                        && paidPlayers.Count <= m_GameLogic.GetMaxPlayerCount())
                    {
                        m_GameLogic.ResetSeats();
                        foreach (var player in paidPlayers)
                        {
                            m_GameLogic.TakeSeat(player.GetShortId(), Convert.ToInt32(player.GamePoints));
                        }
                        canStarGame = true;
                    }
                }

                if (canStarGame)
                {
                    m_BettingTimeCountdown = -1;
                    m_PlayerTurnCountdown = TimeForPlayerTurn;

                    m_GameLogic.StartNewRound();

                    foreach (var player in paidPlayers)
                    {
                        var msgdata = new
                        {
                            msg = "start_game",
                            player = m_GameLogic.GetCurrentPlayerName(),
                            cards = m_GameLogic.GetPlayerCards(player.GetShortId())
                        };

                        var req = new
                        {
                            player_id = player.PlayerId,
                            merchant_code = player.MerchantCode,
                            currency_code = player.CurrencyCode,
                            data = msgdata
                        };

                        await RemoteCaller.BroadcastCall(m_Node.GetRemoteServices(), "fes-client", "send-player-msg",
                            m_Node.GetJsonHelper().ToJsonString(req));
                    }

                    UpdateRoundState(GAME_STATUS.PlayingTime);
                }
                else
                {
                    // clean up bad connections ...

                    if (m_GameState == GAME_STATUS.BettingTime)
                        await KickOfflinePlayers();

                    // then start next loop ...

                    m_BettingTimeCountdown = TimeToBet;
                    UpdateRoundState(GAME_STATUS.BettingTime);
                }

                
            }
            return "";
        }

        public async Task KickOfflinePlayers()
        {
            var players = new List<GamePlayer>();
            lock (m_Players)
            {
                players.AddRange(m_Players);
            }

            foreach (var player in players)
            {
                //if (player.GamePoints > 0) continue; // ...

                var req = new
                {
                    player_id = player.PlayerId,
                    merchant_code = player.MerchantCode,
                    currency_code = player.CurrencyCode
                };

                var reply = await RemoteCaller.SpecifiedCall(m_Node.GetRemoteServices(),
                                                player.FrontEndServer, "fes-client", "check-online",
                                                m_Node.GetJsonHelper().ToJsonString(req));

                if (!string.IsNullOrEmpty(reply) && reply == "false")
                {
                    await LeaveTable(player.MerchantCode, player.CurrencyCode, player.PlayerId);
                }
            }
        }

        public async Task ResendGameStart(string merchantCode, string currencyCode, string playerId)
        {
            if (m_GameState != GAME_STATUS.PlayingTime) return;

            var msgdata = new
            {
                msg = "start_game",
                player = m_GameLogic.GetCurrentPlayerName(),
                cards = m_GameLogic.GetPlayerCards(GamePlayer.GetShortId(merchantCode, currencyCode, playerId))
            };

            var req = new
            {
                player_id = playerId,
                merchant_code = merchantCode,
                currency_code = currencyCode,
                data = msgdata
            };

            await RemoteCaller.BroadcastCall(m_Node.GetRemoteServices(), "fes-client", "send-player-msg",
                m_Node.GetJsonHelper().ToJsonString(req));
        }

        public string ProcessPlayingTime()
        {
            //m_Logger.Info("Start racing time...");

            if (!m_IsCountdownActive) return "";

            if (m_PlayerTurnCountdown >= 0)
            {
                UpdateRoundState(GAME_STATUS.PlayingTime);
                m_Logger.Info("Player turn time - " + m_GameLogic.GetCurrentPlayerName() + ": " + m_PlayerTurnCountdown);
                m_PlayerTurnCountdown--;
            }
            else
            {
                if (m_GameLogic.IsCurrentRoundDone())
                {
                    m_PlayerTurnCountdown = -1;
                    UpdateRoundState(GAME_STATUS.EndCurrentRound);
                }
                else
                {
                    GamePlayer currentPlayer = null;
                    var currentPlayerName = m_GameLogic.GetCurrentPlayerName();
                    lock (m_Players)
                    {
                        foreach (var player in m_Players)
                        {
                            if (player.GetShortId() == currentPlayerName)
                            {
                                currentPlayer = player;
                                break;
                            }
                        }
                    }
                    var result = currentPlayer == null ? null : m_GameLogic.AutoPlay();
                    if (result != null && result.okay == true)
                    {
                        var msgdata = new
                        {
                            msg = "play_game_reply",
                            result = result,
                            error_code = 0,
                            error_message = "ok"
                        };

                        var req = new
                        {
                            player_id = currentPlayer.PlayerId,
                            merchant_code = currentPlayer.MerchantCode,
                            currency_code = currentPlayer.CurrencyCode,
                            data = msgdata
                        };

                        m_PlayerTurnCountdown = TimeForPlayerTurn;

                        return m_Node.GetJsonHelper().ToJsonString(req);
                    }
                    else
                    {
                        m_RuntimeErrors++;
                        m_Logger.Error("Failed to run auto-play");
                    }
                }
                
            }
            return "";
        }

        public void EndRound()
        {
            m_Logger.Info("End round...");

            //m_CurrentGameRemark = GetCurrentOutput();
            //m_CurrentGameResult = GetCurrentOutput(true);

            m_FinalCards.Clear();
            var finalOutput = GetCurrentOutput(true);
            if (finalOutput != null)
            {
                try
                {
                    foreach (var cards in finalOutput.cards)
                    {
                        string str = cards;
                        m_FinalCards.Add(str);
                    }
                }
                catch (Exception ex)
                {
                    m_Logger.Error("Failed to get final state of cards when end round - " + ex.ToString());
                }
            }
            
            var scores = m_GameLogic.CalculateScores();
            if (scores == null)
            {
                throw new Exception("Failed to calculate scores");
            }
            else
            {
                m_CurrentGamePlayerBet.Clear();
                m_CurrentGamePlayerScore.Clear();
                lock (m_Players)
                {
                    foreach (var player in m_Players)
                    {
                        var shortId = player.GetShortId();
                        if (scores.ContainsKey(shortId))
                        {
                            lock (player)
                            {
                                player.GamePoints = scores[shortId];
                                m_CurrentGamePlayerScore.Add(shortId, scores[shortId]);
                                m_CurrentGamePlayerBet.Add(shortId, player.GetTotalBetAmount());
                            }
                        }
                    }
                }
            }

            //m_History.Enqueue(ranking);
            //if (m_History.Count > MAX_HIST_LENGTH) m_History.Dequeue();

            UpdateRoundState(GAME_STATUS.OutputGameResult);

        }

        public async Task OutputGameResult()
        {
            //if (PlayerPoints > BankerPoints) m_Logger.Info("PLAYER WIN");

            m_Logger.Info("Updating game record in database...");

            var finalResult = GetCurrentOutput(true);
            if (finalResult != null && m_FinalCards.Count > 0)
            {
                try
                {
                    finalResult.cards.Clear();
                    finalResult.cards.AddRange(m_FinalCards);
                }
                catch (Exception ex)
                {
                    m_Logger.Error("Failed to update final state of cards when settling - " + ex.ToString());
                }
            }

            m_CurrentGameResult = finalResult == null ? "" : m_Node.GetJsonHelper().ToJsonString(finalResult);

            var gameResult = m_CurrentGameResult;

            var saveReq = new
            {
                server = m_Node.GetName(),
                table = TableCode,
                shoe = m_ShoeCode,
                round = m_RoundIndex,
                state = (int)m_GameState,
                updatetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                output = GetCurrentOutputString(),
                result = gameResult
            };
            string ret = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                "game-data", "update-result", m_Node.GetJsonHelper().ToJsonString(saveReq));

            if (ret == "ok") m_Logger.Info("Update database successfully");
            else
            {
                m_RuntimeErrors++;
                m_Logger.Error("Failed to update game data in database");
                if (!string.IsNullOrEmpty(ret)) m_Logger.Error(ret);
                m_Logger.Error(m_Node.GetJsonHelper().ToJsonString(saveReq));
            }


            m_GameReadyCountdown = TimeToPrepare;

            UpdateRoundState(GAME_STATUS.GetGameReady);

            //System.Diagnostics.Debugger.Break();

            m_Logger.Info("Settling...");


            // read bet records from cache

            var gameServer = m_Node.GetName();

            m_Logger.Info("CheckBetWinlossByGameResult - " + gameServer);

            List<dynamic> bets = new List<dynamic>();

            var dbhelper = m_Node.GetDataHelper();
            using (var cnn = dbhelper.OpenDatabase(m_MainCache))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@server_code", gameServer);

                    cmd.CommandText = " select a.game_result, a.game_output, b.bet_uuid, b.merchant_code, b.currency_code, b.player_id, b.client_id, b.session_id, "
                                    + " b.bet_pool, b.bet_amount, b.coins_per_credit, b.bets_per_line, b.bet_type, b.game_input "
                                    + " from tbl_round_state a, tbl_bet_record b "
                                    + " where a.round_state = 7 and b.bet_state = 0 "
                                    + " and a.server_code = @server_code "
                                    + " and a.server_code = b.server_code "
                                    + " and a.table_code = b.table_code "
                                    + " and a.shoe_code = b.shoe_code "
                                    + " and a.round_number = b.round_number "
                                    ;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string result = reader["game_result"].ToString();
                            string output = reader["game_output"].ToString();
                            string betGuid = reader["bet_uuid"].ToString();
                            string merchant = reader["merchant_code"].ToString();
                            string currency = reader["currency_code"].ToString();
                            string player = reader["player_id"].ToString();

                            string client = reader["client_id"].ToString();
                            string session = reader["session_id"].ToString();

                            int pool = Convert.ToInt32(reader["bet_pool"].ToString());
                            decimal betAmount = Convert.ToDecimal(reader["bet_amount"].ToString());

                            decimal cpc = Convert.ToDecimal(reader["coins_per_credit"].ToString());
                            int bpl = Convert.ToInt32(Convert.ToDecimal(reader["bets_per_line"].ToString()));
                            int betType = Convert.ToInt32(reader["bet_type"].ToString());
                            string betInput = reader["game_input"].ToString();

                            decimal payAmount = 0;
                            decimal comission = 0;

                            var shortId = GamePlayer.GetShortId(merchant, currency, player);
                            if (m_CurrentGamePlayerScore.ContainsKey(shortId))
                            {
                                payAmount = BetToCoins(m_CurrentGamePlayerScore[shortId], cpc, bpl);
                                decimal totalBetAmount = m_CurrentGamePlayerBet[shortId];

                                var winloss = payAmount - totalBetAmount;
                                if (winloss > 0)
                                {
                                    comission = winloss * m_GameLogic.GetWinCommission();
                                    if (comission > 0) payAmount -= comission;
                                }
                            }
                                

                            dynamic bet = new
                            {
                                bet_uuid = betGuid,
                                pay_amount = payAmount,
                                contribution = comission,
                                game_result = result,
                                game_output = output,
                                bet_pool = pool,
                                merchant_code = merchant,
                                currency_code = currency,
                                player_id = player,
                                client_id = client,
                                session_id = session
                            };

                            bets.Add(bet);
                        }
                    }
                }
            }

            if (bets == null || bets.Count <= 0)
            {
                m_RuntimeErrors++;
                m_Logger.Error("Failed to update bets by game result");
            }
            else
            {
                m_Logger.Info("Update bets in database...");

                var passIds = new Dictionary<string, string>();
                List<Task> dbTasks = new List<Task>();
                foreach (var bet in bets)
                {
                    dbTasks.Add(Task.Run(async () =>
                    {
                        bool okay = false;
                        string uuid = bet.bet_uuid.ToString();
                        string dbReply = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                                                "bet-data", "update-result", m_Node.GetJsonHelper().ToJsonString(bet));
                        //if (dbErr != "ok") errIds.Add(uuid);
                        if (dbReply.Contains('-') && dbReply.Contains('='))
                        {
                            var itemParts = dbReply.Split('='); // bet_uuid = settle_time
                            if (itemParts != null && itemParts.Length >= 2)
                            {
                                okay = true;
                                passIds.Add(itemParts[0], itemParts[1]);
                            }
                        }
                        if (!okay) m_Logger.Error("Fialed to update bet in db: " + uuid);
                    }));
                }

                Task.WaitAll(dbTasks.ToArray());

                m_Logger.Info("Updated bets in db: " + passIds.Count + " / " + bets.Count);

                if (passIds.Count > 0)
                {
                    m_Logger.Info("Update wallets...");

                    List<dynamic> items = new List<dynamic>();
                    foreach (var bet in bets)
                    {
                        string currentBetId = bet.bet_uuid;
                        if (!passIds.ContainsKey(currentBetId)) continue; // only keep going with good records
                        items.Add(new
                        {
                            bet_uuid = currentBetId,
                            table_code = TableCode,
                            shoe_code = m_ShoeCode,
                            round_number = m_RoundIndex,
                            bet_pool = bet.bet_pool,
                            merchant_code = bet.merchant_code,
                            currency_code = bet.currency_code,
                            player_id = bet.player_id,
                            client_id = bet.client_id,
                            session_id = bet.session_id,
                            pay_amount = bet.pay_amount,
                            settle_time = passIds[currentBetId]
                        });
                    }

                    if (items.Count > 0)
                    {
                        List<string> errIds = new List<string>();
                        List<Task> walletTasks = new List<Task>();
                        foreach (var item in items)
                        {
                            walletTasks.Add(Task.Run(async () =>
                            {
                                bool foundError = false;
                                string uuid = item.bet_uuid.ToString();
                                string walletReplyStr = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                                                            "single-wallet", "credit-for-settling-bet", m_Node.GetJsonHelper().ToJsonString(item));
                                if (String.IsNullOrEmpty(walletReplyStr) || !walletReplyStr.Contains('{'))
                                {
                                    foundError = true;
                                }
                                else
                                {
                                    dynamic walletReply = m_Node.GetJsonHelper().ToJsonObject(walletReplyStr);
                                    if (walletReply.error_code != 0) foundError = true;
                                }

                                if (foundError)
                                {
                                    errIds.Add(uuid);
                                    m_Logger.Error("Fialed to update wallet for bet: " + uuid);
                                }

                            }));
                        }

                        Task.WaitAll(walletTasks.ToArray());

                        m_Logger.Info("Updated wallets with bet results: " + (items.Count - errIds.Count) + " / " + items.Count);
                    }
                }

                m_Logger.Info("Update cache...");

                int cacheErrIdCount = 0;
                using (var cnn = dbhelper.OpenDatabase(m_MainCache))
                {
                    foreach (var bet in bets)
                    {
                        int rows = 0;
                        using (var cmd = cnn.CreateCommand())
                        {
                            dbhelper.AddParam(cmd, "@bet_uuid", bet.bet_uuid);
                            dbhelper.AddParam(cmd, "@pay_amount", bet.pay_amount);
                            dbhelper.AddParam(cmd, "@game_result", bet.game_result);

                            cmd.CommandText = "update tbl_bet_record "
                                                + " set pay_amount = @pay_amount "
                                                + " , game_result = @game_result "
                                                + " , bet_state = 1 "
                                                + " , settle_time = CURRENT_TIMESTAMP "
                                                + " where bet_uuid = @bet_uuid "
                                                ;
                            rows = cmd.ExecuteNonQuery();
                            if (rows <= 0)
                            {
                                cacheErrIdCount++;
                                m_Logger.Error("Errors found when update bet in cache: " + bet.bet_uuid.ToString());
                            }
                        }
                    }
                }
                m_Logger.Info("Updated bets in cache: " + (bets.Count - cacheErrIdCount) + " / " + bets.Count);

            }

            m_Logger.Info("Settle done");

        }


        public async Task CleanUpLastIncompleteGame()
        {
            //System.Diagnostics.Debugger.Break();

            m_Logger.Info("Checking last incomplete game...");
            await Task.Delay(500);
            m_Logger.Info("Done");

            string tableCode = TableCode;
            string serverCode = m_Node.GetName();

            var lastGameReq = new
            {
                server_code = serverCode,
                table_code = tableCode,
            };
            string lastGameReplyStr = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                "game-data", "get-last-game", m_Node.GetJsonHelper().ToJsonString(lastGameReq));

            if (string.IsNullOrEmpty(lastGameReplyStr))
            {
                m_Logger.Error("Failed to get last (incomplete) game");
                return;
            }

            dynamic lastGame = m_Node.GetJsonHelper().ToJsonObject(lastGameReplyStr);

            int respCode = lastGame.error_code;

            if (respCode != 0)
            {
                m_Logger.Error("Failed to get last (incomplete) game: " + respCode);
                return;
            }

            string shoeCode = lastGame.game.shoe_code;
            string gameResult = lastGame.game.game_result;
            string gameOutput = lastGame.game.game_output;
            int roundNumber = lastGame.game.round_number;
            int roundState = lastGame.game.round_state;

            string roundId = tableCode + "-" + shoeCode + "-" + roundNumber;

            if (roundNumber > 0 && shoeCode.Length > 0)
            {
                var queryBetsReq = new
                {
                    server_code = serverCode,
                    table_code = tableCode,
                    shoe_code = shoeCode,
                    round_number = roundNumber,
                };

                string queryBetsReplyStr = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                "bet-data", "get-round-bets", m_Node.GetJsonHelper().ToJsonString(queryBetsReq));

                if (string.IsNullOrEmpty(queryBetsReplyStr))
                {
                    m_Logger.Error("Failed to get round bets");
                    return;
                }

                dynamic betsReply = m_Node.GetJsonHelper().ToJsonObject(queryBetsReplyStr);

                if (betsReply.error_code != 0)
                {
                    m_Logger.Error("Failed to get round bets: " + betsReply.error_code);
                    return;
                }

                List<dynamic> bets = new List<dynamic>();

                if (betsReply.bets != null)
                {
                    foreach (var bet in betsReply.bets)
                    {
                        bets.Add(bet);
                    }
                }

                if (bets.Count <= 0)
                {
                    m_Logger.Info("No bets found in last (incomplete) game");
                    return;
                }

                if (roundState < 7) // need to cancel it
                {
                    // cancel debit records for all bets got

                    foreach (var bet in bets)
                    {
                        if (bet.cancel_state == 0 && (bet.debit_state == 0 || bet.credit_state == 0))
                        {
                            var cancelDebitReq = new
                            {
                                bet.bet_uuid,
                                bet.merchant_code,
                            };

                            string cancelDebitReplyStr = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                            "transaction-data", "request-to-cancel-debit", m_Node.GetJsonHelper().ToJsonString(cancelDebitReq));

                            if (string.IsNullOrEmpty(cancelDebitReplyStr))
                            {
                                m_Logger.Error("Failed to cancel bet debit - " + bet.bet_uuid);
                            }
                            else
                            {
                                dynamic cancelDebitReply = m_Node.GetJsonHelper().ToJsonObject(cancelDebitReplyStr);

                                if (cancelDebitReply.error_code != 0)
                                {
                                    m_Logger.Error("Failed to cancel bet debit - " + bet.bet_uuid + " : " + cancelDebitReply.error_message);
                                }
                                else
                                {
                                    m_Logger.Info("Cancel bet debit - " + bet.bet_uuid);
                                }
                            }
                        }
                        else
                        {
                            m_Logger.Info("Bet has been cancelled or settled - " + bet.bet_uuid);
                        }
                    }



                }
                else // check and settle it if need 
                {
                    if (string.IsNullOrEmpty(gameResult))
                    {
                        m_Logger.Error("Failed to get game result of last (incomplete) game");
                        return;
                    }

                    dynamic game = null;
                    List<string> players = new List<string>();
                    List<decimal> scores = new List<decimal>();

                    try
                    {

                        game = m_Node.GetJsonHelper().ToJsonObject(gameResult);
                        foreach (var item in game.players)
                        {
                            string player = item;
                            players.Add(player);
                        }
                        foreach (var item in game.scores)
                        {
                            decimal score = item;
                            scores.Add(score);
                        }
                    }
                    catch (Exception ex)
                    {
                        m_Logger.Error("Failed to convert game result of last (incomplete) game from string to object: " + ex.ToString());
                        return;
                    }

                    // find all bets and their credit records

                    // if credit record exists then try to redo if need
                    // else then settle and create new credit record for the bet

                    foreach (var bet in bets)
                    {
                        /*
                    
                        dynamic bet = new
                            {
                                bet_uuid = uuid,

                                server_code = serverCode,
                                table_code = tableCode,
                                shoe_code = shoeCode,
                                round_number = roundNumber,
                                
                                bet_type = betType,
                                bet_pool = pool,
                                bet_cpc = cpc,
                                bet_bpl = bpl,

                                pay_amount = payout,
                                bet_amount = amount,
                                
                                bet_input = betInput,
                                game_result = result,

                                merchant_code = merchant,
                                currency_code = currency,
                                player_id = player,
                                client_id = client,
                                session_id = session,

                                bet_state = Convert.ToInt32(reader["bet_state"].ToString()),
                                settle_state = Convert.ToInt32(reader["settle_state"].ToString()),
                                debit_state = Convert.ToInt32(reader["debit_state"].ToString()),
                                credit_state = Convert.ToInt32(reader["credit_state"].ToString()),
                                cancel_state = Convert.ToInt32(reader["cancel_state"].ToString()),
                            };

                        */

                        if (bet.debit_state == 1 && bet.credit_state == 1) continue;

                        // try to find credit record

                        string creditUuid = "";
                        decimal payoutAmount = 0;

                        var findCreditReq = new
                        {
                            bet.bet_uuid,
                            bet.merchant_code,
                        };

                        string findCreditReplyStr = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                        "transaction-data", "get-bet-credit", m_Node.GetJsonHelper().ToJsonString(findCreditReq));

                        if (string.IsNullOrEmpty(findCreditReplyStr))
                        {
                            creditUuid = "";
                            m_Logger.Error("Failed to get bet credit - " + bet.bet_uuid);
                        }
                        else
                        {
                            dynamic findCreditReply = m_Node.GetJsonHelper().ToJsonObject(findCreditReplyStr);

                            if (findCreditReply.error_code != 0)
                            {
                                creditUuid = "";
                                m_Logger.Error("Failed to get bet credit - " + bet.bet_uuid + " : " + findCreditReply.error_message);
                            }
                            else
                            {
                                creditUuid = findCreditReply.credit.credit_uuid.ToString();
                                payoutAmount = findCreditReply.credit.credit_amount;

                                m_Logger.Info("Find bet credit - " + creditUuid);
                            }


                        }

                        // if found credit ->  continue ( next bet record )

                        if (!string.IsNullOrEmpty(creditUuid)) continue;

                        // if not found -> re-settle it (calc payout -> create credit -> call single wallet ... etc)

                        // ...

                        m_Logger.Info("Try to re-settle bet - " + bet.bet_uuid);

                        // check bet winloss...

                        decimal betcpc = bet.bet_cpc;
                        int betbpl = bet.bet_bpl;
                        int bettype = bet.bet_type;
                        string betinput = bet.bet_input;

                        string merchant = bet.merchant_code;
                        string currency = bet.currency_code;
                        string playerId = bet.player_id;

                        decimal latestPayout = -1;
                        string shortId = GamePlayer.GetShortId(merchant, currency, playerId);

                        int playerIdx = players.IndexOf(shortId);
                        if (playerIdx >= 0 && playerIdx < scores.Count)
                            latestPayout = scores[playerIdx];

                        if (latestPayout < 0)
                        {
                            m_Logger.Info("Cannot get payout from game result - " + creditUuid);
                            continue;
                        }

                        latestPayout = BetToCoins(latestPayout, betcpc, betbpl);

                        decimal latestBet = bet.bet_amount;
                        decimal comission = 0;

                        var winloss = latestPayout - latestBet;
                        if (winloss > 0 && latestPayout >= 0 && latestBet >= 0)
                        {
                            comission = winloss * m_GameLogic.GetWinCommission();
                            if (comission > 0) latestPayout -= comission;
                        }

                        // update bet in db...

                        var updateBetReq = new
                        {
                            bet.bet_uuid,
                            bet.merchant_code,
                            pay_amount = latestPayout,
                            contribution = comission,
                            game_output = gameOutput,
                            game_result = gameResult
                        };

                        List<dynamic> passIds = new List<dynamic>();

                        string dbReply = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                                            "bet-data", "update-result", m_Node.GetJsonHelper().ToJsonString(updateBetReq));
                        //if (dbErr != "ok") errIds.Add(uuid);
                        if (dbReply.Contains('-') && dbReply.Contains('='))
                        {
                            var itemParts = dbReply.Split('=');
                            passIds.Add(new
                            {
                                bet_uuid = itemParts[0],
                                settle_time = itemParts[1]
                            });
                        }
                        else
                        {
                            m_Logger.Error("Failed to re-settle bet (update bet in db) - " + bet.bet_uuid);
                            continue;
                        }

                        // update wallet...

                        var updateWalletReq = new
                        {
                            bet_uuid = bet.bet_uuid,
                            table_code = bet.table_code,
                            shoe_code = bet.shoe_code,
                            round_number = bet.round_number,
                            bet_pool = bet.bet_pool,
                            merchant_code = bet.merchant_code,
                            currency_code = bet.currency_code,
                            player_id = bet.player_id,
                            client_id = bet.client_id,
                            session_id = bet.session_id,
                            pay_amount = latestPayout,
                            settle_time = passIds[0].settle_time
                        };

                        string walletReplyStr = await RemoteCaller.RandomCall(m_Node.GetRemoteServices(),
                                                "single-wallet", "credit-for-settling-bet", m_Node.GetJsonHelper().ToJsonString(updateWalletReq));
                        if (String.IsNullOrEmpty(walletReplyStr) || !walletReplyStr.Contains('{'))
                        {
                            m_Logger.Error("Failed to re-settle bet (update wallet) - " + bet.bet_uuid);
                            continue;
                        }
                        else
                        {
                            dynamic walletReply = m_Node.GetJsonHelper().ToJsonObject(walletReplyStr);
                            if (walletReply.error_code != 0)
                            {
                                m_Logger.Error("Failed to re-settle bet (update wallet) - " + bet.bet_uuid);
                                continue;
                            }
                        }

                        m_Logger.Info("Bet re-settled - " + bet.bet_uuid);

                    }

                }

            } // end if found some game records
            else
            {
                m_Logger.Info("Last (incomplete) game not found");
                return;
            }

            

        }

    }

    public class GamePlayer
    {
        public string PlayerId { get; set; }
        public string MerchantCode { get; set; }
        public string CurrencyCode { get; set; }
        public string FrontEndServer { get; set; }
        public decimal GamePoints { get; set; }

        public List<string> BetIds { get; set; }
        public List<decimal> BetAmounts { get; set; }

        public GamePlayer(string merchantCode, string currencyCode, string playerId)
        {
            PlayerId = playerId;
            MerchantCode = merchantCode;
            CurrencyCode = currencyCode;
            FrontEndServer = "";
            GamePoints = 0;

            BetIds = new List<string>();
            BetAmounts = new List<decimal>();
        }

        public static string GetShortId(string merchantCode, string currencyCode, string playerId)
        {
            return merchantCode + "|" + currencyCode + "|" + playerId;
        }

        public string GetShortId()
        {
            return GamePlayer.GetShortId(MerchantCode, CurrencyCode, PlayerId);
        }

        public decimal GetTotalBetAmount()
        {
            decimal total = 0;
            foreach (var bet in BetAmounts)
            {
                total += bet;
            }
            return total;
        }

        public void ClearBets()
        {
            BetIds.Clear();
            BetAmounts.Clear();
        }
    }

    public class TableSetting
    {
        public string TableCode { get; set; }
        public string TableName { get; set; }

        public TableSetting()
        {
            TableCode = "";
            TableName = "";
        }
    }

    public class TableGroupSetting
    {
        public int GameType { get; set; }
        public int TableType { get; set; }
        public int BettingTime { get; set; }
        public string TestMerchant { get; set; }
        public List<TableSetting> Tables { get; set; }

        public TableGroupSetting()
        {
            GameType = 1;
            TableType = 1;
            BettingTime = 10;
            TestMerchant = "m1";
            Tables = new List<TableSetting>();
        }
    }
}
