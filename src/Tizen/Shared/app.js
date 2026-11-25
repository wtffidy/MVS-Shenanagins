(() => {
    "use strict";

    const runEngineButton = document.getElementById("run-engine");
    const runChecksButton = document.getElementById("run-checks");
    const closeEngineButton = document.getElementById("close-engine");
    const capabilityGrid = document.getElementById("capability-grid");
    const checkSummary = document.getElementById("check-summary");
    const enginePanel = document.getElementById("engine-panel");
    const engineHost = document.getElementById("engine-host");
    const engineStatus = document.getElementById("engine-status");

    let ruffleLoadPromise = null;
    let player = null;

    function hasWebGl() {
        const canvas = document.createElement("canvas");
        try {
            return !!(canvas.getContext("webgl2") || canvas.getContext("webgl") || canvas.getContext("experimental-webgl"));
        } catch {
            return false;
        }
    }

    function collectCapabilities() {
        const wasmHeader = new Uint8Array([0x00, 0x61, 0x73, 0x6d, 0x01, 0x00, 0x00, 0x00]);

        return [
            { name: "WebAssembly", supported: typeof WebAssembly !== "undefined" && WebAssembly.validate(wasmHeader) },
            { name: "WebGL", supported: hasWebGl() },
            { name: "WebSockets", supported: typeof WebSocket !== "undefined" },
            { name: "Web Crypto", supported: typeof crypto !== "undefined" && !!crypto.subtle }
        ];
    }

    function renderCapabilities(capabilities) {
        capabilityGrid.replaceChildren();

        for (const capability of capabilities) {
            const card = document.createElement("div");
            const title = document.createElement("span");
            const result = document.createElement("b");

            card.className = "capability " + (capability.supported ? "ok" : "fail");
            title.textContent = capability.name;
            result.textContent = capability.supported ? "Available" : "Unavailable";
            card.append(title, result);
            capabilityGrid.append(card);
        }

        const supportedCount = capabilities.filter(item => item.supported).length;
        checkSummary.textContent = supportedCount + " / " + capabilities.length + " available";
        checkSummary.style.color = supportedCount === capabilities.length ? "#7ce0ad" : "#ff9ba6";
    }

    function checkDeviceSupport() {
        renderCapabilities(collectCapabilities());
    }

    function loadRuffle() {
        if (ruffleLoadPromise)
            return ruffleLoadPromise;

        ruffleLoadPromise = new Promise((resolve, reject) => {
            if (window.RufflePlayer) {
                resolve();
                return;
            }

            const script = document.createElement("script");
            script.src = "engine/ruffle/ruffle.js";
            script.onload = resolve;
            script.onerror = () => reject(new Error("The packaged Ruffle JavaScript file could not be loaded."));
            document.head.append(script);
        });

        return ruffleLoadPromise;
    }

    async function runEngineTest() {
        runEngineButton.disabled = true;
        enginePanel.hidden = false;
        engineStatus.textContent = "Checking WebAssembly and loading the packaged Ruffle engine...";
        engineHost.replaceChildren();

        try {
            const capabilities = collectCapabilities();
            renderCapabilities(capabilities);

            if (!capabilities.find(item => item.name === "WebAssembly").supported)
                throw new Error("This device does not expose WebAssembly to the Tizen Web app.");

            if (!capabilities.find(item => item.name === "WebGL").supported)
                throw new Error("This device does not expose WebGL to the Tizen Web app.");

            await loadRuffle();

            if (!window.RufflePlayer)
                throw new Error("Ruffle loaded without registering its player API.");

            const ruffle = window.RufflePlayer.newest();
            player = ruffle.createPlayer();
            player.style.width = "100%";
            player.style.height = "360px";
            engineHost.append(player);

            await player.ruffle().load({
                url: "engine/skua.swf",
                autoplay: "off",
                splashScreen: false,
                showSwfDownload: false,
                contextMenu: "off",
                allowScriptAccess: true,
                allowNetworking: "none",
                openUrlMode: "deny",
                preferredRenderer: "webgl",
                logLevel: "warn"
            });

            engineStatus.textContent = "Ruffle loaded the packaged MVS SWF. AQW networking and the C# bot runtime are intentionally disabled in this smoke test.";
        } catch (error) {
            engineStatus.textContent = "Ruffle test failed: " + (error && error.message ? error.message : String(error));
        } finally {
            runEngineButton.disabled = false;
            enginePanel.scrollIntoView({ behavior: "smooth", block: "nearest" });
        }
    }

    function closeEngine() {
        if (player) {
            try {
                player.remove();
            } catch {
            }
            player = null;
        }

        engineHost.replaceChildren();
        enginePanel.hidden = true;
        runEngineButton.focus();
    }

    function handleBack() {
        if (!enginePanel.hidden) {
            closeEngine();
            return;
        }

        try {
            if (window.tizen && window.tizen.application)
                window.tizen.application.getCurrentApplication().exit();
        } catch {
        }
    }

    runEngineButton.addEventListener("click", runEngineTest);
    runChecksButton.addEventListener("click", checkDeviceSupport);
    closeEngineButton.addEventListener("click", closeEngine);
    document.addEventListener("keydown", event => {
        if (event.key === "Escape" || event.key === "Backspace") {
            event.preventDefault();
            handleBack();
        }
    });
    document.addEventListener("tizenhwkey", event => {
        if (event.keyName === "back") {
            event.preventDefault();
            handleBack();
        }
    });

    checkDeviceSupport();
    runEngineButton.focus();
})();
