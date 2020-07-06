import { BaseGrid } from './../ui-common/base-grid';
import { BaseTextbox } from './../ui-common/base-textbox';
import { BaseDateTimebox } from './../ui-common/base-datebox';

import { Container } from "../ui-common/container";
import { BaseCommonForm } from "../ui-common/base-form";

import { HttpClient } from "../http-client";

class Gui extends Container {

    //searchForm: BaseCommonForm = new BaseCommonForm("search-form");

    fromDt: BaseDateTimebox = new BaseDateTimebox("fromdt");
    toDt: BaseDateTimebox = new BaseDateTimebox("todt");
    merchantText: BaseTextbox = new BaseTextbox("merchant");
    currencyText: BaseTextbox = new BaseTextbox("currency");
    playerText: BaseTextbox = new BaseTextbox("player");
    uuidText: BaseTextbox = new BaseTextbox("uuid");
    resultGrid: BaseGrid = new BaseGrid("bet-record-grid");

    init(callback) {
        this.load([this.fromDt, this.toDt, 
                    this.merchantText, this.currencyText, 
                    this.playerText, this.uuidText,
                    this.resultGrid], () => {

            let currentMerchant = (window as any).appConfig.merchantCode;
            if (currentMerchant === "-") {
                this.merchantText.gui({
                    value: "",
                    readonly: false
                });
            } else {
                this.merchantText.gui({
                    value: currentMerchant,
                    readonly: true
                });
            }

            let reqUrl = window.location.protocol + "//" 
                        + (window as any).appConfig.domainApiUrl 
                        + (window as any).appConfig.betRecordReqUrl;

            let reqUrl2 = window.location.protocol + "//" 
                        + (window as any).appConfig.domainApiUrl 
                        + (window as any).appConfig.betTransReqUrl;

            let mainResultGrid = this.resultGrid;

            this.resultGrid.gui({
                url: reqUrl,
                contentType: "text/plain;charset=utf-8",
                view: (window as any).detailview,
                singleSelect:true,
                fit: true,
                detailFormatter: function(index,row){
                    return '<div style="padding:2px"><table class="ddv"></table></div>';
                },
                loader: function(param, success, error) {
                    HttpClient.postJSON(reqUrl , { 
                        sessionId: (window as any).appConfig.sessionId,
                        queryParam: param } , 
                        (json) => {
                            try {
                                if (json.error || json.error_message) {
                                    if (json.error) console.error(json.error);
                                    else console.error(json.error_message);
                                }
                                if (json.rows == undefined) {
                                    json.rows = [];
                                    json.total = 0;
                                }
                                success(json);
                            } catch (ex) {
                                // bug ... ?
                                console.error(ex);
                            }
                        }, () => error()
                    );
                },
                onExpandRow: function(index, row) {
                    let ddv = mainResultGrid.gui('getRowDetail',index).find('table.ddv');
                    let betId = row.bet_id;
                    ddv.datagrid({
                        url: reqUrl2,
                        fitColumns:true,
                        singleSelect:true,
                        rownumbers:true,
                        loadMsg:'loading...',
                        height:'auto',
                        columns:[[
                            {field:'trans_uuid',title:'Transaction ID',width:"20%"},
                            {field:'trans_type',title:'Transaction Type',width:"20%"},
                            {field:'trans_amount',title:'Transaction Amount',width:"60%"}
                        ]],
                        loader: function(param2, success2, error2) {
                            HttpClient.postJSON(reqUrl2 , { 
                                sessionId: (window as any).appConfig.sessionId,
                                queryParam: param2 } , 
                                (json) => {
                                    try {

                                        if (json.error || json.error_message) {
                                            if (json.error) console.error(json.error);
                                            else console.error(json.error_message);
                                        }

                                        if (json.rows == undefined) {
                                            setTimeout(function(){
                                                mainResultGrid.gui('fixDetailRowHeight',index);
                                            }, 130);
                                            setTimeout(function(){
                                                mainResultGrid.gui('resize',{width: "auto", height: "auto"});
                                            }, 290);
                                        } else {
                                            success2(json);
                                        }
                                        
                                        
                                    } catch (ex) {
                                        // bug ... ?
                                        console.error(ex);
                                    }
                                }, () => error2()
                            );
                        },
                        onResize:function(){
                            mainResultGrid.gui('fixDetailRowHeight',index);
                        },
                        onLoadSuccess:function() {

                            setTimeout(function(){
                                mainResultGrid.gui('fixDetailRowHeight',index);
                            }, 130);

                            setTimeout(function(){
                                mainResultGrid.gui('resize',{width: "auto", height: "auto"});
                            }, 290);
                        }
                    });

                    mainResultGrid.gui('fixDetailRowHeight',index);

                    let reqParams2 = {
                        sessionId: (window as any).appConfig.sessionId,
                        merchantCode: (window as any).appConfig.merchantCode,
                        userId: (window as any).appConfig.userId,
                        betId: betId
                    }

                    ddv.datagrid("load", reqParams2);
                }
            });

            if (callback) callback();
        });
    }

    doSearch() {
        let fromDateTime = this.fromDt.gui('getValue');
        let toDateTime = this.toDt.gui('getValue');

        let merchantCode = this.merchantText.gui('getValue');
        let currencyCode = this.currencyText.gui('getValue');
        let playerId = this.playerText.gui('getValue');

        let betId = this.uuidText.gui('getValue');
        
        let reqParams = {
            sessionId: (window as any).appConfig.sessionId,
            merchantCode: (window as any).appConfig.merchantCode,
            userId: (window as any).appConfig.userId,
            fromDateTime: fromDateTime,
            toDateTime: toDateTime,
            queryMerchant: merchantCode,
            queryCurrency: currencyCode,
            queryPlayer: playerId,
            betId: betId
        }

        //console.log(reqParams);

        this.resultGrid.gui("load", reqParams);
    }

    
}

export const gui = new Gui();
