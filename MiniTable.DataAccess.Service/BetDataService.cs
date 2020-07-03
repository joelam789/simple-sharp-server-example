using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.DataAccess.Service
{
    [Access(Name = "bet-data", IsPublic = false)]
    public class BetDataService
    {
        string m_CommonMerchantDb = "MerchantDB";

        string m_BaseBetCode = "";
        int m_BetIndex = 0;

        private string GetBetId()
        {
            if (String.IsNullOrEmpty(m_BaseBetCode))
                m_BaseBetCode = Guid.NewGuid().ToString();

            Interlocked.Increment(ref m_BetIndex);

            return m_BaseBetCode + "-" + m_BetIndex;
        }

        [Access(Name = "on-load", IsLocal = true)]
        public async Task<string> Load(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            m_BaseBetCode = Guid.NewGuid().ToString();

            node.GetLogger().Info("Reloading database settings from config...");

            await Task.Delay(50);
            // we can apply new merchant db config just by hot-swapping, no need to restart server
            node.GetDataHelper().RefreshDatabaseSettings();
            await Task.Delay(50);

            node.GetLogger().Info(this.GetType().Name + " service started");

            return "";
        }

        [Access(Name = "save-record")]
        public async Task SaveRecord(RequestContext ctx)
        {
            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send("Invalid request");
                return;
            }

            dynamic betreq = ctx.JsonHelper.ToJsonObject(reqstr);

            string betId = "";

            try
            {
                betId = betreq.bet_id;
            }
            catch
            {
                betId = "";
            }

            if (string.IsNullOrEmpty(betId)) betId = GetBetId();

            bool okay = false;
            string betTimeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string merchantCode = betreq.merchant_code.ToString();

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_CommonMerchantDb))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@bet_uuid", betId);

                    dbhelper.AddParam(cmd, "@merchant_code", betreq.merchant_code);
                    dbhelper.AddParam(cmd, "@currency_code", betreq.currency_code);
                    dbhelper.AddParam(cmd, "@player_id", betreq.player_id);

                    dbhelper.AddParam(cmd, "@server_code", betreq.server_code);
                    dbhelper.AddParam(cmd, "@table_code", betreq.table_code);
                    dbhelper.AddParam(cmd, "@shoe_code", betreq.shoe_code);
                    dbhelper.AddParam(cmd, "@round_number", betreq.round_number);
                    dbhelper.AddParam(cmd, "@client_id", betreq.client_id);
                    dbhelper.AddParam(cmd, "@front_end", betreq.front_end);

                    dbhelper.AddParam(cmd, "@session_id", betreq.session_id);

                    dbhelper.AddParam(cmd, "@bet_pool", betreq.bet_pool);
                    dbhelper.AddParam(cmd, "@bet_amount", betreq.bet_amount);

                    dbhelper.AddParam(cmd, "@bet_type", betreq.bet_type);
                    dbhelper.AddParam(cmd, "@coins_per_credit", betreq.bet_cpc);
                    dbhelper.AddParam(cmd, "@bets_per_line", betreq.bet_bpl);
                    dbhelper.AddParam(cmd, "@betted_lines", betreq.bet_lines);
                    dbhelper.AddParam(cmd, "@game_input", betreq.bet_input);

                    cmd.CommandText = " insert into tbl_bet_record "
                                    + " ( bet_uuid, merchant_code, currency_code, player_id, server_code, table_code, shoe_code, round_number, client_id, front_end, session_id, "
                                    + "   bet_pool, bet_amount, bet_type, coins_per_credit, bets_per_line, betted_lines, game_input, bet_time ) values "
                                    + " ( @bet_uuid, @merchant_code, @currency_code, @player_id, @server_code , @table_code , @shoe_code , @round_number , @client_id , @front_end , @session_id , "
                                    + "   @bet_pool, @bet_amount , @bet_type, @coins_per_credit, @bets_per_line, @betted_lines, @game_input, CURRENT_TIMESTAMP ) "
                                    ;

                    okay = cmd.ExecuteNonQuery() > 0;
                }

                if (okay)
                {
                    using (var cmd = cnn.CreateCommand())
                    {
                        dbhelper.AddParam(cmd, "@bet_uuid", betId);

                        cmd.CommandText = "select bet_uuid, bet_time from tbl_bet_record "
                                            + " where bet_uuid = @bet_uuid "
                                            ;

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                betTimeStr = Convert.ToDateTime(reader["bet_time"]).ToString("yyyy-MM-dd HH:mm:ss");
                            }
                        }

                    }
                }
            }

            if (okay)
            {
                await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                {
                    error_code = 0,
                    bet_uuid = betId,
                    bet_time = betTimeStr
                }));
            }
            else await ctx.Session.Send("Failed to update database");
        }

        [Access(Name = "update-result")]
        public async Task UpdateResult(RequestContext ctx)
        {
            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send("Invalid request");
                return;
            }

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            bool okay = false;
            string betId = req.bet_uuid;
            string settleTimeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            string merchantCode = req.merchant_code.ToString();

            decimal contribution = -1;
            try
            {
                contribution = req.contribution;
                if (contribution < 0) contribution = 0;
            }
            catch
            {
                contribution = -1;
            }

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_CommonMerchantDb))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@bet_uuid", betId);
                    dbhelper.AddParam(cmd, "@pay_amount", req.pay_amount);
                    dbhelper.AddParam(cmd, "@game_result", req.game_result);
                    dbhelper.AddParam(cmd, "@game_output", req.game_output);

                    if (contribution >= 0)
                        dbhelper.AddParam(cmd, "@contribution", contribution);

                    cmd.CommandText = "update tbl_bet_record "
                                        + " set pay_amount = @pay_amount "
                                        + " , game_result = @game_result "
                                        + " , game_output = @game_output "
                                        + (contribution >= 0 ? " , contribution = @contribution " : "")
                                        + " , bet_state = 1 "
                                        + " , settle_time = CURRENT_TIMESTAMP "
                                        + " , update_time = CURRENT_TIMESTAMP "
                                        + " where bet_uuid = @bet_uuid "
                                        ;

                    okay = cmd.ExecuteNonQuery() > 0;
                }

                if (okay)
                {
                    using (var cmd = cnn.CreateCommand())
                    {
                        dbhelper.AddParam(cmd, "@bet_uuid", betId);

                        cmd.CommandText = "select bet_uuid, settle_time from tbl_bet_record "
                                            + " where bet_uuid = @bet_uuid "
                                            ;

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                settleTimeStr = Convert.ToDateTime(reader["settle_time"]).ToString("yyyy-MM-dd HH:mm:ss");
                            }
                        }

                    }
                }
            }

            if (okay) await ctx.Session.Send(betId + "=" + settleTimeStr);
            else await ctx.Session.Send("Failed to update database");
        }

        [Access(Name = "get-round-bets")]
        public async Task GetRoundBets(RequestContext ctx)
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

            dynamic betreq = ctx.JsonHelper.ToJsonObject(reqstr);

            //string merchantCode = betreq.merchant_code.ToString(); // maybe need to get db name with merchant code

            string serverCode = betreq.server_code.ToString();
            string tableCode = betreq.table_code.ToString();
            string shoeCode = betreq.shoe_code.ToString();
            int roundNumber = betreq.round_number;

            List<dynamic> items = new List<dynamic>();

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_CommonMerchantDb)) // should do it in all merchant dbs...
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@server_code", serverCode);
                    dbhelper.AddParam(cmd, "@table_code", tableCode);
                    dbhelper.AddParam(cmd, "@shoe_code", shoeCode);
                    dbhelper.AddParam(cmd, "@round_number", roundNumber);

                    cmd.CommandText = " select bet_uuid, merchant_code, currency_code, player_id, client_id, session_id, "
                                    + " bet_pool, bet_amount, pay_amount, coins_per_credit, bets_per_line, bet_type, game_input, game_result, "
                                    + " bet_state, settle_state, debit_state, credit_state, cancel_state "
                                    + " from tbl_bet_record "
                                    + " where server_code = @server_code "
                                    + " and table_code = @table_code "
                                    + " and shoe_code = @shoe_code "
                                    + " and round_number = @round_number "
                                    ;

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string result = reader["game_result"].ToString();
                            string uuid = reader["bet_uuid"].ToString();
                            string merchant = reader["merchant_code"].ToString();
                            string currency = reader["currency_code"].ToString();
                            string player = reader["player_id"].ToString();

                            string client = reader["client_id"].ToString();
                            string session = reader["session_id"].ToString();

                            int pool = Convert.ToInt32(reader["bet_pool"].ToString());
                            decimal amount = Convert.ToDecimal(reader["bet_amount"].ToString());
                            decimal payout = Convert.ToDecimal(reader["pay_amount"].ToString());

                            decimal cpc = Convert.ToDecimal(reader["coins_per_credit"].ToString());
                            int bpl = Convert.ToInt32(Convert.ToDecimal(reader["bets_per_line"].ToString()));
                            int betType = Convert.ToInt32(reader["bet_type"].ToString());
                            string betInput = reader["game_input"].ToString();

                            dynamic bet = new
                            {
                                bet_uuid = uuid,

                                server_code = serverCode,
                                table_code = tableCode,
                                shoe_code = shoeCode,
                                round_number = roundNumber,

                                bet_type = betType,
                                bet_pool = pool,
                                bet_cpc = cpc,
                                bet_bpl = bpl,

                                pay_amount = payout,
                                bet_amount = amount,

                                bet_input = betInput,
                                game_result = result,

                                merchant_code = merchant,
                                currency_code = currency,
                                player_id = player,
                                client_id = client,
                                session_id = session,

                                bet_state = Convert.ToInt32(reader["bet_state"].ToString()),
                                settle_state = Convert.ToInt32(reader["settle_state"].ToString()),
                                debit_state = Convert.ToInt32(reader["debit_state"].ToString()),
                                credit_state = Convert.ToInt32(reader["credit_state"].ToString()),
                                cancel_state = Convert.ToInt32(reader["cancel_state"].ToString()),
                            };

                            items.Add(bet);

                        }
                    }

                }
            }

            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                error_code = 0,
                error_message = "ok",
                bets = items
            }));
        }

        [Access(Name = "get-player-bets")]
        public async Task GetPlayerBets(RequestContext ctx)
        {
            int maxCount = 1000;

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

            string startDtStr = req.start_dt;
            string endDtStr = req.end_dt;

            string merchantCode = req.merchant_code;
            string currencyCode = req.currency_code;
            string playerId = req.player_id;

            string sqlSelect = " select * "
                             + " from tbl_bet_record ";

            string sqlWhere = " where debit_state = 1 and credit_state = 1 "
                            + " and merchant_code = @merchant_code "
                            + " and currency_code = @currency_code "
                            + " and player_id = @player_id "
                            + " and bet_time >= @start_dt and bet_time <= @end_dt "
                            ;

            //if (merchantCodes.Length > 0) sqlWhere += " and merchant_code in " + merchantCodes;

            string sqlLimit = " limit " + maxCount;

            string sql = sqlSelect + sqlWhere + sqlLimit;

            List<dynamic> items = new List<dynamic>();

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_CommonMerchantDb)) // ...
            {
                using (var cmd = cnn.CreateCommand())
                {
                    cmd.CommandText = sql;

                    dbhelper.AddParam(cmd, "@start_dt", startDtStr);
                    dbhelper.AddParam(cmd, "@end_dt", endDtStr);
                    dbhelper.AddParam(cmd, "@merchant_code", merchantCode);
                    dbhelper.AddParam(cmd, "@currency_code", currencyCode);
                    dbhelper.AddParam(cmd, "@player_id", playerId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string betdt = "";
                            try
                            {
                                var betTime = Convert.ToDateTime(reader["bet_time"]);
                                betdt = betTime.ToString("yyyy-MM-dd HH:mm:ss");
                            }
                            catch { }

                            var item = new
                            {
                                merchant = reader["merchant_code"].ToString(),
                                currency = reader["currency_code"].ToString(),
                                player = reader["player_id"].ToString(),

                                game_id = reader["table_code"].ToString()
                                            + "-" + reader["shoe_code"].ToString()
                                            + "-" + reader["round_number"].ToString(),

                                game_result = reader["game_result"].ToString(),
                                game_output = reader["game_output"].ToString(),

                                bet_id = reader["bet_uuid"].ToString(),
                                bet_time = betdt,

                                bet_type = Convert.ToInt32(reader["bet_type"].ToString()),
                                bet_input = reader["game_input"].ToString(),
                                betted_lines = Convert.ToInt32(reader["betted_lines"].ToString()),

                                bet_amount = Convert.ToDecimal(reader["bet_amount"].ToString()),
                                pay_amount = Convert.ToDecimal(reader["pay_amount"].ToString()),

                            };

                            items.Add(item);

                        }
                    }
                }
            }

            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                error_code = 0,
                error_message = "ok",
                records = items
            }));
        }


    }
}
