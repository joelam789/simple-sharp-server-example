using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.FrontEnd.GameBet
{
    public class BetResultDeliverer
    {
        static CommonRng m_Rng = new CommonRng();

        private Timer m_Timer = null;

        private IServerNode m_Node = null;
        private IServerLogger m_Logger = null;

        private bool m_IsRunning = false;
        private bool m_IsWorking = false;

        private string m_MainCache = "MainCache";

        private string m_ServerName = "";

        private ConcurrentDictionary<string, ClientInfo> m_Clients = new ConcurrentDictionary<string, ClientInfo>();

        public BetResultDeliverer(IServerNode node)
        {
            m_Node = node;
            m_Logger = m_Node.GetLogger();

            m_ServerName = m_Node.GetName();

            m_IsWorking = false;
            m_IsRunning = false;
        }

        public async Task Start()
        {
            await Stop();
            m_IsWorking = true;
            m_IsRunning = false;
            m_Timer = new Timer(Tick, m_Rng, 500, 1000 * 1);
        }

        public async Task Stop()
        {
            m_IsWorking = false;
            if (m_Timer != null)
            {
                //Thread.Sleep(300);
                await Task.Delay(300);
                m_Timer.Dispose();
                m_Timer = null;
                //Thread.Sleep(200);
                await Task.Delay(200);
            }

            m_Clients.Clear();

        }

        public void AddClient(string clientId, string merchantCode, string currencyCode, string playerId, IWebSession session)
        {
            ClientInfo oldOne = null;
            if (m_Clients.ContainsKey(clientId))
            {
                if (m_Clients.TryRemove(clientId, out oldOne))
                {
                    if (oldOne != null)
                    {
                        oldOne.Session.CloseConnection();
                    }
                }
            }

            ClientInfo newOne = new ClientInfo();
            newOne.ClientId = clientId;
            newOne.PlayerId = playerId;
            newOne.MerchantCode = merchantCode;
            newOne.CurrencyCode = currencyCode;
            newOne.Session = session;

            if (session != null) m_Clients.TryAdd(clientId, newOne);
        }

        public void RemoveClient(string clientId)
        {
            ClientInfo oldOne = null;
            if (m_Clients.ContainsKey(clientId))
            {
                if (m_Clients.TryRemove(clientId, out oldOne))
                {
                    if (oldOne != null)
                    {
                        oldOne.Session.CloseConnection();
                    }
                }
            }
        }

        private void Tick(object param)
        {
            if (!m_IsWorking) return;

            if (m_IsRunning) return;
            m_IsRunning = true;
            try
            {
                //Console.WriteLine("Check and send bet result...");
                //m_Logger.Info("Check and send bet result...");
                Deliver();
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

        private async void Deliver()
        {
            Dictionary<string, List<dynamic>> betResults = new Dictionary<string, List<dynamic>>();
            var dbhelper = m_Node.GetDataHelper();
            using (var cnn = dbhelper.OpenDatabase(m_MainCache))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@front_end", m_ServerName);

                    cmd.CommandText = " update tbl_bet_record "
                                    + " set bet_state = 2 " // that means we are going to send them
                                    + " where front_end = @front_end and bet_state = 1 ";

                    cmd.ExecuteNonQuery();
                }

                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@front_end", m_ServerName);

                    // select records which are ready to be sent
                    cmd.CommandText = " select * from tbl_bet_record "
                                    + " where front_end = @front_end and bet_state = 2 ";

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new
                            {
                                client = reader["client_id"].ToString(),
                                server = reader["server_code"].ToString(),
                                table = reader["table_code"].ToString(),
                                shoe = reader["shoe_code"].ToString(),
                                round = Convert.ToInt32(reader["round_number"].ToString()),
                                pool = Convert.ToInt32(reader["bet_pool"].ToString()),
                                bet_type = Convert.ToInt32(reader["bet_type"].ToString()),
                                bet_input = reader["game_input"].ToString(),
                                bet_id = reader["bet_uuid"].ToString(),
                                bet = Convert.ToDecimal(reader["bet_amount"].ToString()),
                                payout = Convert.ToDecimal(reader["pay_amount"].ToString()),
                                result = reader["game_result"].ToString()
                            };

                            if (betResults.ContainsKey(item.client))
                            {
                                var list = betResults[item.client];
                                list.Add(item);
                            }
                            else
                            {
                                var list = new List<dynamic>();
                                list.Add(item);
                                betResults.Add(item.client, list);
                            }

                        }
                    }
                }

                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@front_end", m_ServerName);

                    // remove them
                    cmd.CommandText = " delete from tbl_bet_record "
                                    + " where front_end = @front_end and bet_state = 2 ";

                    cmd.ExecuteNonQuery();
                }
            }

            foreach (var item in betResults)
            {
                try
                {
                    var list = item.Value;
                    var clientMsg = new
                    {
                        msg = "bet_result",
                        results = list
                    };
                    ClientInfo client = null;
                    if (m_Clients.TryGetValue(item.Key, out client))
                    {
                        await client.Session.Send(m_Node.GetJsonHelper().ToJsonString(clientMsg));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("FES Send-Bet-Result Error - " + ex.ToString());
                }
            }

            if (betResults.Count > 0) m_Logger.Info("Sent bet results to clients - " + betResults.Count);
        }
    }

    public class ClientInfo
    {
        public IWebSession Session { get; set; }

        public String ClientId { get; set; }
        public String PlayerId { get; set; }
        public String MerchantCode { get; set; }
        public String CurrencyCode { get; set; }
    }
}
