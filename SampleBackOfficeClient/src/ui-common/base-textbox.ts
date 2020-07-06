import { Widget } from "./widget";

export class BaseTextbox implements Widget {

    uid: string = "";
    gui: any = null;

    constructor(uid) {
        this.uid = uid;
    }
    
    init(callback?) {
        this.gui = ($('#' + this.uid) as any).textbox.bind($('#' + this.uid));
        if (callback) callback();
    }
}
