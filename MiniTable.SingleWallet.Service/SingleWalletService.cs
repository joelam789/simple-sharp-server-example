using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.SingleWallet.Service
{
    [Access(Name = "single-wallet", IsPublic = false)]
    public class SingleWalletService
    {
        async Task<string> CallMerchantApi(RequestContext ctx, string serviceName, string actionName, string jsonParam)
        {
            var svcs = ctx.LocalServices;
            if (svcs.InternalServices.ContainsKey(serviceName))
            {
                var svc = svcs.InternalServices[serviceName];
                object ret = await svc.LocalCall(actionName, jsonParam);
                if (ret != null) return ret.ToString();
            }
            else
            {
                ctx.Logger.Error("Merchant API service not found: " + serviceName + "|" + actionName);
            }
            return null;
        }

        [Access(Name = "debit-for-placing-bet")]
        public async Task DebitForBetting(RequestContext ctx)
        {
            ctx.Logger.Info("Debit for placing-bet...");

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

            string merchantCode = req.merchant_code.ToString();
            string currencyCode = req.currency_code.ToString();

            string merchantInfo = await RemoteCaller.RandomCall(ctx.RemoteServices,
                                        "merchant-data", "get-merchant-info", merchantCode + currencyCode);

            if (String.IsNullOrEmpty(merchantInfo) || !merchantInfo.Contains('{') || !merchantInfo.Contains(':'))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Merchant info not found: " + req.merchant_code.ToString()
                }));
                return;
            }

            dynamic merchant = ctx.JsonHelper.ToJsonObject(merchantInfo);
            string merchantUrl = merchant.url.ToString();
            string merchantService = merchant.service.ToString();

            if (String.IsNullOrEmpty(merchantUrl) || String.IsNullOrEmpty(merchantService))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Merchant API URL or Service not found: " + req.merchant_code.ToString()
                }));
                return;
            }

            string reqIp = req.client_id.ToString();
            if (reqIp.Contains(":")) reqIp = reqIp.Split(':')[0];

            ctx.Logger.Info("Create debit record in db...");

            var saveReq = new
            {
                bet_uuid = req.bet_uuid,
                table_code = req.table_code,
                shoe_code = req.shoe_code,
                round_number = req.round_number,
                bet_pool = req.bet_pool,
                merchant_code = req.merchant_code,
                currency_code = req.currency_code,
                player_id = req.player_id,
                client_id = req.client_id,
                session_id = req.session_id,
                bet_amount = req.bet_amount
            };
            string dbReplyStr = await RemoteCaller.RandomCall(ctx.RemoteServices,
                "transaction-data", "create-debit", ctx.JsonHelper.ToJsonString(saveReq));

            if (dbReplyStr.Trim().Length <= 0 || !dbReplyStr.Contains("{"))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Failed to create debit record in db: " + dbReplyStr
                }));

                return;
            }

            ctx.Logger.Info("Call merchant site to debit...");

            dynamic dbReply = ctx.JsonHelper.ToJsonObject(dbReplyStr);

            string apiUrl = merchantUrl;

            var apiReq = new
            {
                merchant_url = apiUrl,
                dbReply.debit_uuid,
                req.bet_uuid,
                req.merchant_code,
                req.currency_code,
                req.player_id,
                player_ip = reqIp,
                req.session_id,
                dbReply.round_id,
                req.bet_pool,
                debit_amount = req.bet_amount,
                req.bet_time,
                is_cancelled = false
            };

            string retJson = await CallMerchantApi(ctx, merchantService, "debit-for-betting", ctx.JsonHelper.ToJsonString(apiReq));
            dynamic ret = string.IsNullOrEmpty(retJson) ? null : ctx.JsonHelper.ToJsonObject(retJson);

            if (ret == null)
            {
                ctx.Logger.Info("Failed to call debit function from merchant site");

                var updateReq = new
                {
                    dbReply.debit_uuid,
                    req.bet_uuid,
                    req.merchant_code,
                    req.currency_code,
                    req.player_id,
                    request_times = 1,
                    is_success = 0,
                    network_error = 1,
                    response_error = 0
                };
                string dbReplyStr2 = await RemoteCaller.RandomCall(ctx.RemoteServices,
                    "transaction-data", "update-debit", ctx.JsonHelper.ToJsonString(updateReq));

                dynamic dbReply2 = ctx.JsonHelper.ToJsonObject(dbReplyStr2);

                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Failed to call debit function from merchant site"
                }));
            }
            else
            {
                ctx.Logger.Info("Update debit record in db...");

                if (ret.error_code == 0)
                {
                    var updateReq = new
                    {
                        dbReply.debit_uuid,
                        req.bet_uuid,
                        req.merchant_code,
                        req.currency_code,
                        req.player_id,
                        request_times = 1,
                        is_success = 1,
                        network_error = 0,
                        response_error = 0
                    };
                    string dbReplyStr2 = await RemoteCaller.RandomCall(ctx.RemoteServices,
                        "transaction-data", "update-debit", ctx.JsonHelper.ToJsonString(updateReq));

                    if (String.IsNullOrEmpty(dbReplyStr2))
                    {
                        ctx.Logger.Info("Failed to update debit record in db");
                    }
                    else
                    {
                        dynamic dbReply2 = ctx.JsonHelper.ToJsonObject(dbReplyStr2);
                        ctx.Logger.Info("Update debit record in db - error code: " + dbReply2.error_code.ToString());
                    }
                }
                else
                {
                    var updateReq = new
                    {
                        dbReply.debit_uuid,
                        req.bet_uuid,
                        req.merchant_code,
                        req.currency_code,
                        req.player_id,
                        request_times = 1,
                        is_success = 0,
                        network_error = 0,
                        response_error = ret.error_code
                    };
                    string dbReplyStr2 = await RemoteCaller.RandomCall(ctx.RemoteServices,
                        "transaction-data", "update-debit", ctx.JsonHelper.ToJsonString(updateReq));

                    dynamic dbReply2 = ctx.JsonHelper.ToJsonObject(dbReplyStr2);

                }

                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(ret));
            }
        }

        [Access(Name = "credit-for-settling-bet")]
        public async Task CreditForBetting(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

            ctx.Logger.Info("Credit for settling-bet...");

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

            string merchantCode = req.merchant_code.ToString();
            string currencyCode = req.currency_code.ToString();

            string merchantInfo = await RemoteCaller.RandomCall(ctx.RemoteServices,
                                        "merchant-data", "get-merchant-info", merchantCode + currencyCode);

            if (String.IsNullOrEmpty(merchantInfo) || !merchantInfo.Contains('{') || !merchantInfo.Contains(':'))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Merchant info not found: " + req.merchant_code.ToString()
                }));
                return;
            }

            dynamic merchant = ctx.JsonHelper.ToJsonObject(merchantInfo);
            string merchantUrl = merchant.url.ToString();
            string merchantService = merchant.service.ToString();

            if (String.IsNullOrEmpty(merchantUrl) || String.IsNullOrEmpty(merchantService))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Merchant API URL or Service not found: " + req.merchant_code.ToString()
                }));
                return;
            }

            string reqIp = req.client_id.ToString();
            if (reqIp.Contains(":")) reqIp = reqIp.Split(':')[0];

            ctx.Logger.Info("Create credit record in db...");

            var saveReq = new
            {
                bet_uuid = req.bet_uuid,
                table_code = req.table_code,
                shoe_code = req.shoe_code,
                round_number = req.round_number,
                bet_pool = req.bet_pool,
                merchant_code = req.merchant_code,
                req.currency_code,
                player_id = req.player_id,
                client_id = req.client_id,
                session_id = req.session_id,
                pay_amount = req.pay_amount
            };
            string dbReplyStr = await RemoteCaller.RandomCall(ctx.RemoteServices,
                "transaction-data", "create-credit", ctx.JsonHelper.ToJsonString(saveReq));

            if (dbReplyStr.Trim().Length <= 0 || !dbReplyStr.Contains('{'))
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Failed to create credit record in db: " + dbReplyStr
                }));

                return;
            }

            ctx.Logger.Info("Call merchant site to credit...");

            dynamic dbReply = ctx.JsonHelper.ToJsonObject(dbReplyStr);

            string apiUrl = merchantUrl;

            var apiReq = new
            {
                merchant_url = apiUrl,
                dbReply.credit_uuid,
                req.bet_uuid,
                req.merchant_code,
                req.currency_code,
                req.player_id,
                player_ip = reqIp,
                req.session_id,
                dbReply.round_id,
                req.bet_pool,
                credit_amount = req.pay_amount,
                bet_settle_time = req.settle_time,
                request_times = 1,
                is_cancelled = false
            };

            string retJson = await CallMerchantApi(ctx, merchantService, "credit-for-settling", ctx.JsonHelper.ToJsonString(apiReq));
            dynamic ret = string.IsNullOrEmpty(retJson) ? null : ctx.JsonHelper.ToJsonObject(retJson);

            if (ret == null)
            {
                ctx.Logger.Info("Failed to call credit function from merchant site");

                var updateReq = new
                {
                    dbReply.credit_uuid,
                    req.bet_uuid,
                    req.merchant_code,
                    req.currency_code,
                    req.player_id,
                    request_times = 1,
                    is_success = 0,
                    network_error = 1,
                    response_error = 0
                };
                string dbReplyStr2 = await RemoteCaller.RandomCall(ctx.RemoteServices,
                    "transaction-data", "update-credit", ctx.JsonHelper.ToJsonString(updateReq));

                dynamic dbReply2 = ctx.JsonHelper.ToJsonObject(dbReplyStr2);

                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = -1,
                    error_message = "Failed to call credit function from merchant site"
                }));
            }
            else
            {
                ctx.Logger.Info("Update credit record in db...");

                if (ret.error_code == 0)
                {
                    var updateReq = new
                    {
                        dbReply.credit_uuid,
                        req.bet_uuid,
                        req.merchant_code,
                        req.currency_code,
                        req.player_id,
                        request_times = 1,
                        is_success = 1,
                        network_error = 0,
                        response_error = 0
                    };
                    string dbReplyStr2 = await RemoteCaller.RandomCall(ctx.RemoteServices,
                        "transaction-data", "update-credit", ctx.JsonHelper.ToJsonString(updateReq));

                    if (String.IsNullOrEmpty(dbReplyStr2))
                    {
                        ctx.Logger.Info("Failed to update credit record in db");
                    }
                    else
                    {
                        dynamic dbReply2 = ctx.JsonHelper.ToJsonObject(dbReplyStr2);
                        ctx.Logger.Info("Update credit record in db - error code: " + dbReply2.error_code.ToString());
                    }
                }
                else
                {
                    var updateReq = new
                    {
                        dbReply.credit_uuid,
                        req.bet_uuid,
                        req.merchant_code,
                        req.currency_code,
                        req.player_id,
                        request_times = 1,
                        is_success = 0,
                        network_error = 0,
                        response_error = ret.error_code
                    };
                    string dbReplyStr2 = await RemoteCaller.RandomCall(ctx.RemoteServices,
                        "transaction-data", "update-credit", ctx.JsonHelper.ToJsonString(updateReq));

                    dynamic dbReply2 = ctx.JsonHelper.ToJsonObject(dbReplyStr2);

                }

                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(ret));
            }
        }
    }
}
