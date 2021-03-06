﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.FrontEnd.GameBet
{
    [Access(Name = "bet-result")]
    public class BetResultService
    {
        BetResultDeliverer m_Deliverer = null;
        protected IServerNode m_LocalNode = null;
        protected string m_MainCache = "MainCache";

        [Access(Name = "on-load", IsLocal = true)]
        public async Task<string> Load(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            m_LocalNode = node;
            if (m_Deliverer == null) m_Deliverer = new BetResultDeliverer(node);

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

        [Access(Name = "on-connect", IsLocal = true)]
        public void OnConnect(IWebSession session)
        {
            //Console.WriteLine(m_LocalNode.GetName() + " - OnConnect: " + session.GetRemoteAddress());

            //System.Diagnostics.Debugger.Break();

            //m_LocalNode.GetLogger().Info("OnClientConnect: " + session.GetRequestPath());

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

            if (!String.IsNullOrEmpty(merchantCode)
                && !String.IsNullOrEmpty(currencyCode)
                && !String.IsNullOrEmpty(playerId)
                && !String.IsNullOrEmpty(sessionId))
            {
                okay = true;
            }

            if (okay && m_Deliverer != null)
                m_Deliverer.AddClient(session.GetRemoteAddress(), merchantCode, currencyCode, playerId, session);
        }

        [Access(Name = "on-disconnect", IsLocal = true)]
        public void OnDisconnect(IWebSession session)
        {
            //Console.WriteLine(m_LocalNode.GetName() + " - OnDisconnect: " + session.GetRemoteAddress());
            //m_LocalNode.GetLogger().Info("OnClientDisconnect: " + session.GetRemoteAddress());

            if (m_Deliverer != null) m_Deliverer.RemoveClient(session.GetRemoteAddress());
        }
    }
}
