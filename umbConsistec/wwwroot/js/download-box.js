let sdkPromise;

function getCaptchaSdk() {
    sdkPromise ??= import("https://cdn.jsdelivr.net/npm/@friendlycaptcha/sdk@1.1.3/+esm")
        .then(({ FriendlyCaptchaSDK }) => new FriendlyCaptchaSDK())
        .catch((error) => {
            sdkPromise = undefined;
            throw error;
        });
    return sdkPromise;
}

function initializeDownloads() {
    for (const modal of document.querySelectorAll("[data-download-modal]")) {
        // Block Grid animations can create stacking contexts around the card.
        document.body.append(modal);
        const form = modal.querySelector("[data-protected-download]");
        const element = form.querySelector("[data-download-captcha]");
        const email = form.elements.namedItem("email");
        const terms = form.elements.namedItem("acceptTerms");
        const button = form.querySelector("button[type=submit]");
        const status = form.querySelector("[role=status]");
        let sending = false;
        let sent = false;
        let loading = false;
        let widget;

        const canSubmit = (captchaState = widget?.getState()) =>
            !sending && !sent && email.validity.valid && terms.checked && captchaState === "completed";

        const updateButton = () => { button.disabled = !canSubmit(); };
        email.addEventListener("input", updateButton);
        email.addEventListener("change", updateButton);
        terms.addEventListener("change", updateButton);

        modal.addEventListener("show.bs.modal", () => document.body.classList.add("download-modal-open"));
        modal.addEventListener("hidden.bs.modal", () => document.body.classList.remove("download-modal-open"));

        element.addEventListener("frc:widget.statechange", ({ detail }) => {
            button.disabled = !canSubmit(detail.state);
            if (!sending && !sent && (detail.state === "error" || detail.state === "expired")) {
                status.textContent = form.dataset.verification;
            }
        });

        modal.addEventListener("shown.bs.modal", async () => {
            if (widget || loading) return;
            loading = true;
            status.textContent = "";
            try {
                const sdk = await getCaptchaSdk();
                if (!modal.classList.contains("show")) return;
                widget = sdk.createWidget({
                    element,
                    sitekey: element.dataset.sitekey,
                    language: form.dataset.language,
                    startMode: "focus"
                });
            } catch {
                status.textContent = form.dataset.error;
            } finally {
                loading = false;
            }
        });

        form.addEventListener("submit", async (event) => {
            event.preventDefault();
            if (!canSubmit()) return;

            sending = true;
            button.disabled = true;
            status.textContent = form.dataset.sending;
            try {
                const response = await fetch(form.action, {
                    method: "POST",
                    body: new URLSearchParams(new FormData(form)),
                    credentials: "same-origin"
                });
                sent = response.status === 204;
                status.textContent = sent ? form.dataset.success : ({
                    400: form.dataset.invalid,
                    422: form.dataset.verification,
                    429: form.dataset.rateLimit
                }[response.status] || form.dataset.error);
                if (sent) form.reset();
            } catch {
                status.textContent = form.dataset.error;
            } finally {
                sending = false;
                if (!sent) widget.reset();
                updateButton();
            }
        });
    }
}

initializeDownloads();
