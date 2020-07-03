using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MiniTable.SampleMerchant.Service
{
    [Access(Name = "bet")]
    public class BetService
    {
        string m_MerchantDB = "SampleMerchantDB";

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

            string amountStr = req.debit_amount.ToString();
            ctx.Logger.Info("Debit amount: " + amountStr);

            //string mCode = req.merchant_code.ToString();
            //string cCode = req.currency_code.ToString();
            //string pID = req.player_id.ToString();

            //ctx.Logger.Info("m: " + mCode);
            //ctx.Logger.Info("c: " + cCode);
            //ctx.Logger.Info("p: " + pID);

            var done = false;
            var found = false;
            var cancelled = false;
            decimal balance = 0;
            string merchantCode = req.merchant_code.ToString();

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_MerchantDB))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@debit_uuid", req.debit_uuid);

                    cmd.CommandText = "select * from tbl_trans_cancel "
                                        + " where target_uuid = @debit_uuid "
                                        ;

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            cancelled = true;
                        }
                    }

                }

                if (cancelled)
                {
                    await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                    {
                        error_code = -2,
                        error_message = "Transaction has been cancelled"
                    }));
                    return;
                }

                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@debit_uuid", req.debit_uuid);

                    cmd.CommandText = "select * from tbl_trans_debit "
                                        + " where debit_uuid = @debit_uuid "
                                        ;

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            found = true;
                            var success = Convert.ToInt32(reader["debit_success"].ToString());
                            if (success > 0) done = true;
                        }
                    }

                }

                if (found && !done)
                {
                    var trans = cnn.BeginTransaction();

                    using (var cmd = cnn.CreateCommand())
                    {
                        cmd.Transaction = trans;

                        dbhelper.AddParam(cmd, "@debit_uuid", req.debit_uuid);
                        dbhelper.AddParam(cmd, "@bet_uuid", req.bet_uuid);
                        dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                        dbhelper.AddParam(cmd, "@currency_code", req.currency_code);
                        dbhelper.AddParam(cmd, "@player_id", req.player_id);
                        dbhelper.AddParam(cmd, "@round_id", req.round_id);
                        dbhelper.AddParam(cmd, "@bet_pool", req.bet_pool);
                        dbhelper.AddParam(cmd, "@debit_amount", req.debit_amount);
                        dbhelper.AddParam(cmd, "@bet_time", req.bet_time);
                        dbhelper.AddParam(cmd, "@debit_success", done ? 1 : 0);

                        cmd.CommandText = "update tbl_trans_debit "
                                        + " set bet_uuid = @bet_uuid , merchant_code = @merchant_code , currency_code = @currency_code , player_id = @player_id ,"
                                        + " round_id = @round_id , bet_pool = @bet_pool , debit_amount = @debit_amount , "
                                        + " debit_success = @debit_success, bet_time = @bet_time , update_time = NOW() "
                                        + " where debit_uuid = @debit_uuid and is_cancelled = 0 "
                                        ;

                        done = cmd.ExecuteNonQuery() > 0;
                    }

                    if (done)
                    {
                        using (var cmd = cnn.CreateCommand())
                        {
                            cmd.Transaction = trans;

                            dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                            dbhelper.AddParam(cmd, "@currency_code", req.currency_code);
                            dbhelper.AddParam(cmd, "@player_id", req.player_id);
                            dbhelper.AddParam(cmd, "@debit_amount", req.debit_amount);

                            cmd.CommandText = "update tbl_player_balance "
                                                + " set player_balance = player_balance - @debit_amount "
                                                + " where merchant_code = @merchant_code "
                                                + " and currency_code = @currency_code "
                                                + " and player_id = @player_id "
                                                + " and player_balance >= @debit_amount "
                                                ;

                            done = done && cmd.ExecuteNonQuery() > 0;
                        }

                        if (done)
                        {
                            using (var cmd = cnn.CreateCommand())
                            {
                                cmd.Transaction = trans;

                                dbhelper.AddParam(cmd, "@debit_uuid", req.debit_uuid);
                                dbhelper.AddParam(cmd, "@bet_uuid", req.bet_uuid);
                                dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                                dbhelper.AddParam(cmd, "@currency_code", req.currency_code);
                                dbhelper.AddParam(cmd, "@player_id", req.player_id);
                                dbhelper.AddParam(cmd, "@round_id", req.round_id);
                                dbhelper.AddParam(cmd, "@bet_pool", req.bet_pool);
                                dbhelper.AddParam(cmd, "@debit_amount", req.debit_amount);
                                dbhelper.AddParam(cmd, "@bet_time", req.bet_time);

                                cmd.CommandText = "update tbl_bet_record "
                                                + " set debit_uuid = @debit_uuid , merchant_code = @merchant_code , currency_code = @currency_code , player_id = @player_id ,"
                                                + " round_id = @round_id , bet_pool = @bet_pool , bet_amount = @debit_amount , "
                                                + " bet_time = @bet_time , update_time = NOW() "
                                                + " where bet_uuid = @bet_uuid "
                                                ;

                                cmd.ExecuteNonQuery();
                            }
                        }
                    }

                    if (found && done) trans.Commit();
                    else trans.Rollback();
                }
                else if (!found && !done)
                {
                    var trans = cnn.BeginTransaction();

                    using (var cmd = cnn.CreateCommand())
                    {
                        cmd.Transaction = trans;

                        dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                        dbhelper.AddParam(cmd, "@currency_code", req.currency_code);
                        dbhelper.AddParam(cmd, "@player_id", req.player_id);
                        dbhelper.AddParam(cmd, "@debit_amount", req.debit_amount);

                        cmd.CommandText = "update tbl_player_balance "
                                            + " set player_balance = player_balance - @debit_amount "
                                            + " where merchant_code = @merchant_code "
                                            + " and currency_code = @currency_code "
                                            + " and player_id = @player_id "
                                            + " and player_balance >= @debit_amount "
                                            ;

                        done = cmd.ExecuteNonQuery() > 0;
                    }

                    if (done)
                    {
                        using (var cmd = cnn.CreateCommand())
                        {
                            cmd.Transaction = trans;

                            dbhelper.AddParam(cmd, "@debit_uuid", req.debit_uuid);
                            dbhelper.AddParam(cmd, "@bet_uuid", req.bet_uuid);
                            dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                            dbhelper.AddParam(cmd, "@currency_code", req.currency_code);
                            dbhelper.AddParam(cmd, "@player_id", req.player_id);
                            dbhelper.AddParam(cmd, "@round_id", req.round_id);
                            dbhelper.AddParam(cmd, "@bet_pool", req.bet_pool);
                            dbhelper.AddParam(cmd, "@debit_amount", req.debit_amount);
                            dbhelper.AddParam(cmd, "@bet_time", req.bet_time);
                            dbhelper.AddParam(cmd, "@debit_success", done ? 1 : 0);

                            cmd.CommandText = "insert into tbl_trans_debit (debit_uuid, bet_uuid, merchant_code, currency_code, player_id, round_id, "
                                            + " bet_pool, debit_amount, debit_success, bet_time, update_time) "
                                            + " select @debit_uuid , @bet_uuid , @merchant_code , @currency_code , @player_id , @round_id , "
                                            + " @bet_pool , @debit_amount , @debit_success , @bet_time, NOW() from dual "
                                            + " where not exists ( select trans_uuid from tbl_trans_cancel where target_uuid = @debit_uuid ) "
                                            ;

                            found = cmd.ExecuteNonQuery() > 0;
                        }
                    }

                    if (found)
                    {
                        using (var cmd = cnn.CreateCommand())
                        {
                            cmd.Transaction = trans;

                            dbhelper.AddParam(cmd, "@debit_uuid", req.debit_uuid);
                            dbhelper.AddParam(cmd, "@bet_uuid", req.bet_uuid);
                            dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                            dbhelper.AddParam(cmd, "@currency_code", req.currency_code);
                            dbhelper.AddParam(cmd, "@player_id", req.player_id);
                            dbhelper.AddParam(cmd, "@round_id", req.round_id);
                            dbhelper.AddParam(cmd, "@bet_pool", req.bet_pool);
                            dbhelper.AddParam(cmd, "@debit_amount", done ? req.debit_amount : 0);
                            dbhelper.AddParam(cmd, "@bet_time", req.bet_time);

                            cmd.CommandText = "insert into tbl_bet_record "
                                            + " set bet_uuid = @bet_uuid, debit_uuid = @debit_uuid , merchant_code = @merchant_code , currency_code = @currency_code , player_id = @player_id ,"
                                            + " round_id = @round_id , bet_pool = @bet_pool , bet_amount = @debit_amount , "
                                            + " bet_time = @bet_time , update_time = NOW() "
                                            ;

                            cmd.ExecuteNonQuery();
                        }
                    }

                    if (found && done) trans.Commit();
                    else trans.Rollback();
                }

                if (found && done)
                {
                    using (var cmd = cnn.CreateCommand())
                    {
                        dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                        dbhelper.AddParam(cmd, "@currency_code", req.currency_code);
                        dbhelper.AddParam(cmd, "@player_id", req.player_id);

                        cmd.CommandText = "select * from tbl_player_balance "
                                            + " where merchant_code = @merchant_code "
                                            + " and currency_code = @currency_code "
                                            + " and player_id = @player_id "
                                            ;

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                balance = Convert.ToDecimal(reader["player_balance"].ToString());
                            }
                        }
                    }
                }
            }

            ctx.Logger.Info("Debit done");

            var reply = new
            {
                req.merchant_code,
                req.player_id,
                player_balance = balance,
                error_code = found && done ? 0 : -1
            };
            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(reply));
        }

        [Access(Name = "cancel-debit")]
        public async Task CancelDebit(RequestContext ctx)
        {
            ctx.Logger.Info("Cancel debit ...");

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

            string debitId = req.debit_uuid.ToString();
            ctx.Logger.Info("Debit to cancel: " + debitId);

            var cancelDone = false;

            var done = false;
            var found = false;
            var cancelled = false;

            decimal balance = 0;

            string merchantCode = "";
            string currencyCode = "";
            string playerId = "";
            string debitUuid = "";

            decimal debitAmount = 0;

            string amountStr = req.amount.ToString();
            decimal targetAmount = Convert.ToDecimal(amountStr);

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_MerchantDB))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@debit_uuid", req.debit_uuid);

                    cmd.CommandText = "select * from tbl_trans_cancel "
                                        + " where target_uuid = @debit_uuid "
                                        ;

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            cancelled = true;
                        }
                    }

                }

                if (cancelled)
                {
                    await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                    {
                        error_code = 0,
                        error_message = "Transaction has been cancelled"
                    }));
                    return;
                }

                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@debit_uuid", req.debit_uuid);

                    cmd.CommandText = "select * from tbl_trans_debit "
                                        + " where debit_uuid = @debit_uuid "
                                        ;

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            found = true;
                            var success = Convert.ToInt32(reader["debit_success"].ToString());
                            if (success > 0) done = true;

                            var isCancelled = Convert.ToInt32(reader["is_cancelled"].ToString());
                            if (isCancelled > 0) cancelled = true;

                            debitUuid = reader["debit_uuid"].ToString();
                            merchantCode = reader["merchant_code"].ToString();
                            currencyCode = reader["currency_code"].ToString();
                            playerId = reader["player_id"].ToString();

                            debitAmount = Convert.ToDecimal(reader["debit_amount"].ToString());
                        }
                    }

                }

                if (cancelled)
                {
                    await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                    {
                        error_code = 0,
                        error_message = "Transaction has been cancelled"
                    }));
                    return;
                }

                if (found && debitAmount != targetAmount)
                {
                    await ctx.Session.Send(ctx.JsonHelper.ToJsonString(new
                    {
                        error_code = -2,
                        error_message = "Wrong debit amount to cancel"
                    }));
                    return;
                }

                var addedNew = false;
                var updatedState = false;
                var updatedBalance = false;

                var trans = cnn.BeginTransaction();

                using (var cmd = cnn.CreateCommand())
                {
                    cmd.Transaction = trans;

                    dbhelper.AddParam(cmd, "@target_uuid", req.debit_uuid);
                    dbhelper.AddParam(cmd, "@trans_uuid", req.trans_uuid);
                    dbhelper.AddParam(cmd, "@amount", req.amount);

                    cmd.CommandText = "insert into tbl_trans_cancel (trans_uuid, target_uuid, amount, cancel_type, update_time) "
                                    + " select @trans_uuid , @target_uuid , @amount, 0, NOW() from dual "
                                    + " where not exists ( select debit_uuid from tbl_trans_debit where debit_uuid = @target_uuid and is_cancelled > 0 ) "
                                    ;

                    addedNew = cmd.ExecuteNonQuery() > 0;
                }

                if (addedNew && found)
                {
                    using (var cmd = cnn.CreateCommand())
                    {
                        cmd.Transaction = trans;

                        dbhelper.AddParam(cmd, "@target_uuid", req.debit_uuid);

                        cmd.CommandText = "update tbl_trans_debit "
                                        + " set is_cancelled = 1 , update_time = NOW() , cancel_time = NOW() "
                                        + " where debit_uuid = @target_uuid and is_cancelled = 0 "
                                        ;

                        updatedState = cmd.ExecuteNonQuery() > 0;
                    }
                }

                if (addedNew && found && done)
                {
                    using (var cmd = cnn.CreateCommand())
                    {
                        cmd.Transaction = trans;

                        dbhelper.AddParam(cmd, "@merchant_code", merchantCode);
                        dbhelper.AddParam(cmd, "@currency_code", currencyCode);
                        dbhelper.AddParam(cmd, "@player_id", playerId);
                        dbhelper.AddParam(cmd, "@debit_amount", debitAmount);

                        cmd.CommandText = "update tbl_player_balance "
                                            + " set player_balance = player_balance + @debit_amount "
                                            + " where merchant_code = @merchant_code "
                                            + " and currency_code = @currency_code "
                                            + " and player_id = @player_id "
                                            + " and player_balance >= 0 "
                                            ;

                        updatedBalance = cmd.ExecuteNonQuery() > 0;
                    }
                }



                if (!addedNew) cancelDone = false;
                else
                {
                    if (!found) cancelDone = true;
                    else if (found && !done && updatedState) cancelDone = true;
                    else if (found && done && updatedState && updatedBalance) cancelDone = true;
                    else cancelDone = false;
                }

                if (cancelDone) trans.Commit();
                else trans.Rollback();
            }

            ctx.Logger.Info("Cancel debit done");

            var reply = new
            {
                error_code = cancelDone ? 0 : -3
            };
            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(reply));
        }

        [Access(Name = "credit-for-settling-bet")]
        public async Task CreditForBetting(RequestContext ctx)
        {
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

            string creditAmountStr = req.credit_amount.ToString();
            ctx.Logger.Info("Credit amount: " + creditAmountStr);

            string creditUUID = req.credit_uuid.ToString();
            //string mCode = req.merchant_code.ToString();
            //string cCode = req.currency_code.ToString();
            //string pID = req.player_id.ToString();

            //ctx.Logger.Info("m: " + mCode);
            //ctx.Logger.Info("c: " + cCode);
            //ctx.Logger.Info("p: " + pID);

            var done = false;
            var found = false;
            decimal balance = 0;
            string merchantCode = req.merchant_code.ToString();

            var dbhelper = ctx.DataHelper;
            using (var cnn = dbhelper.OpenDatabase(m_MerchantDB))
            {
                using (var cmd = cnn.CreateCommand())
                {
                    dbhelper.AddParam(cmd, "@credit_uuid", req.credit_uuid);

                    cmd.CommandText = "select * from tbl_trans_credit "
                                        + " where credit_uuid = @credit_uuid "
                                        ;

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ctx.Logger.Info("Already existed: " + creditUUID);

                            found = true;
                            var success = Convert.ToInt32(reader["credit_success"].ToString());
                            if (success > 0) done = true;
                        }
                    }

                }

                if (found && !done)
                {
                    var trans = cnn.BeginTransaction();

                    using (var cmd = cnn.CreateCommand())
                    {
                        cmd.Transaction = trans;

                        dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                        dbhelper.AddParam(cmd, "@currency_code", req.currency_code);
                        dbhelper.AddParam(cmd, "@player_id", req.player_id);
                        dbhelper.AddParam(cmd, "@credit_amount", req.credit_amount);

                        cmd.CommandText = "update tbl_player_balance "
                                            + " set player_balance = player_balance + @credit_amount "
                                            + " where merchant_code = @merchant_code "
                                            + " and currency_code = @currency_code "
                                            + " and player_id = @player_id "
                                            ;

                        done = cmd.ExecuteNonQuery() > 0;
                    }

                    if (done)
                    {
                        using (var cmd = cnn.CreateCommand())
                        {
                            cmd.Transaction = trans;

                            dbhelper.AddParam(cmd, "@credit_uuid", req.credit_uuid);
                            dbhelper.AddParam(cmd, "@bet_uuid", req.bet_uuid);
                            dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                            dbhelper.AddParam(cmd, "@currency_code", req.currency_code);
                            dbhelper.AddParam(cmd, "@player_id", req.player_id);
                            dbhelper.AddParam(cmd, "@round_id", req.round_id);
                            dbhelper.AddParam(cmd, "@bet_pool", req.bet_pool);
                            dbhelper.AddParam(cmd, "@credit_amount", req.credit_amount);
                            dbhelper.AddParam(cmd, "@bet_settle_time", req.bet_settle_time);
                            dbhelper.AddParam(cmd, "@credit_success", done ? 1 : 0);

                            cmd.CommandText = "update tbl_trans_credit "
                                            + " set bet_uuid = @bet_uuid , merchant_code = @merchant_code , currency_code = @currency_code , player_id = @player_id ,"
                                            + " round_id = @round_id , bet_pool = @bet_pool , credit_amount = @credit_amount , "
                                            + " credit_success = @credit_success , bet_settle_time = @bet_settle_time , update_time = NOW() "
                                            + " where credit_uuid = @credit_uuid "
                                            ;

                            cmd.ExecuteNonQuery();
                        }

                        using (var cmd = cnn.CreateCommand())
                        {
                            cmd.Transaction = trans;

                            dbhelper.AddParam(cmd, "@credit_uuid", req.credit_uuid);
                            dbhelper.AddParam(cmd, "@bet_uuid", req.bet_uuid);
                            dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                            dbhelper.AddParam(cmd, "@currency_code", req.currency_code);
                            dbhelper.AddParam(cmd, "@player_id", req.player_id);
                            dbhelper.AddParam(cmd, "@round_id", req.round_id);
                            dbhelper.AddParam(cmd, "@bet_pool", req.bet_pool);
                            dbhelper.AddParam(cmd, "@credit_amount", req.credit_amount);
                            dbhelper.AddParam(cmd, "@settle_time", req.bet_settle_time);

                            cmd.CommandText = "update tbl_bet_record "
                                            + " set credit_uuid = @credit_uuid , merchant_code = @merchant_code , currency_code = @currency_code , player_id = @player_id ,"
                                            + " round_id = @round_id , bet_pool = @bet_pool , pay_amount = @credit_amount , "
                                            + " settle_time = @settle_time , update_time = NOW() "
                                            + " where bet_uuid = @bet_uuid "
                                            ;

                            cmd.ExecuteNonQuery();
                        }
                    }

                    trans.Commit();
                }
                else if (!found && !done)
                {

                    ctx.Logger.Info("just new: " + creditUUID);

                    var trans = cnn.BeginTransaction();

                    using (var cmd = cnn.CreateCommand())
                    {
                        cmd.Transaction = trans;

                        dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                        dbhelper.AddParam(cmd, "@currency_code", req.currency_code);
                        dbhelper.AddParam(cmd, "@player_id", req.player_id);
                        dbhelper.AddParam(cmd, "@credit_amount", req.credit_amount);

                        cmd.CommandText = "update tbl_player_balance "
                                            + " set player_balance = player_balance + @credit_amount "
                                            + " where merchant_code = @merchant_code "
                                            + " and currency_code = @currency_code "
                                            + " and player_id = @player_id "
                                            ;

                        done = cmd.ExecuteNonQuery() > 0;
                    }

                    using (var cmd = cnn.CreateCommand())
                    {
                        cmd.Transaction = trans;

                        dbhelper.AddParam(cmd, "@credit_uuid", req.credit_uuid);
                        dbhelper.AddParam(cmd, "@bet_uuid", req.bet_uuid);
                        dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                        dbhelper.AddParam(cmd, "@currency_code", req.currency_code);
                        dbhelper.AddParam(cmd, "@player_id", req.player_id);
                        dbhelper.AddParam(cmd, "@round_id", req.round_id);
                        dbhelper.AddParam(cmd, "@bet_pool", req.bet_pool);
                        dbhelper.AddParam(cmd, "@credit_amount", req.credit_amount);
                        dbhelper.AddParam(cmd, "@bet_settle_time", req.bet_settle_time);
                        dbhelper.AddParam(cmd, "@credit_success", done ? 1 : 0);

                        cmd.CommandText = "insert into tbl_trans_credit "
                                        + " set credit_uuid = @credit_uuid , bet_uuid = @bet_uuid , merchant_code = @merchant_code , currency_code = @currency_code , player_id = @player_id ,"
                                        + " round_id = @round_id , bet_pool = @bet_pool , credit_amount = @credit_amount , "
                                        + " credit_success = @credit_success, bet_settle_time = @bet_settle_time , update_time = NOW() "
                                        ;

                        found = cmd.ExecuteNonQuery() > 0;
                    }

                    using (var cmd = cnn.CreateCommand())
                    {
                        cmd.Transaction = trans;

                        dbhelper.AddParam(cmd, "@credit_uuid", req.credit_uuid);
                        dbhelper.AddParam(cmd, "@bet_uuid", req.bet_uuid);
                        dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                        dbhelper.AddParam(cmd, "@currency_code", req.currency_code);
                        dbhelper.AddParam(cmd, "@player_id", req.player_id);
                        dbhelper.AddParam(cmd, "@round_id", req.round_id);
                        dbhelper.AddParam(cmd, "@bet_pool", req.bet_pool);
                        dbhelper.AddParam(cmd, "@credit_amount", done ? req.credit_amount : 0);
                        dbhelper.AddParam(cmd, "@settle_time", req.bet_settle_time);

                        cmd.CommandText = "update tbl_bet_record "
                                        + " set credit_uuid = @credit_uuid , merchant_code = @merchant_code , currency_code = @currency_code , player_id = @player_id ,"
                                        + " round_id = @round_id , bet_pool = @bet_pool , pay_amount = @credit_amount , "
                                        + " settle_time = @settle_time , update_time = NOW() "
                                        + " where bet_uuid = @bet_uuid "
                                        ;

                        cmd.ExecuteNonQuery();
                    }

                    trans.Commit();
                }

                if (found && done)
                {
                    ctx.Logger.Info("done: " + creditUUID);

                    using (var cmd = cnn.CreateCommand())
                    {
                        dbhelper.AddParam(cmd, "@merchant_code", req.merchant_code);
                        dbhelper.AddParam(cmd, "@currency_code", req.currency_code);
                        dbhelper.AddParam(cmd, "@player_id", req.player_id);

                        cmd.CommandText = "select * from tbl_player_balance "
                                            + " where merchant_code = @merchant_code "
                                            + " and currency_code = @currency_code "
                                            + " and player_id = @player_id "
                                            ;

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                balance = Convert.ToDecimal(reader["player_balance"].ToString());
                            }
                        }
                    }
                }
            }

            ctx.Logger.Info("Credit done: " + balance);

            var reply = new
            {
                req.merchant_code,
                req.player_id,
                player_balance = balance,
                error_code = found && done ? 0 : -1
            };
            await ctx.Session.Send(ctx.JsonHelper.ToJsonString(reply));
        }

        //[Access(Name = "cancel-credit")]
        //public async Task CancelCredit(RequestContext ctx)
        //{
        //    // ...
        //}

    }
}
