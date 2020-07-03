
import {autoinject, customElement} from 'aurelia-framework';
import {EventAggregator, Subscription} from 'aurelia-event-aggregator';
import {Router} from 'aurelia-router';

import {DialogService} from 'aurelia-dialog';
import {I18N} from 'aurelia-i18n';

//import {ChatState, ChatMessage} from './chat-state';
import {GameState, TableState} from './game-state';
import {Messenger} from './messenger';
import * as UI from './ui-messages';

@autoinject()
export class GameTablePage {

    static readonly MAX_CHAT_HISTORY_LINE_COUNT = 32;

    //chatHistory: Array<ChatMessage> = new Array<ChatMessage>();

    merchantCode: string = "";
    playerCurrency: string = "";
    playerBalance: number = 0;

    playerName: string = "";
    tableCode: string = "";
    tableName: string = "";
    roomName: string = "";

    playerScore: number = 0;

    allowToSendPlayReq: boolean = false;

    currentTurnPlayerName: string = "";
    lastTurnPlayerName: string = "";
    lastTurnPlay: string = "";

    gameTableStateText: string = "";
    gameTableCountdown: number = 30;

    chatMessage: string = "";

    selectedUser: string = "";
    selectedUserIndex: number = 0;

    selectedChatFlags: Array<string> = [];

    users: Array<string> = [];
    scores: { [name: string]: number }  = { };
    counts: { [name: string]: number }  = { };

    subscribers: Array<Subscription> = [];

    playerCards: Array<string> = [];
    lastPlayCards: Array<string> = [];

    alertMessage: string = null;

    chatLines: string = "";

    refreshBalanceTimer: any = null;

    pressKeyCallback = (event) => {
        if (event.which == 13 || event.keyCode == 13) {
            if (this.canSendChatMessage) this.sendChatMessage();
            return false;
        }
        return true;
    };

    constructor(public dialogService: DialogService, public router: Router, 
                public i18n: I18N, public gameState: GameState,
                public messenger: Messenger, public eventChannel: EventAggregator) {

        //this.chatHistory = [];
        this.subscribers = [];
        
    }

    activate(parameters, routeConfig) {

        //this.chatState.currentPage = "chatroom";
        
        //this.changeLang(this.chatState.currentLang);

        //this.userName = this.chatState.userName;
        //this.roomName = this.chatState.currentRoom.name;

        this.playerCurrency = this.gameState.playerCurrency;
        this.playerBalance = this.gameState.currentPlayerBalance;
        this.merchantCode = this.gameState.merchantName;

        this.playerName = this.gameState.merchantName + "|" + this.gameState.currencyCode + "|" + this.gameState.playerId;
        this.tableCode = this.gameState.currentTableCode;
        this.tableName = this.gameState.currentTableName;
        this.roomName = this.gameState.currentTableCode;

        this.gameState.currentPage = "game-table";
        
        this.changeLang(this.gameState.currentLang);

        this.updateTableInfo();

        if (this.refreshBalanceTimer != null) {
            clearInterval(this.refreshBalanceTimer);
            this.refreshBalanceTimer = null;
        }
        this.refreshBalanceTimer = setInterval(() => {
            this.messenger.getPlayerBalance();
        }, 10000);

        window.addEventListener('keypress', this.pressKeyCallback, false);
        
    }

    deactivate() {
        if (this.refreshBalanceTimer != null) {
            clearInterval(this.refreshBalanceTimer);
            this.refreshBalanceTimer = null;
        }
        window.removeEventListener('keypress', this.pressKeyCallback);
    }

    attached() {

        this.subscribers = [];

        this.subscribers.push(this.eventChannel.subscribe(UI.TableInfoUpdate, data => {
            this.updateTableInfo();
        }));

        this.subscribers.push(this.eventChannel.subscribe(UI.PlaceBetSuccess, data => {
            console.log("place bet success: " + data.balance);
        }));

        this.subscribers.push(this.eventChannel.subscribe(UI.PlaceBetError, data => {
            console.log("place bet error: " + data.message);
        }));

        this.subscribers.push(this.eventChannel.subscribe(UI.UpdatePlayerCards, data => {
            this.updatePlayerCards();
        }));

        this.subscribers.push(this.eventChannel.subscribe(UI.PlaySuccess, data => {
            console.log("play success: " + data.message);
            this.updatePlayerCards();
        }));

        this.subscribers.push(this.eventChannel.subscribe(UI.PlayError, data => {
            console.log("play error: " + data.message);
            this.updatePlayerCards();
        }));

        this.subscribers.push(this.eventChannel.subscribe(UI.LeaveGameTable, data => {
            console.log("leave table: " + data.message);
            this.router.navigate("lobby");
        }));

        this.subscribers.push(this.eventChannel.subscribe(UI.ChatMessage, data => {
            //console.log("leave table: " + data.message);
            let playerId = data.message.merchant_code + "|" + data.message.currency_code + "|" + data.message.player_id;
            let chatMsg = data.message.message;
            this.chatLines += "[" + playerId + "] " + chatMsg + "\n";
        }));

        this.subscribers.push(this.eventChannel.subscribe(UI.PlayerMoney, data => this.playerBalance = data.value));

        this.messenger.processPendingMessages("game-table");

    }

    detached() {
        for (let item of this.subscribers) item.dispose();
        this.subscribers = [];
    }

    get isEmptyAlertMessage() {
        return this.alertMessage == null || this.alertMessage.length <= 0;
    }

    get tagForAnyone() {
        //return this.i18n.tr("chatroom.all");
        return this.i18n.tr("game.all");
    }

    get canSendChatMessage() {
        return !this.messenger.isRequesting && !this.router.isNavigating && this.chatMessage.length > 0;
    }

    get canPlaceBet() {
        return !this.messenger.isRequesting && !this.router.isNavigating && this.playerScore == 0;
    }

    get canPlay() {
        return !this.messenger.isRequesting && !this.router.isNavigating 
                && this.currentTurnPlayerName.length > 0 
                && this.currentTurnPlayerName == this.playerName
                && this.gameTableCountdown > 0
                && this.allowToSendPlayReq;
    }

    dismissAlertMessage() {
        this.alertMessage = null;
    }

    updateTableInfo() {
        let currentSelectedUser = "";
        if (this.selectedUserIndex >= 0 && this.selectedUserIndex < this.users.length) {
            currentSelectedUser = this.users[this.selectedUserIndex];
        }
        //this.users = JSON.parse(JSON.stringify(this.chatState.currentRoom.users));

        this.tableName = this.gameState.currentTableName;

        let tableState = this.gameState.tableStates.get(this.gameState.currentTableCode);

        let players = [];
        for (let i = 0; i<tableState.tableInfo.gamePlayers.length; i++) {
            players[i] = tableState.tableInfo.gamePlayers[i];
            this.scores[players[i]] = 0;
            this.counts[players[i]] = 0;
            if (tableState.tableInfo.gamePlayers[i] == this.playerName) this.playerScore = tableState.tableInfo.playerScores[i]
        }
        this.users = [];
        this.users.push(...players);
        if (currentSelectedUser.length > 0) {
            let idx = this.users.indexOf(currentSelectedUser);
            if (idx >= 0) this.selectedUserIndex = idx;
            else this.selectedUserIndex = 0;
        } else this.selectedUserIndex = 0;
        this.selectedUser = this.users[this.selectedUserIndex];

        for (let i = 0; i<tableState.tableInfo.gamePlayers.length; i++) {
            this.scores[tableState.tableInfo.gamePlayers[i]] = tableState.tableInfo.playerScores[i];
            this.counts[tableState.tableInfo.gamePlayers[i]] = tableState.tableInfo.cardCounts[i];
        }

        this.gameTableStateText = tableState.tableInfo.roundStateText;

        if (tableState.tableInfo.betTimeCountdown >= 0 ) this.gameTableCountdown = tableState.tableInfo.betTimeCountdown;
        else if (tableState.tableInfo.gameTimeCountdown >= 0 ) this.gameTableCountdown = tableState.tableInfo.gameTimeCountdown;
        else if (tableState.tableInfo.nextRoundCountdown >= 0 ) this.gameTableCountdown = tableState.tableInfo.nextRoundCountdown;

        this.currentTurnPlayerName = this.gameState.currentTurnPlayer;

        this.lastTurnPlayerName = tableState.tableInfo.lastPlayer;
        this.lastTurnPlay = tableState.tableInfo.lastPlay;
        
        let lastPlayCards = [];
        if (this.lastTurnPlay && this.lastTurnPlay.length > 0) {
            console.log(this.lastTurnPlay);
            let cards = this.lastTurnPlay.split(',');
            lastPlayCards.push(...cards);
            this.lastPlayCards = [];
            this.lastPlayCards.push(...lastPlayCards);
        }
    }

    updatePlayerCards() {

        for (let i = 0; i < 13; i++) {
            let img = document.getElementById('card_' + i);
            if (img) {
                let top = parseInt(img.style.marginTop, 10);
                if (top == 0) continue;
                else img.style.marginTop = "0px";
            }
        }

        this.playerCards = [];
        if (this.gameState.playerCards && this.gameState.playerCards.length > 0) {
            this.playerCards.push(...this.gameState.playerCards);
        }
        
        this.currentTurnPlayerName = this.gameState.currentTurnPlayer;

        this.allowToSendPlayReq = this.playerCards.length > 0 && this.currentTurnPlayerName.length > 0;
        
    }

    exitRoom() {
        this.messenger.leaveTable();
    }

    logout() {
        //this.messenger.logout();
        this.router.navigate("login"); // go to login
    }

    selectUser(idx: number) {
        this.selectedUserIndex = idx;
        this.selectedUser = this.users[this.selectedUserIndex];
    }

    selectCard(idx: number) {
        let img = document.getElementById('card_' + idx);
        if (img) {
            let top = parseInt(img.style.marginTop, 10);
            if (top == 0) img.style.marginTop = (top - 20) + "px";
            else img.style.marginTop = "0px";
        }
    }

    play() {

        this.allowToSendPlayReq = false; // not allow to send it again for now

        let cards = [];
        for (let i = 0; i < 13; i++) {
            let img = document.getElementById('card_' + i);
            if (img) {
                let top = parseInt(img.style.marginTop, 10);
                if (top == 0) continue;
                else cards.push(i);
            }
        }

        this.messenger.play(this.tableCode, cards);

    }

    sendChatMessage() {
        this.messenger.sendChatMessage(this.chatMessage);
        this.chatMessage = "";
    }

    placeBet() {
        this.messenger.placeGameBet(this.tableCode);
    }

    changeLang(lang: string) {
        this.i18n.setLocale(lang)
        .then(() => {
            this.gameState.currentLang = this.i18n.getLocale();
            console.log(this.gameState.currentLang);
        });
    }
}
