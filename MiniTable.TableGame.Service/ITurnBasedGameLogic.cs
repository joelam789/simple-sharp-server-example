using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniTable.TableGame.Service
{
    public interface ITurnBasedGameLogic
    {
        int GetGameType();
        bool GetGameReady();
        void ResetSeats();
        bool TakeSeat(string playerName, int playerScore);
        bool StartNewRound();
        dynamic AcceptPlay(dynamic play);
        dynamic GetCurrentGameData(bool detailed = false);
        string GetCurrentGameStatus();
        string GetCurrentPlayerName();
        string GetPlayerCards(string playerName);
        decimal GetGamePointBaseLine();
        decimal GetWinCommission();
        int GetMaxBetCount();
        int GetMaxPlayerCount();
        int GetMinPlayerCount();
        bool IsCurrentRoundDone();
        IDictionary<string, int> CalculateScores();
        dynamic AutoPlay();
        

    }
}
