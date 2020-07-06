
import { MenuTree } from "./menu-tree";
import { MainPanel } from "./main-panel";
import { Container } from "../ui-common/container";
import { BaseCommonForm } from "../ui-common/base-form";
import { BaseCommonDialog } from "../ui-common/base-dialog";

import { HttpClient } from "../http-client";

class Gui extends Container {

    menuTree: MenuTree = new MenuTree("menu-tree");
    mainPanel: MainPanel = new MainPanel("main-panel");
    passwordForm: BaseCommonForm = new BaseCommonForm("change-password-form");
    passwordDialog: BaseCommonDialog = new BaseCommonDialog("change-password-dialog");

    init(callback) {
        this.load([this.menuTree, this.mainPanel, this.passwordForm, this.passwordDialog], callback);
    }

    openPasswordDialog() {
        this.passwordForm.setData({
            old_pwd: "",
            new_pwd: "",
            new_pwd2: "",
            user_id: (window as any).appConfig.userId,
            merchant_code: (window as any).appConfig.merchantCode

        });
        this.passwordDialog.open();
    }

    closePasswordDialog() {
        this.passwordDialog.close();
    }

    saveNewPassword() {

        let data = this.passwordForm.getData();

        if (!data || data.old_pwd.length <= 0 || data.new_pwd.length <= 0) {
            ($ as any).messager.alert('Change password','Please input old and new passwords', 'info');
            return;
        }

        if (!data || data.new_pwd != data.new_pwd2) {
            ($ as any).messager.alert('Change password','New passwords are not same', 'info');
            return;
        }

        data.sessionId = (window as any).appConfig.sessionId;
        data.userId = (window as any).appConfig.userId;
        data.merchantCode = (window as any).appConfig.merchantCode;
        data.oldPassword = (window as any).md5(data.old_pwd);
        data.newPassword = (window as any).md5(data.new_pwd);

        let url = (window as any).appConfig.changePasswordReqUrl;

        HttpClient.postJSON(url , {
            sessionId: (window as any).appConfig.sessionId,
            queryParam: data
        }, (json) => {
            if (json && json.error_code === 0) {
                alert("Change password successfully");
                this.passwordDialog.close();
            } else {
                if (json && json.error_code === -2) {
                    alert("Wrong user ID or password");
                } else {
                    if (json && json.error_message) alert(json.error_message);
                }
            }
        });
        
    }

    closeDialog(e) {
        var key = e.keyCode || e.charCode;
        if (key == 27) 
        {
            this.closePasswordDialog();
            $(this).blur();
        }
    }
}

export const gui = new Gui();
