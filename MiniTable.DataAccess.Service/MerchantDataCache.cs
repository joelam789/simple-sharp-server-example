using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.DataAccess.Service
{
    public class MerchantDataCache
    {
        static CommonRng m_Rng = new CommonRng();

        private Timer m_Timer = null;

        private IServerNode m_Node = null;
        private IServerLogger m_Logger = null;

        private bool m_IsRunning = false;
        private bool m_IsWorking = false;

        private string m_MainDatabase = "MainDB";

        private string m_ServerName = "";

        private Dictionary<string, dynamic> m_Merchants = new Dictionary<string, dynamic>();

        public MerchantDataCache(IServerNode node)
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
            m_Timer = new Timer(Tick, m_Rng, 500, 1000 * 5); // update merchant data every 5s
        }

        public async Task Stop()
        {
            m_IsWorking = false;
            if (m_Timer != null)
            {
                await Task.Delay(500);
                m_Timer.Dispose();
                m_Timer = null;
            }

        }

        private void Tick(object param)
        {
            if (!m_IsWorking) return;
            if (m_IsRunning) return;
            m_IsRunning = true;
            try
            {
                UpdateLocalCacheFromDb();
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

        private void UpdateLocalCacheFromDb()
        {
            Dictionary<string, dynamic> merchants = new Dictionary<string, dynamic>();

            var dbhelper = m_Node.GetDataHelper();
            using (var cnn = dbhelper.OpenDatabase(m_MainDatabase))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    cmd.CommandText = " select * from tbl_merchant_info ";

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new
                            {
                                merchant = reader["merchant_code"].ToString(),
                                currency = reader["currency_code"].ToString(),
                                url = reader["api_url"].ToString(),
                                db = reader["db_name"].ToString(),
                                cpc = reader["cpc_options"].ToString(),
                                bpl = reader["bpl_options"].ToString(),
                                active = Convert.ToInt32(reader["is_active"].ToString()),
                                service = reader["api_service"].ToString(),
                                maintaining = Convert.ToInt32(reader["is_maintained"].ToString())
                            };

                            string merchantKey = item.merchant + item.currency;

                            if (merchants.ContainsKey(merchantKey)) merchants.Remove(merchantKey);
                            merchants.Add(merchantKey, item);

                        }
                    }
                }
            }

            m_Merchants = merchants;
        }

        public string GetMerchantUrl(string merchantKey)
        {

            var url = "";
            var merchants = m_Merchants;

            if (merchants.ContainsKey(merchantKey))
            {
                var item = merchants[merchantKey];
                if (item.active > 0) url = item.url;
            }

            return url;
        }

        public string GetMerchantUrl(string merchantCode, string currencyCode)
        {
            return GetMerchantUrl(merchantCode + currencyCode);
        }

        public string GetMerchantInfo(string merchantKey)
        {
            var info = "";
            var merchants = m_Merchants;

            if (merchants.ContainsKey(merchantKey))
            {
                var item = merchants[merchantKey];
                if (item.active > 0) info = m_Node.GetJsonHelper().ToJsonString(item);
            }

            return info;
        }

        public string GetMerchantInfo(string merchantCode, string currencyCode)
        {
            return GetMerchantInfo(merchantCode + currencyCode);
        }
    }
}
