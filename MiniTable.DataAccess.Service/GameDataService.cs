using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.DataAccess.Service
{
    [Access(Name = "game-data", IsPublic = false)]
    public class GameDataService
    {
        private string m_MainDatabase = "MainDB";

        [Access(Name = "on-load", IsLocal = true)]
        public async Task<string> Load(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            await Task.Delay(50);
            node.GetLogger().Info(this.GetType().Name + " service started");
            await Task.Delay(50);

            return "";
        }

        [Access(Name = "save-record")]
        public async Task SaveGameRecord(RequestContext ctx)
        {
            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send("Invalid request");
                return;
            }

            bool okay = false;

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_MainDatabase))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@server_code", req.server);
                    dbhelper.AddParam(cmd, "@table_code", req.table);
                    dbhelper.AddParam(cmd, "@shoe_code", req.shoe);
                    dbhelper.AddParam(cmd, "@round_number", req.round);
                    dbhelper.AddParam(cmd, "@round_state", req.state);
                    dbhelper.AddParam(cmd, "@game_output", req.output);
                    dbhelper.AddParam(cmd, "@game_result", req.result);
                    dbhelper.AddParam(cmd, "@round_start_time", req.starttime);
                    dbhelper.AddParam(cmd, "@last_update_time", req.updatetime);

                    cmd.CommandText = " insert into tbl_game_record "
                                            + " ( server_code, table_code, shoe_code, round_number, round_state,  "
                                            + "   game_output, game_result, round_start_time, last_update_time ) values "
                                            + " ( @server_code , @table_code , @shoe_code , @round_number , @round_state , "
                                            + "   @game_output , @game_result, @round_start_time, @last_update_time ) "
                                            ;

                    okay = cmd.ExecuteNonQuery() > 0;
                }
            }

            if (okay) await ctx.Session.Send("ok");
            else await ctx.Session.Send("Failed to update database");
        }

        [Access(Name = "update-result")]
        public async Task UpdateGameResult(RequestContext ctx)
        {
            string reqstr = ctx.Data.ToString();
            if (reqstr.Trim().Length <= 0)
            {
                await ctx.Session.Send("Invalid request");
                return;
            }

            bool okay = false;

            dynamic req = ctx.JsonHelper.ToJsonObject(reqstr);

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_MainDatabase))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@server_code", req.server);
                    dbhelper.AddParam(cmd, "@table_code", req.table);
                    dbhelper.AddParam(cmd, "@shoe_code", req.shoe);
                    dbhelper.AddParam(cmd, "@round_number", req.round);
                    dbhelper.AddParam(cmd, "@round_state", req.state);
                    dbhelper.AddParam(cmd, "@game_output", req.output);
                    dbhelper.AddParam(cmd, "@game_result", req.result);
                    dbhelper.AddParam(cmd, "@last_update_time", req.updatetime);

                    cmd.CommandText = "update tbl_game_record "
                                            + " set round_state = @round_state "
                                            + " , game_output = @game_output "
                                            + " , game_result = @game_result "
                                            + ", last_update_time = @last_update_time "
                                            + " where server_code = @server_code and table_code = @table_code "
                                            + " and shoe_code = @shoe_code and round_number = @round_number "
                                            ;
                    okay = cmd.ExecuteNonQuery() > 0;
                }
            }

            if (okay) await ctx.Session.Send("ok");
            else await ctx.Session.Send("Failed to update database");
        }

        [Access(Name = "query-game-results")]
        public async Task QueryGameResults(RequestContext ctx)
        {
            int maxCount = 1000;
            int dtColumnIndex = 6;

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

            string sqlSelect = " select game_id, server_code, table_code, shoe_code, round_number, game_result, round_start_time "
                             + " from tbl_game_record ";

            string sqlWhere = " where round_state >= 7 "
                            + " and round_start_time >= @start_dt and round_start_time <= @end_dt "
                            ;

            string sqlLimit = " limit " + maxCount;

            string sql = sqlSelect + sqlWhere + sqlLimit;

            Dictionary<string, dynamic> results = new Dictionary<string, dynamic>();

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_MainDatabase))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    cmd.CommandText = sql;

                    dbhelper.AddParam(cmd, "@start_dt", startDtStr);
                    dbhelper.AddParam(cmd, "@end_dt", endDtStr);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string gamedt = "";
                            try
                            {
                                var gameTime = reader.GetDateTime(dtColumnIndex);
                                gamedt = gameTime.ToString("yyyy-MM-dd HH:mm:ss");
                            }
                            catch { }

                            var item = new
                            {
                                record_id = reader["game_id"].ToString(),

                                game_id = reader["server_code"].ToString()
                                        + "-" + reader["table_code"].ToString()
                                        + "-" + reader["shoe_code"].ToString()
                                        + "-" + reader["round_number"].ToString(),

                                game_result = reader["game_result"].ToString(),
                                game_time = gamedt
                            };

                            if (results.ContainsKey(item.record_id)) results.Remove(item.record_id);
                            results.Add(item.record_id, item);

                        }
                    }
                }
            }

            List<dynamic> list = new List<dynamic>();
            foreach (var item in results) list.Add(item.Value);

            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                error_code = 0,
                game_results = list
            }));
        }

        [Access(Name = "get-table-setting")]
        public async Task GetGameTableSetting(RequestContext ctx)
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

            int settingId = 0;
            int maintainState = 0;

            string serverCode = req.server_code.ToString();
            string tableCode = req.table_code.ToString();

            string sqlSelect = " select * "
                             + " from tbl_game_setting ";

            string sqlWhere = " where server_code = @server_code "
                            + " and table_code = @table_code "
                            ;

            string sqlLimit = " limit 1 ";

            string sql = sqlSelect + sqlWhere + sqlLimit;

            //Dictionary<string, dynamic> results = new Dictionary<string, dynamic>();

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_MainDatabase))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    cmd.CommandText = sql;

                    dbhelper.AddParam(cmd, "@server_code", serverCode);
                    dbhelper.AddParam(cmd, "@table_code", tableCode);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            settingId = Convert.ToInt32(reader["setting_id"].ToString());
                            maintainState = Convert.ToInt32(reader["is_maintained"].ToString());
                        }
                    }
                }
            }

            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                error_code = 0,
                server_code = serverCode,
                table_code = tableCode,
                setting = new
                {
                    setting_id = settingId,
                    is_maintained = maintainState
                }
            }));
        }

        [Access(Name = "get-last-game")]
        public async Task GetLastGame(RequestContext ctx)
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

            string serverCode = req.server_code.ToString();
            string tableCode = req.table_code.ToString();

            string shoeCode = "";
            string gameResult = "";
            string gameOutput = "";
            int roundNumber = 0;
            int roundState = 0;

            string sqlSelect = " select * "
                             + " from tbl_game_record ";

            string sqlWhere = " where server_code = @server_code "
                            + " and table_code = @table_code "
                            ;
            string sqlOrder = " order by game_id desc ";
            string sqlLimit = " limit 1 ";

            string sql = sqlSelect + sqlWhere + sqlOrder + sqlLimit;

            //Dictionary<string, dynamic> results = new Dictionary<string, dynamic>();

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_MainDatabase))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    cmd.CommandText = sql;

                    dbhelper.AddParam(cmd, "@server_code", serverCode);
                    dbhelper.AddParam(cmd, "@table_code", tableCode);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            shoeCode = reader["shoe_code"].ToString();
                            gameResult = reader["game_result"].ToString();
                            gameOutput = reader["game_output"].ToString();
                            roundNumber = Convert.ToInt32(reader["round_number"].ToString());
                            roundState = Convert.ToInt32(reader["round_state"].ToString());
                        }
                    }
                }

            } // end db

            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
            {
                error_code = 0,
                server_code = serverCode,
                table_code = tableCode,
                game = new
                {
                    shoe_code = shoeCode,
                    game_output = gameOutput,
                    game_result = gameResult,
                    round_number = roundNumber,
                    round_state = roundState
                }
            }));
        }


    }
}
