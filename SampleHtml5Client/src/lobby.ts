
import {autoinject, customElement} from 'aurelia-framework';
import {EventAggregator, Subscription} from 'aurelia-event-aggregator';
import {Router} from 'aurelia-router';

import {DialogService} from 'aurelia-dialog';
import {I18N} from 'aurelia-i18n';

import {GameState, TableState, TableSummaryInfo} from './game-state';
import {Messenger} from './messenger';

import * as UI from './ui-messages';

import {BetLimitDialog} from './bet-limit-popup';
import {HttpClient} from './http-client';
import {App} from './app';

@autoinject()
export class LobbyPage {

    merchantCode: string = "";
    playerName: string = "";
    playerCurrency: string = "";
    playerBalance: number = 0;

    alertMessage: string = null;

    selectedTableCode: string = "";
    //betLimitOptions: Array<string> = [];

    tables: Array<TableSummaryInfo> = [];
    workingTables: Array<TableSummaryInfo> = [];
    minVisibleTableCount: number = 0;

    countdownTotalSeconds: number = 30;

    countdownTimer: any = null;
    refreshBalanceTimer: any = null;

    subscribers: Array<Subscription> = [];

    constructor(public dialogService: DialogService, public router: Router, 
                public i18n: I18N, public gameState: GameState, 
                public messenger: Messenger,
                public eventChannel: EventAggregator) {

        this.subscribers = [];

    }

    attached() {
        this.subscribers = [];
        this.subscribers.push(this.eventChannel.subscribe(UI.LoginInfo, data => {
            this.playerCurrency = this.gameState.playerCurrency;
            this.playerBalance = this.gameState.currentPlayerBalance;
            this.merchantCode = this.gameState.merchantName;
            this.playerName = this.gameState.playerId;
        }));
        this.subscribers.push(this.eventChannel.subscribe(UI.TableInfoUpdate, data => {
            this.tables = [];
            this.workingTables = [];
            let tableCodes = Array.from(this.gameState.tableStates.keys());
            for (let tableCode of tableCodes) {
                let tableState = this.gameState.tableStates.get(tableCode);
                let tableInfo = new TableSummaryInfo();
                tableInfo.tableCode = tableState.tableInfo.tableCode;
                tableInfo.tableName = tableState.tableInfo.tableName;
                tableInfo.playerCount = tableState.tableInfo.playerCount;
                tableInfo.countdown = tableState.tableInfo.betTimeCountdown;
                tableInfo.isOpen = tableState.tableInfo.roundState > 1;
                tableInfo.serverCode = tableState.tableInfo.gameServer;
                tableInfo.players = ["?", "?", "?", "?"];
                for (let i = 0; i<tableState.tableInfo.gamePlayers.length; i++) {
                    tableInfo.players[i] = tableState.tableInfo.gamePlayers[i];
                }
                tableInfo.state = TableState.SIMPLE_GAME_STATES[tableState.tableInfo.roundState];
                if (tableInfo.isOpen) {
                    this.workingTables.push(tableInfo);
                } else {
                    // ...
                }
                this.tables.push(tableInfo);
            }
        }));
        this.subscribers.push(this.eventChannel.subscribe(UI.TableStateUpdate, data => {
            // ...
        }));
        this.subscribers.push(this.eventChannel.subscribe(UI.PlayerMoney, data => this.playerBalance = data.value));
        this.subscribers.push(this.eventChannel.subscribe(UI.JoinTableError, data => this.alertMessage = data.message));
        this.subscribers.push(this.eventChannel.subscribe(UI.JoinTableSuccess, data => this.router.navigate("game-table")));
    }

    detached() {
        for (let item of this.subscribers) item.dispose();
        this.subscribers = [];
    }

    activate(parameters, routeConfig) {

        this.gameState.currentPage = "lobby";
        
        this.changeLang(this.gameState.currentLang);

        this.merchantCode = this.gameState.merchantName;
        this.playerName = this.gameState.playerId;

        if (this.playerCurrency.length <= 0) {
            this.playerCurrency = this.gameState.playerCurrency;
            this.playerBalance = this.gameState.playerMoney;
        }

        this.playerBalance = this.gameState.currentPlayerBalance;
        if (this.playerCurrency.length <= 0) this.playerCurrency = "CNY";

        if (this.tables.length <= 0) {
            this.workingTables = [];
            this.gameState.tableStates.forEach((table) => {
                let tableInfo = new TableSummaryInfo();
                tableInfo.tableCode = table.tableInfo.tableCode;
                tableInfo.tableName = table.tableInfo.tableName;
                tableInfo.playerCount = table.tableInfo.playerCount;
                tableInfo.countdown = table.tableInfo.betTimeCountdown;
                tableInfo.isOpen = table.tableInfo.roundState > 1;
                tableInfo.serverCode = table.tableInfo.gameServer;
                tableInfo.players = ["?", "?", "?", "?"];
                for (let i = 0; i<table.tableInfo.gamePlayers.length; i++)
                    tableInfo.players[i] = table.tableInfo.gamePlayers[i];
                tableInfo.state = TableState.SIMPLE_GAME_STATES[table.tableInfo.roundState];
                if (tableInfo.isOpen) {
                    this.workingTables.push(tableInfo);
                } else {
                    // ...
                }
                this.tables.push(tableInfo);
            });
        }

        this.messenger.processPendingMessages("lobby");

        if (this.countdownTimer != null) {
            clearInterval(this.countdownTimer);
            this.countdownTimer = null;
        }
        this.countdownTimer = setInterval(() => {
            // ...
        }, 1000);

        if (this.refreshBalanceTimer != null) {
            clearInterval(this.refreshBalanceTimer);
            this.refreshBalanceTimer = null;
        }
        this.refreshBalanceTimer = setInterval(() => {
            this.messenger.getPlayerBalance();
        }, 10000);
        
    }

    deactivate() {
        if (this.countdownTimer != null) {
            clearInterval(this.countdownTimer);
            this.countdownTimer = null;
        }
        if (this.refreshBalanceTimer != null) {
            clearInterval(this.refreshBalanceTimer);
            this.refreshBalanceTimer = null;
        }
    }

    get canShowTables() {
        return this.workingTables.length > 0 && (this.minVisibleTableCount <= 0 
                || this.workingTables.length >= this.minVisibleTableCount 
                || this.tables.length < this.minVisibleTableCount);
    }

    get isEmptyAlertMessage() {
        return this.alertMessage == null || this.alertMessage.length <= 0;
    }

    dismissAlertMessage() {
        this.alertMessage = null;
    }

    enterTable(tableCode: string) {
        this.selectedTableCode = tableCode;
        console.log("going to enter " + this.selectedTableCode);
        this.messenger.joinTable(this.selectedTableCode);
        //this.gameState.currentTableCode = this.selectedTableCode;
        //this.router.navigate("baccarat");
    }

    changeLang(lang: string) {
        this.i18n.setLocale(lang)
        .then(() => {
            this.gameState.currentLang = this.i18n.getLocale();
            console.log(this.gameState.currentLang);
        });
    }
}
