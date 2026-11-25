(() => {
    "use strict";

    const gameFrame = document.getElementById("game-frame");
    const gamePlaceholder = document.getElementById("game-placeholder");
    const connectionDot = document.getElementById("connection-dot");
    const connectionLabel = document.getElementById("connection-label");
    const playerName = document.getElementById("player-name");
    const scriptList = document.getElementById("script-list");
    const scriptCount = document.getElementById("script-count");
    const selectedScript = document.getElementById("selected-script");
    const selectedScriptPath = document.getElementById("selected-script-path");
    const loadScriptButton = document.getElementById("load-script");
    const botScriptName = document.getElementById("bot-script-name");
    const botStateLabel = document.getElementById("bot-state-label");
    const startScriptButton = document.getElementById("start-script");
    const pauseScriptButton = document.getElementById("pause-script");
    const stopScriptButton = document.getElementById("stop-script");
    const scriptLogs = document.getElementById("script-logs");
    const toast = document.getElementById("toast");

    let scripts = [];
    let selectedPath = null;
    let scriptPaused = false;
    let toastHandle = null;

    function send(action, values = {}) {
        if (typeof MVSBridge === "undefined" || typeof MVSBridge.postMessage !== "function") {
            showToast("MVS native bridge is not available.");
            return;
        }

        MVSBridge.postMessage(JSON.stringify({ action, ...values }));
    }

    function showToast(message) {
        toast.textContent = message;
        toast.classList.add("show");
        clearTimeout(toastHandle);
        toastHandle = setTimeout(() => toast.classList.remove("show"), 2800);
    }

    function setView(name) {
        document.querySelectorAll(".view").forEach(view => view.classList.toggle("active", view.id === "view-" + name));
        document.querySelectorAll(".nav-button").forEach(button => button.classList.toggle("active", button.dataset.view === name));

        if (name === "scripts")
            send("scripts");
        else if (name === "bot")
            send("status");
        else if (name === "logs")
            send("logs");
    }

    function renderScripts(items) {
        scripts = Array.isArray(items) ? items : [];
        scriptCount.textContent = scripts.length + (scripts.length === 1 ? " script" : " scripts");
        scriptList.replaceChildren();

        if (scripts.length === 0) {
            const empty = document.createElement("div");
            empty.className = "empty-state";
            empty.textContent = "No scripts were found in the MVS scripts folder.";
            scriptList.append(empty);
            return;
        }

        for (const script of scripts) {
            const button = document.createElement("button");
            const name = document.createElement("span");
            const hint = document.createElement("small");
            button.type = "button";
            button.className = "script-item" + (script.path === selectedPath ? " selected" : "");
            name.textContent = script.name;
            hint.textContent = "C#";
            button.append(name, hint);
            button.addEventListener("click", () => selectScript(script));
            scriptList.append(button);
        }
    }

    function selectScript(script) {
        selectedPath = script.path;
        selectedScript.textContent = script.name;
        selectedScriptPath.textContent = script.path;
        loadScriptButton.disabled = false;
        renderScripts(scripts);
    }

    function updateStatus(status) {
        const connected = !!status.gameConnected;
        connectionDot.classList.toggle("offline", !connected);
        connectionLabel.textContent = connected ? "Game connected" : "Game not logged in";
        playerName.textContent = connected ? "AQW session active" : "Not logged in";

        botScriptName.textContent = status.loadedScript ? status.loadedScript.split(/[\\\\/]/).pop() : "None loaded";
        scriptPaused = !!status.scriptPaused;
        botStateLabel.textContent = status.scriptRunning ? (scriptPaused ? "Paused" : "Running") : "Idle";
        startScriptButton.disabled = !status.loadedScript || !!status.scriptRunning;
        pauseScriptButton.disabled = !status.scriptRunning;
        stopScriptButton.disabled = !status.scriptRunning;
        pauseScriptButton.textContent = scriptPaused ? "Resume" : "Pause";
    }

    function renderLogs(items) {
        scriptLogs.textContent = Array.isArray(items) && items.length > 0 ? items.join("\n") : "No script output yet.";
        scriptLogs.parentElement.scrollTop = scriptLogs.parentElement.scrollHeight;
    }

    window.MVSClient = {
        setGameUrl(url) {
            gameFrame.src = url;
            gameFrame.addEventListener("load", () => gamePlaceholder.classList.add("hidden"), { once: true });
            gameFrame.addEventListener("error", () => {
                gamePlaceholder.classList.remove("hidden");
                gamePlaceholder.querySelector("h3").textContent = "Game client could not load";
            }, { once: true });
        },
        receive(message) {
            if (!message || typeof message !== "object")
                return;

            switch (message.type) {
                case "status":
                    updateStatus(message);
                    break;
                case "scripts":
                    renderScripts(message.items);
                    break;
                case "scriptLoaded":
                    showToast("Loaded " + message.name);
                    send("status");
                    break;
                case "logs":
                    renderLogs(message.items);
                    break;
                case "operation":
                    showToast(message.message || (message.success ? "Done." : "Operation failed."));
                    send("status");
                    break;
                case "error":
                    showToast(message.message || "MVS operation failed.");
                    break;
            }
        }
    };

    document.querySelectorAll(".nav-button").forEach(button => {
        button.addEventListener("click", () => setView(button.dataset.view));
    });
    document.getElementById("refresh-scripts").addEventListener("click", () => send("scripts"));
    document.getElementById("refresh-logs").addEventListener("click", () => send("logs"));
    loadScriptButton.addEventListener("click", () => {
        if (selectedPath)
            send("loadScript", { path: selectedPath });
    });
    startScriptButton.addEventListener("click", () => send("startScript"));
    stopScriptButton.addEventListener("click", () => send("stopScript"));
    pauseScriptButton.addEventListener("click", () => send("pauseScript", { paused: !scriptPaused }));

    window.addEventListener("load", () => {
        send("status");
        send("scripts");
    });
})();
