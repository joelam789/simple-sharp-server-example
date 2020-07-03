
import {TableState} from './game-state';
import {Messenger} from './messenger';
import * as UI from './ui-messages';

export interface MessageHandler {
    handle(messenger: Messenger, msg: any): boolean;
}

export class TableInfoHandler implements MessageHandler {
    handle(messenger: Messenger, msg: any): boolean {
        if (msg.msg == "table_info") {
            messenger.isRequesting = false;
            let serverTableCodes = [];
            let clientTableCodes = Array.from(messenger.gameState.tableStates.keys());
            if (msg.tables) {

                for (let item of msg.tables) {

                    serverTableCodes.push(item.table);

                    let isOldTableCode = messenger.gameState.tableStates.has(item.table);
                    let table = isOldTableCode ?  messenger.gameState.tableStates.get(item.table) : new TableState();

                    table.tableInfo.gameServer = item.server;
                    table.tableInfo.tableCode = item.table;
                    table.tableInfo.tableName = item.label;
                    table.tableInfo.shoeCode = item.shoe;
                    table.tableInfo.playerCount = parseInt(item.players, 10);
                    table.tableInfo.roundNumber = parseInt(item.round, 10);
                    table.tableInfo.roundState = parseInt(item.state, 10);
                    table.tableInfo.roundStateText = item.status;
                    table.tableInfo.betTimeCountdown = parseInt(item.bet_countdown, 10);
                    table.tableInfo.gameTimeCountdown = parseInt(item.game_countdown, 10);
                    table.tableInfo.nextRoundCountdown = parseInt(item.next_countdown, 10);
                    table.tableInfo.gameResult = item.result;

                    table.tableInfo.gamePlayers = [];
                    table.tableInfo.playerScores = [];
                    table.tableInfo.cardCounts = [];

                    table.tableInfo.currentPlayer = "";
                    table.tableInfo.lastPlayer = "";
                    table.tableInfo.lastPlay = "";

                    let gameOutput = item.output;
                    if (gameOutput && gameOutput.length > 0) {
                        try
                        {
                            let gameData = JSON.parse(gameOutput);

                            if (gameData.players && gameData.players.length > 0)
                                table.tableInfo.gamePlayers.push(...gameData.players);

                            if (gameData.scores && gameData.scores.length > 0)
                                table.tableInfo.playerScores.push(...gameData.scores);

                            if (gameData.current) {
                                table.tableInfo.currentPlayer = gameData.current;
                            }

                            if (gameData.last) {
                                table.tableInfo.lastPlayer = gameData.last;
                            }

                            if (gameData.turns) {
                                table.tableInfo.currentTurns = gameData.turns;
                            } else {
                                table.tableInfo.currentTurns = 0;
                            }
                            
                            if (gameData.play && gameData.play.length > 0) {
                                table.tableInfo.lastPlay = gameData.play;
                            }

                            if (gameData.cards && gameData.cards.length > 0) {
                                for (let i = 0; i < gameData.cards.length; i++)
                                    table.tableInfo.cardCounts.push(parseInt(gameData.cards[i], 10));
                            }

                        } catch (ex) { }
                        
                    }

                    if (messenger.gameState.currentTableCode == table.tableInfo.tableCode) {
                        //console.log(table.tableInfo.currentTurns + " : " + messenger.gameState.currentTurnNumber);
                        messenger.gameState.currentTableName = table.tableInfo.tableName;
                        if (table.tableInfo.currentTurns >= messenger.gameState.currentTurnNumber
                            && table.tableInfo.currentTurns > 0) {
                            messenger.gameState.currentTurnNumber = table.tableInfo.currentTurns;
                            messenger.gameState.currentTurnPlayer = table.tableInfo.currentPlayer;
                        }
                    }

                    if (!isOldTableCode) {
                        messenger.gameState.tableStates.set(table.tableInfo.tableCode, table);
                    }

                }

                for (let tableCode of clientTableCodes) {
                    if (serverTableCodes.indexOf(tableCode) < 0) {
                        messenger.gameState.tableStates.delete(tableCode);
                    }
                }
                
                messenger.dispatch(new UI.TableInfoUpdate(msg.tables.length));
            }
            return true;
        }
        return false;
    }
}

export class ClientInfoHandler implements MessageHandler {
    handle(messenger: Messenger, msg: any): boolean {
        if (msg.msg == "client_info") {
            messenger.isRequesting = false;
            messenger.gameState.frontEndServerName = msg.front_end;
            messenger.gameState.frontEndSessionId = msg.client_token;
            messenger.gameState.frontEndClientId = msg.client_id;
            messenger.dispatch(new UI.ClientInfoUpdate(msg.front_end));
            return true;
        }
        return false;
    }
}

export class ChatMessageHandler implements MessageHandler {
    handle(messenger: Messenger, msg: any): boolean {
        if (msg.msg == "chat_message") {
            messenger.isRequesting = false;
            let data = {
                merchant_code: msg.merchant_code,
                currency_code: msg.currency_code,
                player_id: msg.player_id,
                message: msg.message
            };
            messenger.dispatch(new UI.ChatMessage(data));
            return true;
        }
        return false;
    }
}

export class PlayerBalanceHandler implements MessageHandler {
    handle(messenger: Messenger, msg: any): boolean {
        if (msg.msg == "player_balance") {
            messenger.isRequesting = false;
            if (msg.error_code === 0) {
                //console.log("Get player balance: " + msg.player_balance);
                messenger.gameState.currentPlayerBalance = parseFloat(msg.player_balance);
                messenger.gameState.playerMoney = messenger.gameState.currentPlayerBalance;
                messenger.dispatch(new UI.PlayerMoney(messenger.gameState.playerMoney));
            } else {
                console.log("Failed to get player balance");
                console.log(msg.error_message)
            }
            
            return true;
        }
        return false;
    }
}

export class JoinTableHandler implements MessageHandler {
    handle(messenger: Messenger, msg: any): boolean {
        if (msg.msg == "join_table_reply") {
            messenger.isRequesting = false;
            if (msg.error_code === 0) {
                messenger.gameState.currentTableCode = msg.table_code;
                messenger.dispatch(new UI.JoinTableSuccess(msg.table_code));
            } else {
                messenger.dispatch(new UI.JoinTableError(msg.error_message));
            }
            return true;
        }
        return false;
    }
}

export class LeaveTableHandler implements MessageHandler {
    handle(messenger: Messenger, msg: any): boolean {
        if (msg.msg == "leave_table_reply") {
            messenger.isRequesting = false;
            if (msg.error_code === 0) {
                messenger.gameState.currentTableCode = "";
                messenger.dispatch(new UI.LeaveGameTable(msg.table_code));
            } else {
                console.log("Failed to leave from table " + messenger.gameState.currentTableCode);
                console.log(msg.error_message);
            }
            return true;
        }
        return false;
    }
}

export class PlaceBetHandler implements MessageHandler {
    handle(messenger: Messenger, msg: any): boolean {
        if (msg.msg == "place_bet_reply") {
            messenger.isRequesting = false;
            if (msg.error_code === 0) {
                messenger.gameState.playerMoney = msg.player_balance;
                messenger.dispatch(new UI.PlaceBetSuccess(msg.player_balance));
            } else {
                messenger.dispatch(new UI.PlaceBetError(msg.error_message));
            }
            return true;
        }
        return false;
    }
}

export class StartGameHandler implements MessageHandler {
    handle(messenger: Messenger, msg: any): boolean {
        if (msg.msg == "start_game") {
            messenger.isRequesting = false;
            //messenger.gameState.currentTurnPlayer = "";
            console.log(msg.cards);
            let cards = msg.cards.split(',');
            messenger.gameState.currentTurnNumber = 0;
            messenger.gameState.playerCards = [];
            if (cards && cards.length > 0) messenger.gameState.playerCards.push(...cards);
            console.log(msg.player);
            if (msg.player && msg.player.length > 0) messenger.gameState.currentTurnPlayer = msg.player;
            messenger.dispatch(new UI.UpdatePlayerCards("start"));
            return true;
        }
        return false;
    }
}

export class PlayTurnHandler implements MessageHandler {
    handle(messenger: Messenger, msg: any): boolean {
        if (msg.msg == "play_game_reply") {
            messenger.isRequesting = false;
            if (msg.error_code === 0) {
                //messenger.gameState.currentTurnPlayer = "";
                if (msg.result.turns && msg.result.turns >= messenger.gameState.currentTurnNumber) {
                    messenger.gameState.currentTurnNumber = msg.result.turns;
                    if (msg.result.player && msg.result.player.length > 0) 
                        messenger.gameState.currentTurnPlayer = msg.result.player;
                }
                
                if (msg.result.cards && msg.result.cards.length > 0) {
                    console.log(msg.result.cards);
                    let cards = msg.result.cards.split(',');
                    messenger.gameState.playerCards = [];
                    if (cards && cards.length > 0) messenger.gameState.playerCards.push(...cards);
                } else {
                    console.log("NO CARDS!!");
                    messenger.gameState.playerCards = [];
                }
                
                messenger.dispatch(new UI.PlaySuccess("ok"));

            } else {

                messenger.dispatch(new UI.PlayError(msg.error_message));

            }
            return true;
        }
        return false;
    }
}

export class BetResultHandler implements MessageHandler {
    handle(messenger: Messenger, msg: any): boolean {
        if (msg.msg == "bet_result") {
            messenger.isRequesting = false;
            console.log("Get Bet Results");
            let messages = [];
            for (let item of msg.results) {
                let uimsg = {
                    table: item.table,
                    shoe: item.shoe,
                    round: parseInt(item.round, 10),
                    pool: parseInt(item.pool, 10),
                    bet: parseFloat(item.bet),
                    payout: parseFloat(item.payout),
                    result: item.result
                }
                messages.push(uimsg);
            }
            messenger.dispatch(new UI.BetResultUpdate(messages));
            
            return true;
        }
        return false;
    }
}

