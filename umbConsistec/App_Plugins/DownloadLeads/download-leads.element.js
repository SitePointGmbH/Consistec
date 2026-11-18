import { umbHttpClient } from "@umbraco-cms/backoffice/http-client";
import { css, html, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";

const pageSize = 50;
const utc = new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium", timeStyle: "short", timeZone: "UTC"
});
const date = (value) => value ? utc.format(new Date(value)) : "—";

export class DownloadLeadsDashboard extends UmbLitElement {
    static properties = {
        _items: { state: true },
        _total: { state: true },
        _page: { state: true },
        _loading: { state: true },
        _exporting: { state: true },
        _listError: { state: true },
        _exportError: { state: true }
    };

    static styles = css`
        :host { display: block; padding: var(--uui-size-layout-1); }
        header {
            display: flex;
            align-items: center;
            justify-content: space-between;
            flex-wrap: wrap;
            gap: var(--uui-size-space-3);
            margin-block-end: var(--uui-size-space-4);
        }
        h2 { margin: 0; }
        .table-scroll { overflow-x: auto; }
        table { width: 100%; border-collapse: collapse; text-align: start; }
        th, td {
            text-align: start;
            padding: var(--uui-size-space-3);
            border-bottom: 1px solid var(--uui-color-border);
            vertical-align: top;
        }
        th, .date { white-space: nowrap; }
        .text { min-width: 9rem; overflow-wrap: anywhere; }
        .statuses { display: flex; align-items: flex-start; gap: var(--uui-size-space-2); flex-direction: column; }
        footer { display: flex; justify-content: flex-end; margin-block-start: var(--uui-size-space-4); }
    `;

    #listRequest;
    #exportRequest;

    constructor() {
        super();
        this._items = [];
        this._total = 0;
        this._page = 1;
        this._loading = false;
        this._exporting = false;
        this._listError = "";
        this._exportError = "";
    }

    connectedCallback() {
        super.connectedCallback();
        this.#load(1);
    }

    disconnectedCallback() {
        this.#listRequest?.abort();
        this.#exportRequest?.abort();
        this._items = [];
        this._total = 0;
        this._exporting = false;
        super.disconnectedCallback();
    }

    async #request(path, signal, parseAs) {
        const { data, response } = await umbHttpClient.get({
            url: `/umbraco/management/api/v1/consistec/download-leads${path}`,
            security: [{ scheme: "bearer", type: "http" }],
            cache: "no-store",
            parseAs,
            signal
        });
        if (!response?.ok) throw new Error(String(response?.status));
        return data;
    }

    async #load(page) {
        this.#listRequest?.abort();
        const request = this.#listRequest = new AbortController();
        this._loading = true;
        this._listError = "";
        this._exportError = "";
        try {
            const result = await this.#request(`?page=${page}`, request.signal, "json");
            if (request.signal.aborted) return;
            this._items = result.items;
            this._total = result.total;
            this._page = page;
        } catch (error) {
            if (request.signal.aborted) return;
            this._items = [];
            this._total = 0;
            this._listError = error.message === "403" ? "Access denied." : "Could not load download leads.";
        } finally {
            if (!request.signal.aborted) this._loading = false;
        }
    }

    async #exportCsv() {
        if (this._exporting) return;
        const request = this.#exportRequest = new AbortController();
        this._exporting = true;
        this._exportError = "";
        try {
            const csv = await this.#request("/export", request.signal, "blob");
            if (request.signal.aborted) return;
            const url = URL.createObjectURL(csv);
            const link = document.createElement("a");
            link.href = url;
            link.download = "download-leads.csv";
            link.click();
            setTimeout(() => URL.revokeObjectURL(url), 1000);
        } catch {
            if (!request.signal.aborted) this._exportError = "Could not export download leads.";
        } finally {
            if (!request.signal.aborted) this._exporting = false;
        }
    }

    render() {
        return html`
            <uui-box>
                <header>
                    <h2>Download Leads</h2>
                    <uui-button look="outline" label="Export CSV"
                        ?disabled=${this._loading || this._exporting || !this._total}
                        @click=${this.#exportCsv}>
                        <uui-icon name="icon-download-alt"></uui-icon> Export CSV
                    </uui-button>
                </header>
                ${this._exportError ? html`<p role="alert">${this._exportError}</p>` : nothing}
                ${this.#renderTable()}
            </uui-box>
        `;
    }

    #renderTable() {
        if (this._listError) return html`
            <p role="alert">${this._listError}</p>
            <uui-button look="outline" label="Retry" @click=${() => this.#load(1)}>Retry</uui-button>
        `;
        if (this._loading && !this._items.length) return html`<p role="status">Loading download leads...</p>`;
        if (!this._items.length) return html`<p>No download leads found.</p>`;
        const pages = Math.ceil(this._total / pageSize);
        return html`
            <div class="table-scroll" tabindex="0" role="region" aria-label="Download leads" aria-busy=${this._loading}>
                <table>
                    <thead>
                        <tr>
                            <th scope="col">Email</th><th scope="col">File</th><th scope="col">Status</th>
                            <th scope="col">Requested</th><th scope="col">Email sent</th>
                            <th scope="col">Expires</th><th scope="col">Downloaded</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${this._items.map((lead) => html`
                            <tr>
                                <td class="text">${lead.email ?? "Unavailable"}</td>
                                <td class="text">${lead.downloadName}</td>
                                <td><div class="statuses">
                                    <uui-tag look="secondary" color=${lead.status === "Downloaded" ? "positive" : lead.isExpired ? "warning" : "default"}>${lead.status}</uui-tag>
                                    ${lead.isExpired && lead.status === "Downloaded" ? html`<uui-tag look="secondary" color="warning">Link expired</uui-tag>` : nothing}
                                </div></td>
                                <td class="date">${date(lead.requestedAtUtc)}</td>
                                <td class="date">${date(lead.emailSentAtUtc)}</td>
                                <td class="date">${date(lead.expiresAtUtc)}</td>
                                <td class="date">${date(lead.downloadedAtUtc)}</td>
                            </tr>
                        `)}
                    </tbody>
                </table>
            </div>
            ${pages > 1 ? html`
                <footer>
                    <uui-pagination .current=${this._page} .total=${pages}
                        @change=${(event) => this.#load(event.target.current)}></uui-pagination>
                </footer>
            ` : nothing}
        `;
    }
}

customElements.define("consistec-download-leads", DownloadLeadsDashboard);
export default DownloadLeadsDashboard;
