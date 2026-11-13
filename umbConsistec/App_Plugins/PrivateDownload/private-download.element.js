import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { css, html, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbMediaPickerFolderFilter } from "@umbraco-cms/backoffice/media";
import { UMB_PROPERTY_DATASET_CONTEXT } from "@umbraco-cms/backoffice/property";

const mediaPrefix = "umb://media/";
const mediaIdPattern = /^[a-f0-9]{32}$|^[a-f0-9]{8}-(?:[a-f0-9]{4}-){3}[a-f0-9]{12}$/i;

export default class PrivateDownloadElement extends UmbLitElement {
    static properties = {
        value: { attribute: false },
        readonly: { type: Boolean },
        protectedDownload: { state: true },
        uploading: { state: true },
        message: { state: true }
    };

    static styles = css`
        .actions { display: flex; align-items: center; flex-wrap: wrap; gap: var(--uui-size-space-3); }
        .file-name { margin-block: 0 var(--uui-size-space-3); overflow-wrap: anywhere; }
        .status { margin-block: var(--uui-size-space-3) 0; }
    `;

    constructor() {
        super();
        this.value = "";
        this.readonly = false;
        this.uploading = false;
        this.message = "";
        this.consumeContext(UMB_PROPERTY_DATASET_CONTEXT, async (dataset) => {
            const mode = await dataset?.propertyValueByAlias("protectedDownload");
            this.observe(mode, (value) => {
                this.protectedDownload = value === true || value === 1 || value === "1" || value === "true";
                this.message = "";
            }, "observeDownloadMode");
        });
    }

    render() {
        if (this.protectedDownload === undefined) return nothing;
        if (!this.protectedDownload) {
            const mediaId = this.value?.startsWith(mediaPrefix) ? this.value.slice(mediaPrefix.length) : "";
            return html`
                <umb-input-media .max=${1} .folderFilter=${UmbMediaPickerFolderFilter.FILES_ONLY}
                    .selection=${mediaIdPattern.test(mediaId) ? [mediaId] : []}
                    ?readonly=${this.readonly} @change=${this.#selectMedia}></umb-input-media>
            `;
        }

        const displayName = this.value?.startsWith(mediaPrefix)
            ? "" : this.value?.replace(/^upload-[a-f0-9]{32}-/, "");
        return html`
            ${displayName ? html`<p class="file-name">${displayName}</p>` : nothing}
            ${this.readonly ? nothing : html`
                <input type="file" hidden accept=".pdf,.docx,.xlsx,.pptx,.txt,.csv,.odt,.ods"
                       @change=${this.#upload} ?disabled=${this.uploading} />
                <div class="actions">
                    <uui-button type="button" look="outline" label=${displayName ? "Replace private file" : "Upload private file"}
                                ?disabled=${this.uploading} @click=${this.#chooseFile}>
                        <uui-icon name="icon-upload"></uui-icon>
                        ${displayName ? "Replace private file" : "Upload private file"}
                    </uui-button>
                    ${displayName ? html`<uui-button type="button" look="secondary" label="Clear file selection"
                        ?disabled=${this.uploading} @click=${this.#clear}>Clear</uui-button>` : nothing}
                </div>
            `}
            <p class="status" role="status" ?hidden=${!this.message}>${this.message}</p>
        `;
    }

    #selectMedia(event) {
        event.stopPropagation();
        if (this.readonly || this.protectedDownload !== false) return;
        const [mediaId] = event.currentTarget.selection;
        if (mediaId && !mediaIdPattern.test(mediaId)) return;
        this.value = mediaId ? mediaPrefix + mediaId : "";
        this.dispatchEvent(new UmbChangeEvent());
    }

    #chooseFile() {
        this.renderRoot.querySelector("input[type=file]")?.click();
    }

    #clear() {
        this.value = "";
        this.message = "";
        this.dispatchEvent(new UmbChangeEvent());
    }

    async #upload(event) {
        const input = event.currentTarget;
        const file = input.files?.[0];
        input.value = "";
        if (!file || !this.protectedDownload || this.readonly || this.uploading) return;
        if (file.size === 0 || file.size > 10 * 1024 * 1024) {
            this.message = "Choose a nonempty file up to 10 MiB.";
            return;
        }

        this.uploading = true;
        this.message = "Uploading...";
        try {
            const auth = await this.getContext(UMB_AUTH_CONTEXT);
            const config = auth.getOpenApiConfiguration();
            const body = new FormData();
            body.append("file", file);
            const response = await fetch(
                `${config.base.replace(/\/$/, "")}/umbraco/management/api/v1/consistec/private-downloads`,
                {
                    method: "POST",
                    credentials: config.credentials,
                    headers: { Authorization: `Bearer ${await config.token()}` },
                    body
                });
            if (!response.ok) {
                const problem = await response.json().catch(() => null);
                this.message = response.status === 401 || response.status === 403
                    ? "Sign in with access to the Content section to upload."
                    : problem?.title || "The file could not be uploaded.";
                return;
            }

            const result = await response.json();
            if (!this.isConnected || !this.protectedDownload || this.readonly) return;
            this.value = result.value;
            this.message = "File uploaded.";
            this.dispatchEvent(new UmbChangeEvent());
        } catch {
            this.message = "The file could not be uploaded. Please try again.";
        } finally {
            this.uploading = false;
        }
    }
}

customElements.define("consistec-private-download", PrivateDownloadElement);
