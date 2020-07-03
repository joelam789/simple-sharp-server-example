using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.BackgroundUpdater.Service
{
    [Access(Name = "trans-cleanning", IsPublic = false)]
    public class TransCleanUpService
    {
        TransactionCleaner m_Cleaner = null;
        protected IServerNode m_LocalNode = null;
        protected string m_MainCache = "MainCache";

        [Access(Name = "on-load", IsLocal = true)]
        public async Task<string> Load(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            m_LocalNode = node;
            if (m_Cleaner == null) m_Cleaner = new TransactionCleaner(node);

            await Task.Delay(50);
            if (m_Cleaner != null) await m_Cleaner.Start();
            await Task.Delay(50);

            node.GetLogger().Info(this.GetType().Name + " service started");

            return "";
        }

        [Access(Name = "on-unload", IsLocal = true)]
        public async Task<string> Unload(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            await Task.Delay(100);
            if (m_Cleaner != null)
            {
                await m_Cleaner.Stop();
                m_Cleaner = null;
            }
            await Task.Delay(100);

            return "";
        }
    }
}
