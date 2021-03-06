
import { autoinject } from 'aurelia-framework';
import { EventAggregator } from 'aurelia-event-aggregator';

import { GameState } from './game-state';
import { HttpClient } from './http-client';
import * as Handlers from './handlers';
import * as UI from './ui-messages';

@autoinject()
export class Messenger {

    wsFrontend: any = null;
    isRequesting: boolean = false;

    pendingMessageQueues: Map<string, Array<any>> = new Map<string, Array<any>>();
    pendingMessageQueueSize = 32;

    handlers: Array<Handlers.MessageHandler> = [];

    constructor(public gameState: GameState, public eventChannel: EventAggregator) {
        this.handlers = [new Handlers.TableInfoHandler(), new Handlers.ClientInfoHandler(), new Handlers.BetResultHandler(),
                         new Handlers.JoinTableHandler(), new Handlers.PlaceBetHandler(), new Handlers.StartGameHandler(),
                         new Handlers.PlayTurnHandler(), new Handlers.PlayerBalanceHandler(), new Handlers.LeaveTableHandler(),
                         new Handlers.ChatMessageHandler()
                        ];
    }

    processPendingMessages(pageName: string = null) {
        if (pageName == null) {
            this.pendingMessageQueues.forEach((pendingMessages, page) => {
                while (pendingMessages.length > 0) {
                    let msg = pendingMessages.shift();
                    if (msg != null) this.eventChannel.publish(msg);
                }
            });
        } else {
            let pendingMessages = this.pendingMessageQueues.get(pageName);
            if (pendingMessages == undefined || pendingMessages == null) return;
            else {
                while (pendingMessages.length > 0) {
                    let msg = pendingMessages.shift();
                    if (msg != null) this.eventChannel.publish(msg);
                }
            }
        }
    }

    enqueueMessage(msg: any, pageName: string) {
        if (pageName == undefined || pageName == null || pageName.length <= 0) return;
        let pendingMessages = this.pendingMessageQueues.get(pageName);
        if (pendingMessages == undefined || pendingMessages == null) {
            this.pendingMessageQueues.set(pageName, []);
            pendingMessages = this.pendingMessageQueues.get(pageName);
        }
        pendingMessages.push(msg);
        if (pendingMessages.length > this.pendingMessageQueueSize) {
            while (pendingMessages.length > 0) {
                let msg = pendingMessages.shift();
                if (msg != null) this.eventChannel.publish(msg);
            }
        }
    }

    dispatch(msg: any, pageName: string = null, important: boolean = false) {
        if (pageName != null && pageName.length > 0) {
            if (pageName == this.gameState.currentPage) this.eventChannel.publish(msg);
            else {
                if (important) this.enqueueMessage(msg, pageName);
                else this.eventChannel.publish(msg);
            }
        } else {
            this.eventChannel.publish(msg);
        }
    }

    send(msg: any, url: string = "", needWaitForReply: boolean = false) {
        if (needWaitForReply) this.isRequesting = true;
        if (url && url.length > 0) this.wsFrontend.send(url + "/" + JSON.stringify(msg));
        else this.wsFrontend.send(JSON.stringify(msg));
    }

    getPlayerBalance() {

        //console.log("getPlayerBalance");

        let reqmsg = {
            merchant_code: this.gameState.merchantName,
            currency_code: this.gameState.currencyCode,
            player_id: this.gameState.playerId,
            session_id: this.gameState.currentPlayerSessionId,
            client_token: this.gameState.frontEndSessionId
        };

        this.send(reqmsg, "fes-api/get-player-balance");

    }

    sendChatMessage(message: string) {

        let tableCode = this.gameState.currentTableCode;

        if (!tableCode || tableCode.length <= 0) return;

        let reqmsg = {
            merchant_code: this.gameState.merchantName,
            currency_code: this.gameState.currencyCode,
            player_id: this.gameState.playerId,
            session_id: this.gameState.currentPlayerSessionId,
            client_token: this.gameState.frontEndSessionId,
            table_code: tableCode,
            message: message

        };

        this.send(reqmsg, "fes-table/chat");

    }

    joinTable(tableCode: string) {

        console.log("start to join table - " + tableCode);

        this.gameState.targetTableCode = tableCode;

        let reqmsg = {
            merchant_code: this.gameState.merchantName,
            currency_code: this.gameState.currencyCode,
            player_id: this.gameState.playerId,
            session_id: this.gameState.currentPlayerSessionId,
            client_token: this.gameState.frontEndSessionId,
            table_code: tableCode
        };

        this.send(reqmsg, "fes-table/join-table", true);

    }

    leaveTable() {

        let tableCode = this.gameState.currentTableCode;

        console.log("start to leave from table - " + tableCode);

        this.gameState.targetTableCode = "";

        let reqmsg = {
            merchant_code: this.gameState.merchantName,
            currency_code: this.gameState.currencyCode,
            player_id: this.gameState.playerId,
            session_id: this.gameState.currentPlayerSessionId,
            client_token: this.gameState.frontEndSessionId,
            table_code: tableCode
        };

        this.send(reqmsg, "fes-table/leave-table", true);

    }

    placeGameBet(tableCode: string) {

        console.log("start to place bet - " + tableCode);

        let table = this.gameState.tableStates.get(tableCode);
        if (!table) return;

        console.log("placing bet on " + tableCode + " - " + this.gameState.bettingServerAddress);

        let reqmsg = {
            server_code: table.tableInfo.gameServer,
            table_code: table.tableInfo.tableCode,
            shoe_code: table.tableInfo.shoeCode,
            round_number: table.tableInfo.roundNumber,
            client_id: this.gameState.frontEndClientId,
            front_end: this.gameState.frontEndServerName,

            session_id: this.gameState.currentPlayerSessionId,
            client_token: this.gameState.frontEndSessionId,

            merchant_code: this.gameState.merchantName,
            currency_code: this.gameState.currencyCode,
            player_id: this.gameState.playerId,

            bet_pool: 1,
            bet_amount: 1
        };

        this.send(reqmsg, "fes-table/place-bet", true);

    }

    play(tableCode: string, cards: Array<number>) {

        //console.log("start to play - " + tableCode);

        let table = this.gameState.tableStates.get(tableCode);
        if (!table) return;

        let reqmsg = {

            server_code: table.tableInfo.gameServer,
            table_code: table.tableInfo.tableCode,
            shoe_code: table.tableInfo.shoeCode,
            round_number: table.tableInfo.roundNumber,

            session_id: this.gameState.currentPlayerSessionId,
            client_token: this.gameState.frontEndSessionId,

            merchant_code: this.gameState.merchantName,
            currency_code: this.gameState.currencyCode,
            player_id: this.gameState.playerId,

            game_input: cards.join()
        };

        this.send(reqmsg, "fes-table/play-game", true);

    }

    login() {
        
        console.log("start to login");

        let reqmsg = {
            merchant_code: this.gameState.merchantName,
            currency_code: this.gameState.currencyCode,
            player_id: this.gameState.playerId,
            login_token: this.gameState.password
        };
        
        HttpClient.postJSON(this.gameState.loginServerAddress + "/login/player-login", reqmsg, (reply) => {
            console.log(reply);
            if (reply.error_code != undefined && reply.error_code == 0) {
                this.gameState.frontEndServerAddress = reply.front_end;
                //this.gameState.bettingServerAddress = reply.bet_server;
                this.gameState.currentPlayerSessionId = reply.session_id;
                this.gameState.currentPlayerBalance = reply.player_balance;
                this.gameState.currentCpcOptions = reply.cpc;
                this.gameState.currentBplOptions = reply.bpl;
                this.loginFrontend();
            } else {
                let errormsg = "Failed to login: " + reply.error_message;
                console.log(errormsg);
                this.dispatch(new UI.LoginError(errormsg)); // send error msg to ui ...
            }
        }, (errmsg) => {
            let errormsg = "Failed to login: " + errmsg;
            console.log(errormsg);
            this.dispatch(new UI.LoginError(errormsg)); // send error msg to ui ...
        })
        
    }

    placeBet(tableCode: string, betPool: number, betAmount: number) {
        
        let table = this.gameState.tableStates.get(tableCode);
        if (!table) return;

        console.log("placing bet on " + tableCode + " - " + this.gameState.bettingServerAddress);

        let reqmsg = {
            server_code: table.tableInfo.gameServer,
            table_code: table.tableInfo.tableCode,
            shoe_code: table.tableInfo.shoeCode,
            round_number: table.tableInfo.roundNumber,
            client_id: this.gameState.frontEndClientId,
            front_end: this.gameState.frontEndServerName,

            session_id: this.gameState.currentPlayerSessionId,

            merchant_code: this.gameState.merchantName,
            player_id: this.gameState.playerId,

            bet_pool: betPool,
            bet_amount: betAmount
        };
        
        HttpClient.postJSON(this.gameState.bettingServerAddress + "/accept-bet/accept", reqmsg, (reply) => {
            if (reply.error_code != undefined && reply.error_code == 0) {
                this.dispatch(new UI.PlaceBetSuccess(parseFloat(reply.player_balance)));
            } else {
                let errormsg = "Failed to place bet: " + reply.error_msg;
                this.dispatch(new UI.PlaceBetError(errormsg)); // send error msg to ui ...
            }
        }, (errmsg) => {
            let errormsg = "Failed to place bet: " + errmsg;
            this.dispatch(new UI.PlaceBetError(errormsg)); // send error msg to ui ...
        })
        
    }

    loginFrontend() {

        console.log(this.gameState.frontEndServerAddress);

        if (this.gameState.frontEndServerAddress == null || this.gameState.frontEndServerAddress.length <= 0) return;
        //if (this.gameState.bettingServerAddress == null || this.gameState.bettingServerAddress.length <= 0) return;
        if (this.gameState.currentPlayerSessionId == null || this.gameState.currentPlayerSessionId.length <= 0) return;
        if (this.gameState.merchantName == null || this.gameState.merchantName.length <= 0) return;
        if (this.gameState.currencyCode == null || this.gameState.currencyCode.length <= 0) return;
        if (this.gameState.playerId == null || this.gameState.playerId.length <= 0) return;

        if (this.wsFrontend != null) {
            this.wsFrontend.close();
            this.wsFrontend = null;
        }

        if (this.wsFrontend == null) {

            console.log("Connecting to frontend server: " + this.gameState.frontEndServerAddress);
            this.isRequesting = true;
            this.wsFrontend = new WebSocket(this.gameState.frontEndServerAddress  
                                            + "/" + this.gameState.merchantName 
                                            + "/" + this.gameState.currencyCode 
                                            + "/" + this.gameState.playerId 
                                            + "/" + this.gameState.currentPlayerSessionId);
            this.wsFrontend.onopen = () => {
                console.log("Connected to frontend server - " + this.gameState.frontEndServerAddress);
                this.dispatch(new UI.LoginSuccess("ok"));
                //this.gameState.startAutoCountdown();
            };

            this.wsFrontend.onmessage = (event) => {
                let reply = JSON.parse(event.data);
                this.processFrontendMessage(reply);
            };

            this.wsFrontend.onclose = () => {
                console.log("Disconnected from server - " + this.gameState.frontEndServerAddress);
                //this.gameState.stopAutoCountdown();
            };
        }
    }

    processFrontendMessage(msg: any) {

        for (let handler of this.handlers) {
            if (handler.handle(this, msg)) return;
        }

        // print unknown messages
        console.log(JSON.stringify(msg));
        console.log(msg);

    }

}
