(() => {
    "use strict";
    const toggle = document.getElementById("concierge-toggle");
    const panel = document.getElementById("concierge-panel");
    const close = document.getElementById("concierge-close");
    const form = document.getElementById("concierge-form");
    const question = document.getElementById("concierge-question");
    const log = document.getElementById("concierge-messages");
    const suggestions = document.getElementById("concierge-suggestions");
    if (!toggle || !panel || !close || !form || !question || !log) return;

    let pending = false;
    const history = [];
    const submit = form.querySelector('button[type="submit"]');

    function show(open) {
        panel.hidden = !open;
        toggle.setAttribute("aria-expanded", String(open));
        if (open) question.focus();
        else toggle.focus();
    }

    function bubble(role, message) {
        const entry = document.createElement("p");
        entry.className = "concierge-bubble " +
            (role === "user" ? "concierge-bubble-user" : "concierge-bubble-assistant");
        // Text only: never interpret model output as HTML.
        entry.textContent = message;
        log.appendChild(entry);
        log.scrollTop = log.scrollHeight;
        return entry;
    }

    // Link only recognised restaurant pages. Never parse chatbot or AI text as HTML.
    function renderAssistant(entry, message) {
        const label = "Guest Reviews page";
        const start = message.toLowerCase().indexOf(label.toLowerCase());
        if (start === -1) {
            entry.textContent = message;
            return;
        }

        const link = document.createElement("a");
        link.href = "/Reviews";
        link.textContent = message.slice(start, start + label.length);
        entry.replaceChildren(
            document.createTextNode(message.slice(0, start)),
            link,
            document.createTextNode(message.slice(start + label.length))
        );
    }

    async function ask(text) {
        const message = String(text || "").trim();
        if (!message || pending || message.length > 500) return;
        pending = true;
        question.value = "";
        question.disabled = true;
        submit.disabled = true;
        if (suggestions) suggestions.hidden = true;

        const priorHistory = history.slice(-6);
        bubble("user", message);
        const thinking = bubble("assistant", "Let me check that for you…");
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 18000);
        try {
            const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const response = await fetch(form.dataset.endpoint, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": token || ""
                },
                credentials: "same-origin",
                body: JSON.stringify({ message, history: priorHistory }),
                signal: controller.signal
            });
            let data = {};
            try { data = await response.json(); } catch (_) { /* Non-JSON failures */ }
            if (!response.ok || !data.reply) {
                throw new Error(data.error || (response.status === 429
                    ? "You've reached the chat limit for now. Please try again shortly."
                    : "The concierge is temporarily unavailable. Please try again."));
            }
            renderAssistant(thinking, data.reply);
            history.push({ role: "user", content: message },
                         { role: "assistant", content: data.reply });
            if (history.length > 12) history.splice(0, history.length - 12);
        } catch (error) {
            thinking.textContent = error.name === "AbortError"
                ? "That took too long. Please try again in a moment."
                : error.message || "Please try again.";
        } finally {
            clearTimeout(timeout);
            pending = false;
            question.disabled = false;
            submit.disabled = false;
            log.scrollTop = log.scrollHeight;
            question.focus();
        }
    }

    toggle.addEventListener("click", () => show(panel.hidden));
    close.addEventListener("click", () => show(false));
    form.addEventListener("submit", event => {
        event.preventDefault();
        ask(question.value);
    });
    suggestions?.addEventListener("click", event => {
        const button = event.target.closest("button[data-question]");
        if (button) ask(button.dataset.question);
    });
    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && !panel.hidden) show(false);
    });
})();
