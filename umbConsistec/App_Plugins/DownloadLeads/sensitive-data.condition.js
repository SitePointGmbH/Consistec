import { UMB_CURRENT_USER_CONTEXT } from "@umbraco-cms/backoffice/current-user";
import { UmbConditionBase } from "@umbraco-cms/backoffice/extension-registry";

export class api extends UmbConditionBase {
    constructor(host, args) {
        super(host, args);
        this.permitted = false;
        this.consumeContext(UMB_CURRENT_USER_CONTEXT, (context) => {
            this.permitted = false;
            this.observe(context?.hasAccessToSensitiveData, (permitted) => {
                this.permitted = permitted === true;
            }, "downloadLeadsSensitiveData");
        });
    }
}
