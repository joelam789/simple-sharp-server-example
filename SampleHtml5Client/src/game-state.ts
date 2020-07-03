

export class TableInfo {
    gameServer: string = "";
    gameType: number = 0;
    tableCode: string = "";
    tableName: string = "";
    shoeCode: string = "";
    roundNumber: number = 0;
    roundState: number = 0;
    roundStateText: string = "";
    betTimeCountdown: number = 0;
    gameTimeCountdown: number = 0;
    nextRoundCountdown: number = 0;
    playerCount: number = 0;
    currentTurns: number = 0;
    gameResult: string = "";
    currentPlayer: string = "";
    lastPlayer: string = "";
    lastPlay: string = "";
    gamePlayers: Array<string> = [];
    playerScores: Array<number> = [];
    cardCounts: Array<number> = [];
    isOpen: boolean = false;
}

export class TableState {

    static readonly SIMPLE_GAME_STATES = ["Unknown", "Closed", "Preparing", "NewRound", "Betting", 
                                          "Dealing", "Counting", "Settling"];

    tableInfo: TableInfo = new TableInfo();

    simpleHistory: string = "";

    constructor() {
        
    }


    static getPlayerScoreFromResult(result: string): number {
        return parseInt(result.substr(2, 1), 10);
    }

    static getBankerScoreFromResult(result: string): number {
        return parseInt(result.substr(1, 1), 10);
    }

    static getWinlossFlagFromResult(result: string): number {
        return parseInt(result.substr(0, 1), 10);
    }

}

export class TableSummaryInfo {
    state: string = "";
    tableCode: string = "";
    tableName: string = "";
    serverCode: string = "";
    playerCount: number = 0;
    players: Array<string> = [];
    countdown: number = 0;
    isOpen: boolean = false;
}

export class BetLimitRange {
    min: number = 0;
    max: number = 0;
}

export class GameCard {
    suit: number = -1; // CARD_SUIT_SPADE = 0, CARD_SUIT_HEART, CARD_SUIT_CLUB, CARD_SUIT_DIAMOND
    value: number = -1; // CARD_ACE = 0, CARD_2, CARD_3, ... , CARD_10, CARD_JACK, CARD_QUEEN, CARD_KING
    open: boolean = false;
}

export class GameState {

    // some global ui info
    currentPage: string = "";
    currentLang: string = "en";

    // for logging in to login server
    merchantName: string = "";
    currencyCode: string = "";
    playerId: string = "";
    password: string = "";
    serverUrl: string = "";
    loginError: string = "";

    // for logging in to frontend server
    companyCode: string = "";
    playerName: string = "";
    loginSystemId: number = 0;
    loginKey: string = "";
    hostIp: string = "";
    hostPort: number = 0;

    systemType: number = 0;

    // login info
    playerCurrency: string = "";
    playerReloginToken: string = "";
    playerMoney: number = 0;

    playerScore: number = 0;

    betLimitProfiles: any = null;
    playerBetHistoryUrl: string = "";

    selectedTableCode: string = "";
    selectedBetLimitIndex: number = -1;

    targetTableCode: string = "";
    currentTableCode: string = "";
    currentTableName: string = "";

    // info from login server
    loginServerAddress: string = "";
    frontEndServerAddress: string = "";
    bettingServerAddress: string = "";
    currentPlayerSessionId: string = "";
    currentPlayerBalance: number = 0;
    currentCpcOptions: string = "";
    currentBplOptions: string = "";

    // info from front-end server
    frontEndServerName: string = "";
    frontEndSessionId: string = "";
    frontEndClientId: string = "";

    currentTurnPlayer: string = "";
    currentTurnNumber: number = 0;

    tables: Map<string, TableSummaryInfo> = new Map<string, TableSummaryInfo>();
    tableStates: Map<string, TableState> = new Map<string, TableState>();

    playerCards: Array<string> = [];
    

    // table info
    talbeChips: Map<string, Array<Array<number>>> = new Map<string, Array<Array<number>>>();
    
    countdownTimer: any = null;

    startAutoCountdown() {
        if (this.countdownTimer != null) {
            clearInterval(this.countdownTimer);
            this.countdownTimer = null;
        }
        this.countdownTimer = setInterval(() => {
            this.tables.forEach((table) => {
                if (table.isOpen && table.countdown > 0) table.countdown--;
                if (!table.isOpen || table.countdown < 0) table.countdown = 0;
            });
        }, 1000);
    }

    stopAutoCountdown() {
        if (this.countdownTimer != null) {
            clearInterval(this.countdownTimer);
            this.countdownTimer = null;
        }
    }

    static getCardCode(card: GameCard = null): string {
        if (card != null && card.suit >= 0 && card.value >= 0) {
            let cardNamePart1Chars = ['1', '2', '3', '4', '5', '6', '7', '8', '9', 'T', 'J', 'Q', 'K'];
            let cardNamePart2Chars = ['S', 'H', 'C', 'D'];
            if (card.suit < cardNamePart2Chars.length && card.value < cardNamePart1Chars.length) {
                return cardNamePart1Chars[card.value] + cardNamePart2Chars[card.suit];
            }
        }
        return "01";
    }

    static getCardByCode(code: string = null): GameCard {
        if (code == undefined || code == null || code.length <= 1) return new GameCard();
        let cardNamePart1Chars = ['1', '2', '3', '4', '5', '6', '7', '8', '9', 'T', 'J', 'Q', 'K'];
        let cardNamePart2Chars = ['S', 'H', 'C', 'D'];
        let card = new GameCard();
        card.open = true;
        card.suit = cardNamePart2Chars.indexOf(code.charAt(1));
        card.value = cardNamePart1Chars.indexOf(code.charAt(0));
        return card;
    }

}
