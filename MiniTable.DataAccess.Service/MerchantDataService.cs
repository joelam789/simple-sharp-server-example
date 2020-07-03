using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.DataAccess.Service
{
    [Access(Name = "merchant-data", IsPublic = false)]
    public class MerchantDataService
    {
        MerchantDataCache m_LocalCacheUpdater = null;
        private string m_MainDatabase = "MainDB";

        [Access(Name = "on-load", IsLocal = true)]
        public async Task<string> Load(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            if (m_LocalCacheUpdater == null) m_LocalCacheUpdater = new MerchantDataCache(node);

            await Task.Delay(50);
            if (m_LocalCacheUpdater != null) await m_LocalCacheUpdater.Start();
            await Task.Delay(50);

            node.GetLogger().Info(this.GetType().Name + " service started");

            return "";
        }

        [Access(Name = "on-unload", IsLocal = true)]
        public async Task<string> Unload(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            await Task.Delay(100);
            if (m_LocalCacheUpdater != null)
            {
                await m_LocalCacheUpdater.Stop();
                m_LocalCacheUpdater = null;
            }
            await Task.Delay(100);

            node.GetLogger().Info(this.GetType().Name + " service stopped");

            return "";
        }

        [Access(Name = "get-merchant-url")]
        public async Task GetMerchantUrlFromLocalCache(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

            if (m_LocalCacheUpdater == null)
            {
                await ctx.Session.Send("Service not available");
                return;
            }

            string merchantKey = ctx.Data.ToString();
            if (merchantKey.Trim().Length <= 0)
            {
                await ctx.Session.Send("Invalid request");
                return;
            }

            var url = m_LocalCacheUpdater.GetMerchantUrl(merchantKey);
            await ctx.Session.Send(url);

        }

        [Access(Name = "get-merchant-info")]
        public async Task GetMerchantInfoFromLocalCache(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

            if (m_LocalCacheUpdater == null)
            {
                await ctx.Session.Send("Service not available");
                return;
            }

            string merchantKey = ctx.Data.ToString();
            if (merchantKey.Trim().Length <= 0)
            {
                await ctx.Session.Send("Invalid request");
                return;
            }

            var info = m_LocalCacheUpdater.GetMerchantInfo(merchantKey);
            await ctx.Session.Send(info);

        }

        [Access(Name = "set-maintenance-mode")]
        public async Task SetMaintenanceMode(RequestContext ctx)
        {
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

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            string merchantCodes = req.merchant_codes;
            if (merchantCodes != null) merchantCodes = merchantCodes.Trim();
            else merchantCodes = "";

            if (merchantCodes.Length > 0)
            {
                string allMerchants = "";
                string[] merchantArray = merchantCodes.Split(',');
                foreach (string merchantItem in merchantArray)
                {
                    string oneMerchant = merchantItem.Trim();
                    if (oneMerchant.Length <= 0) continue;
                    if (allMerchants.Length <= 0) allMerchants = "'" + oneMerchant + "'";
                    else allMerchants = allMerchants + ",'" + oneMerchant + "'";
                }

                if (allMerchants.Length > 0) merchantCodes = " (" + allMerchants + ") ";
                else merchantCodes = "";
            }

            int updateRows = 0;
            int mode = req.mode;

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_MainDatabase))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    //dbhelper.AddParam(cmd, "@bet_uuid", betUuid);

                    var sql = " update tbl_merchant_info "
                                    + " set is_maintained = " + mode
                                    + " where is_active > 0 "
                                    ;

                    if (merchantCodes.Length > 0) sql += " and merchant_code in " + merchantCodes;

                    cmd.CommandText = sql;

                    updateRows = cmd.ExecuteNonQuery();
                }
            }

            ctx.Logger.Info("Updated maintenance modes of merchants: " + updateRows);

            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                error_code = 0,
                update_rows = updateRows,
                error_message = updateRows.ToString()
            }));
        }
    }
}
