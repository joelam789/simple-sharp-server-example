using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.FrontEnd.GameClient
{
    public class ClientHolder
    {
        private IServerNode m_Node = null;
        private IServerLogger m_Logger = null;

        private string m_MainCache = "MainCache";

        private string m_ServerName = "";

        private Dictionary<string, ClientInfo> m_Clients = new Dictionary<string, ClientInfo>();

        public ClientHolder(IServerNode node)
        {
            m_Node = node;
            m_Logger = m_Node.GetLogger();

            m_ServerName = m_Node.GetName();

            if (m_Clients != null) m_Clients.Clear();
            else m_Clients = new Dictionary<string, ClientInfo>();

        }

        public void Clear()
        {
            List<ClientInfo> list = new List<ClientInfo>();
            lock (m_Clients)
            {
                foreach (var item in m_Clients) list.Add(item.Value);
                m_Clients.Clear();
            }
            foreach (var item in list) item.Session.CloseConnection();
            list.Clear();
        }

        public void AddClient(string clientId, string merchantCode, string currencyCode, string playerId, IWebSession session)
        {
            ClientInfo newOne = new ClientInfo();
            newOne.ClientId = clientId;
            newOne.PlayerId = playerId;
            newOne.MerchantCode = merchantCode;
            newOne.CurrencyCode = currencyCode;
            newOne.Session = session;

            ClientInfo oldOne = null;

            lock (m_Clients)
            {
                if (m_Clients.ContainsKey(clientId))
                {
                    oldOne = m_Clients[clientId];
                    m_Clients.Remove(clientId);
                }
                if (session != null) m_Clients.Add(clientId, newOne);
            }

            if (oldOne != null) oldOne.Session.CloseConnection();
        }

        public void RemoveClient(string clientId)
        {
            ClientInfo oldOne = null;

            lock (m_Clients)
            {
                if (m_Clients.ContainsKey(clientId))
                {
                    oldOne = m_Clients[clientId];
                    m_Clients.Remove(clientId);
                }
            }

            if (oldOne != null) oldOne.Session.CloseConnection();
        }

        public void KickPlayer(string merchantCode, string playerId)
        {
            List<string> clients = new List<string>();
            List<ClientInfo> list = new List<ClientInfo>();
            lock (m_Clients)
            {
                foreach (var item in m_Clients)
                {
                    if (item.Value.MerchantCode == merchantCode
                        && item.Value.PlayerId == playerId)
                    {
                        clients.Add(item.Key);
                        list.Add(item.Value);
                    }
                }
                foreach (var item in clients) m_Clients.Remove(item);
            }
            foreach (var item in list) item.Session.CloseConnection();
        }

        public void KickMerchant(string merchantCode)
        {
            List<string> clients = new List<string>();
            List<ClientInfo> list = new List<ClientInfo>();
            lock (m_Clients)
            {
                foreach (var item in m_Clients)
                {
                    if (item.Value.MerchantCode == merchantCode)
                    {
                        clients.Add(item.Key);
                        list.Add(item.Value);
                    }
                }
                foreach (var item in clients) m_Clients.Remove(item);
            }
            foreach (var item in list) item.Session.CloseConnection();
        }

        public void KickAll()
        {
            List<string> clients = new List<string>();
            List<ClientInfo> list = new List<ClientInfo>();
            lock (m_Clients)
            {
                foreach (var item in m_Clients)
                {
                    clients.Add(item.Key);
                    list.Add(item.Value);
                }
                foreach (var item in clients) m_Clients.Remove(item);
            }
            foreach (var item in list) item.Session.CloseConnection();
        }

        public ClientInfo FindClient(string merchantCode, string currencyCode, string playerId)
        {
            ClientInfo client = null;
            lock (m_Clients)
            {
                foreach (var item in m_Clients)
                {
                    if (item.Value.MerchantCode == merchantCode
                        && item.Value.CurrencyCode == currencyCode
                        && item.Value.PlayerId == playerId)
                    {
                        client = item.Value;
                        break;
                    }
                }
            }
            return client;
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
