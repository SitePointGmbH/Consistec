import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { css, html, repeat } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";

// Keep in sync with umbConsistec.Services.PodcastIcons (frontend rendering).
const icons = [
    ["apple-podcasts", "Apple Podcasts"],
    ["spotify", "Spotify"],
    ["youtube", "YouTube"],
    ["youtube-music", "YouTube Music"],
    ["amazon-music", "Amazon Music"],
    ["deezer", "Deezer"],
    ["audible", "Audible"],
    ["pocket-casts", "Pocket Casts"],
    ["rss", "RSS-Feed"],
    ["podcast", "Podcast (neutral)"]
];

export default class PodcastIconPickerElement extends UmbLitElement {
    static properties = {
        value: { attribute: false },
        readonly: { type: Boolean }
    };

    static styles = css`
        .grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(110px, 1fr));
            gap: var(--uui-size-space-3);
        }
        button {
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: var(--uui-size-space-2);
            padding: var(--uui-size-space-3);
            border: 1px solid var(--uui-color-border);
            border-radius: var(--uui-border-radius);
            background: var(--uui-color-surface);
            color: var(--uui-color-text);
            font: inherit;
            font-size: var(--uui-type-small-size);
            cursor: pointer;
        }
        button:hover:not(:disabled) { border-color: var(--uui-color-border-emphasis); }
        button[aria-pressed="true"] {
            border-color: var(--uui-color-selected);
            box-shadow: 0 0 0 2px var(--uui-color-selected);
        }
        button:disabled { cursor: default; opacity: .6; }
        img { width: 32px; height: 32px; object-fit: contain; }
        .clear { margin-top: var(--uui-size-space-3); }
    `;

    constructor() {
        super();
        this.value = "";
        this.readonly = false;
    }

    render() {
        return html`
            <div class="grid" role="group" aria-label="Podcast-Plattform">
                ${repeat(icons, ([key]) => key, ([key, label]) => html`
                    <button type="button" aria-pressed=${this.value === key ? "true" : "false"}
                            ?disabled=${this.readonly} @click=${() => this.#select(key)}>
                        <img src="/assets/img/podcast-icons/${key}.svg" alt="" />
                        ${label}
                    </button>
                `)}
            </div>
            ${this.value && !this.readonly ? html`
                <uui-button class="clear" look="secondary" label="Auswahl entfernen"
                            @click=${() => this.#select("")}>Auswahl entfernen</uui-button>` : ""}
        `;
    }

    #select(key) {
        if (this.readonly || this.value === key) return;
        this.value = key;
        this.dispatchEvent(new UmbChangeEvent());
    }
}

customElements.define("consistec-podcast-icon-picker", PodcastIconPickerElement);
