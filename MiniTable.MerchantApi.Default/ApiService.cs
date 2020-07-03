using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.MerchantApi.Default
{
    [Access(Name = "merchant-api", IsPublic = false)]
    public class ApiService
    {
        protected IServerNode m_LocalNode = null;

        [Access(Name = "on-load", IsLocal = true)]
        public async Task<string> Load(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            m_LocalNode = node;

            await Task.Delay(50);

            node.GetLogger().Info(this.GetType().Name + " service loaded - default merchant api service");

            return "";
        }

        [Access(Name = "player-login", IsLocal = true)]
        public async Task<string> PlayerLogin(string jsonRequest)
        {
            if (m_LocalNode == null) return m_LocalNode.GetJsonHelper().ToJsonString(new
            {
                error_code = -1,
                error_message = "Service is not available"
            });

            m_LocalNode.GetLogger().Info("call merchant api: player-login");

            dynamic req = m_LocalNode.GetJsonHelper().ToJsonObject(jsonRequest);
            string merchantUrl = req.merchant_url.ToString();

            m_LocalNode.GetLogger().Info("Player login - [" + req.merchant_code.ToString() + "] " + req.player_id.ToString());
            m_LocalNode.GetLogger().Info("Merchant URL - " + merchantUrl);

            dynamic ret = null;

            var apiReq = new
            {
                req.merchant_code,
                req.currency_code,
                req.player_id,
                req.login_token
            };

            try
            {
                ret = await RemoteCaller.Request(merchantUrl + "/player/validate-login", apiReq, null, 10 * 1000);
            }
            catch (Exception ex)
            {
                ret = null;
                m_LocalNode.GetLogger().Error("Three-Way Login Error - Failed to call merchant API: " + ex.Message);
            }

            if (ret == null) return null;

            if (ret.error_code != 0)
            {
                m_LocalNode.GetLogger().Error("Three-Way Login Error: " + (ret == null ? "Failed to call merchant API" : ret.error_message));
                return m_LocalNode.GetJsonHelper().ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Three-Way Login Error"
                });
            }

            return m_LocalNode.GetJsonHelper().ToJsonString(ret);
        }

        [Access(Name = "get-player-balance", IsLocal = true)]
        public async Task<string> GetPlayerBalance(string jsonRequest)
        {
            if (m_LocalNode == null) return m_LocalNode.GetJsonHelper().ToJsonString(new
            {
                error_code = -1,
                error_message = "Service is not available"
            });

            //m_LocalNode.GetLogger().Info("call merchant api: get-player-balance");

            dynamic req = m_LocalNode.GetJsonHelper().ToJsonObject(jsonRequest);
            string merchantUrl = req.merchant_url.ToString();

            //m_LocalNode.GetLogger().Info("Merchant URL - " + merchantUrl);

            dynamic ret = null;

            var apiReq = new
            {
                req.merchant_code,
                req.currency_code,
                req.player_id
            };

            try
            {
                ret = await RemoteCaller.Request(merchantUrl + "/player/get-balance", apiReq, null, 10 * 1000);
            }
            catch (Exception ex)
            {
                ret = null;
                m_LocalNode.GetLogger().Error("Get Player Balance Error - Failed to call merchant API: " + ex.Message);
            }

            if (ret == null) return null;

            if (ret.error_code != 0)
            {
                m_LocalNode.GetLogger().Error("Get Player Balance Error: " + (ret == null ? "Failed to call merchant API" : ret.error_message));
                return m_LocalNode.GetJsonHelper().ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Get Player Balance Error"
                });
            }
            return m_LocalNode.GetJsonHelper().ToJsonString(ret);
        }

        [Access(Name = "debit-for-betting", IsLocal = true)]
        public async Task<string> DebitForBetting(string jsonRequest)
        {
            if (m_LocalNode == null)
            {
                m_LocalNode.GetLogger().Error("DebitForBetting Error: Service is not available");
                return null;
            };

            //m_LocalNode.GetLogger().Info("call merchant api: debit-for-betting");

            dynamic req = m_LocalNode.GetJsonHelper().ToJsonObject(jsonRequest);
            string merchantUrl = req.merchant_url.ToString();

            //m_LocalNode.GetLogger().Info("Merchant URL - " + merchantUrl);

            dynamic ret = null;

            var apiReq = new
            {
                req.debit_uuid,
                req.bet_uuid,
                req.merchant_code,
                req.currency_code,
                req.player_id,
                req.round_id,
                req.bet_pool,
                req.debit_amount,
                req.bet_time,
                req.is_cancelled
            };

            try
            {
                ret = await RemoteCaller.Request(merchantUrl + "/bet/debit-for-placing-bet", apiReq, 20 * 1000);
            }
            catch (Exception ex)
            {
                ret = null;
                m_LocalNode.GetLogger().Error("DebitForBetting Error - Failed to call merchant API: " + ex.Message);
            }

            if (ret == null) return null;

            return m_LocalNode.GetJsonHelper().ToJsonString(ret);
        }

        [Access(Name = "cancel-debit", IsLocal = true)]
        public async Task<string> CancelDebit(string jsonRequest)
        {
            if (m_LocalNode == null)
            {
                m_LocalNode.GetLogger().Error("CancelDebit Error: Service is not available");
                return null;
            };

            //m_LocalNode.GetLogger().Info("call merchant api: debit-for-betting");

            dynamic req = m_LocalNode.GetJsonHelper().ToJsonObject(jsonRequest);
            string merchantUrl = req.merchant_url.ToString();

            //m_LocalNode.GetLogger().Info("Merchant URL - " + merchantUrl);

            dynamic ret = null;

            var apiReq = new
            {
                req.trans_uuid,
                req.debit_uuid,
                req.merchant_code,
                req.currency_code,
                req.amount

            };

            try
            {
                ret = await RemoteCaller.Request(merchantUrl + "/bet/cancel-debit", apiReq, null, 20 * 1000);
            }
            catch (Exception ex)
            {
                ret = null;
                m_LocalNode.GetLogger().Error("CancelDebit Error - Failed to call merchant API: " + ex.Message);
            }

            if (ret == null) return null;

            return m_LocalNode.GetJsonHelper().ToJsonString(ret);
        }

        [Access(Name = "credit-for-settling", IsLocal = true)]
        public async Task<string> CreditForSettling(string jsonRequest)
        {
            if (m_LocalNode == null)
            {
                m_LocalNode.GetLogger().Error("CreditForSettling Error: Service is not available");
                return null;
            };

            //m_LocalNode.GetLogger().Info("call merchant api: credit-for-settling");

            dynamic req = m_LocalNode.GetJsonHelper().ToJsonObject(jsonRequest);
            string merchantUrl = req.merchant_url.ToString();

            //m_LocalNode.GetLogger().Info("Merchant URL - " + merchantUrl);

            dynamic ret = null;

            var apiReq = new
            {
                req.credit_uuid,
                req.bet_uuid,
                req.merchant_code,
                req.currency_code,
                req.player_id,
                req.round_id,
                req.bet_pool,
                req.credit_amount,
                req.bet_settle_time,
                req.request_times,
                req.is_cancelled
            };

            try
            {
                ret = await RemoteCaller.Request(merchantUrl + "/bet/credit-for-settling-bet", apiReq, 20 * 1000);
            }
            catch (Exception ex)
            {
                ret = null;
                m_LocalNode.GetLogger().Error("CreditForSettling Error - Failed to call merchant API: " + ex.Message);
            }

            if (ret == null) return null;

            return m_LocalNode.GetJsonHelper().ToJsonString(ret);
        }
    }
}
