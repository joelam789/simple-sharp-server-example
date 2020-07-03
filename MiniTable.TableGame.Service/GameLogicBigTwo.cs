using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BigTwoGameLogic;

namespace MiniTable.TableGame.Service
{
    public class GameLogicBigTwo: ITurnBasedGameLogic
    {
        public static readonly int GAME_TYPE = 2;

        readonly int MAX_BET_COUNT = 1;

        readonly int MAX_PLAYER_COUNT = 4;
        readonly int MIN_PLAYER_COUNT = 2;

        readonly decimal GAME_POINT_BASE_LINE = 52.0m;
        readonly decimal GAME_WIN_COMMISSION = 0.10m;
        

        private BigTwoGame m_Game = null;

        public GameLogicBigTwo()
        {
            m_Game = new BigTwoGame();
        }

        public int GetGameType()
        {
            return GAME_TYPE;
        }

        public bool GetGameReady()
        {
            return m_Game.GetGameReady();
        }
        public void ResetSeats()
        {
            m_Game.LeaveGame();
        }
        public bool TakeSeat(string playerName, int playerScore)
        {
            return m_Game.JoinGame(playerName, playerScore);
        }
        public bool StartNewRound()
        {
            return m_Game.StartNewRound();
        }
        public dynamic AcceptPlay(dynamic play)
        {
            string playerName = play.player;
            List<int> cardIndexes = play.cards;
            var player = m_Game.FindPlayer(m_Game.GetCurrentTurnPlayerName());
            var okay = m_Game.AcceptPlay(playerName, cardIndexes);
            return new
            {
                okay = okay,
                turns = okay ? m_Game.GetCurrentTurns() : 0,
                player = okay && player != null ? m_Game.GetCurrentTurnPlayerName() : "",
                cards = okay && player != null ? player.CurrentHand.ToString() : ""
            };
        }
        public dynamic GetCurrentGameData(bool detailed = false)
        {
            var currentPlay = m_Game.GetLastPlay();
            
            var lastTurnPlay = m_Game.GetLastTurnPlay();
            var lastTurnPlayerName = m_Game.GetLastTurnPlayerName();

            var simpleHistory = lastTurnPlayerName + "=";
            if (lastTurnPlay != null && lastTurnPlay.Count > 0)
                simpleHistory += BigTwoLogic.ToString(lastTurnPlay);

            var data = new
            {
                play = currentPlay != null && currentPlay.Count > 0 ? BigTwoLogic.ToString(currentPlay) : "",
                history = simpleHistory,
                last = m_Game.GetLastPlayerName(),
                current = m_Game.GetCurrentTurnPlayerName(),
                state = m_Game.GetGameState(),
                turns = m_Game.GetCurrentTurns(),
                players = new List<string>(),
                scores = new List<int>(),
                cards = new List<string>(),
            };

            var gamePlayers = m_Game.GetPlayers();
            foreach (var gamePlayer in gamePlayers)
            {
                data.players.Add(gamePlayer.PlayerName);
                data.scores.Add(gamePlayer.GameScore);

                if (detailed)
                {
                    data.cards.Add(BigTwoLogic.EncodeCards(gamePlayer.CurrentHand.GetCards()));
                }
                else
                {
                    data.cards.Add(gamePlayer.CurrentHand.GetNumberOfCards().ToString());
                }
            }

            return data;
        }
        public string GetCurrentGameStatus()
        {
            return m_Game.GetGameState();
        }
        public string GetCurrentPlayerName()
        {
            return m_Game.GetCurrentTurnPlayerName();
        }
        public string GetPlayerCards(string playerName)
        {
            var gamePlayer = m_Game.FindPlayer(playerName);
            if (gamePlayer == null || gamePlayer.CurrentHand == null) return "";
            return gamePlayer.CurrentHand.ToString();
        }
        public bool IsCurrentRoundDone()
        {
            return m_Game.GetGameState() == "EndRound";
        }
        public IDictionary<string, int> CalculateScores()
        {
            if (m_Game.CalculateScores())
            {
                var result = new Dictionary<string, int>();
                var players = m_Game.GetPlayers();
                foreach (var player in players)
                {
                    result.Add(player.PlayerName, player.GameScore);
                }
                return result;
            }
            else return null;
        }
        public decimal GetGamePointBaseLine()
        {
            return GAME_POINT_BASE_LINE;
        }
        public decimal GetWinCommission()
        {
            return GAME_WIN_COMMISSION;
        }
        public int GetMaxBetCount()
        {
            return MAX_BET_COUNT;
        }
        public int GetMaxPlayerCount()
        {
            return MAX_PLAYER_COUNT;
        }
        public int GetMinPlayerCount()
        {
            return MIN_PLAYER_COUNT;
        }
        public dynamic AutoPlay()
        {
            if (m_Game.GetGameState() != "PlayingCards") return null;

            var lastPlayerName = m_Game.GetLastPlayerName();
            var currentPlayerName = m_Game.GetCurrentTurnPlayerName();

            if (string.IsNullOrEmpty(currentPlayerName)) return null;
            var currentPlayer = m_Game.FindPlayer(currentPlayerName);

            var okay = false;

            if (string.IsNullOrEmpty(lastPlayerName))
            {
                var play = BigTwoLogic.TryToGiveOutBest(currentPlayer.CurrentHand.GetCards(), 0, "3D");
                if (play == null || play.Count <= 0) return null;

                okay = m_Game.AcceptPlay(currentPlayerName, play);
            }
            else
            {
                if (m_Game.CanPass(currentPlayerName))
                {
                    okay = m_Game.AcceptPlay(currentPlayerName, new List<int>());
                }
                else
                {
                    if (lastPlayerName == currentPlayerName)
                    {
                        var play = BigTwoLogic.TryToGiveOutBest(currentPlayer.CurrentHand.GetCards(), 0);
                        okay = m_Game.AcceptPlay(currentPlayerName, play);
                    }
                    else
                    {
                        var lastPlay = m_Game.GetLastPlay();
                        var play = BigTwoLogic.TryToGiveOutBest(currentPlayer.CurrentHand.GetCards(), lastPlay.Count);
                        if (play == null || play.Count <= 0) okay = m_Game.AcceptPlay(currentPlayerName, new List<int>());
                        else okay = m_Game.AcceptPlay(currentPlayerName, play);
                    }
                    
                }
            }

            if (okay == false) return null;
            else
            {
                return new
                {
                    okay = okay,
                    turns = okay ? m_Game.GetCurrentTurns() : 0,
                    player = okay && currentPlayer != null ? m_Game.GetCurrentTurnPlayerName() : "",
                    cards = okay && currentPlayer != null ? currentPlayer.CurrentHand.ToString() : ""
                };
            }

        }
    }
}
