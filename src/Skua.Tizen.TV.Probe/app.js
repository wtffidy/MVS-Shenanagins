(function () {
    "use strict";

    const testResults = {};
    const reportData = {
        app: "MVS Samsung TV Probe",
        version: "0.1.0",
        testedAt: null,
        device: {},
        capabilities: {},
        inputEvents: []
    };

    const byId = (id) => document.getElementById(id);
    const setText = (id, value) => {
        const element = byId(id);
        if (element) element.textContent = String(value);
    };

    function setTest(name, state, detail) {
        testResults[name] = { state, detail: String(detail || "") };
        const card = document.querySelector('[data-test="' + name + '"]');
        if (!card) return;

        const result = card.querySelector(".result");
        const detailElement = card.querySelector(".detail");
        result.className = "result " + state;
        result.textContent = state.toUpperCase();
        detailElement.textContent = String(detail || "");
    }

    function updateOverallStatus() {
        const values = Object.values(testResults);
        if (values.length === 0) return;

        const failed = values.filter(result => result.state === "fail").length;
        const warned = values.filter(result => result.state === "warn").length;
        const pill = byId("overall-status");

        if (failed > 0) {
            pill.className = "status-pill fail";
            pill.textContent = failed + " check(s) failed";
        } else if (warned > 0) {
            pill.className = "status-pill warn";
            pill.textContent = "Review " + warned + " check(s)";
        } else {
            pill.className = "status-pill pass";
            pill.textContent = "Checks complete";
        }
    }

    function collectDeviceInfo() {
        const screenWidth = window.screen && window.screen.width ? window.screen.width : 0;
        const screenHeight = window.screen && window.screen.height ? window.screen.height : 0;
        const userAgent = navigator.userAgent || "Unavailable";
        const isSamsung = /Tizen|SMART-TV|SmartTV|Samsung/i.test(userAgent);

        reportData.device = {
            userAgent,
            platform: navigator.platform || "Unavailable",
            language: navigator.language || "Unavailable",
            screen: screenWidth + "x" + screenHeight,
            viewport: window.innerWidth + "x" + window.innerHeight,
            devicePixelRatio: window.devicePixelRatio || 1,
            hardwareConcurrency: navigator.hardwareConcurrency || null,
            deviceMemoryGb: navigator.deviceMemory || null,
            maxTouchPoints: navigator.maxTouchPoints || 0,
            isSamsungOrTizen: isSamsung
        };

        setText("device-name", isSamsung ? "Samsung / Tizen browser detected" : "Browser environment detected");
        setText("device-summary", userAgent);
        setText("resolution", screenWidth + " × " + screenHeight);
        setText("pixel-ratio", navigator.devicePixelRatio || 1);
        setText("cpu-threads", navigator.hardwareConcurrency || "Not exposed");
        setText("touch-points", navigator.maxTouchPoints || 0);
    }

    async function testWasm() {
        if (typeof WebAssembly !== "object") {
            setTest("wasm", "fail", "WebAssembly API is unavailable.");
            return;
        }

        const bytes = new Uint8Array([
            0x00, 0x61, 0x73, 0x6d, 0x01, 0x00, 0x00, 0x00,
            0x01, 0x07, 0x01, 0x60, 0x02, 0x7f, 0x7f, 0x01, 0x7f,
            0x03, 0x02, 0x01, 0x00,
            0x07, 0x07, 0x01, 0x03, 0x61, 0x64, 0x64, 0x00, 0x00,
            0x0a, 0x09, 0x01, 0x07, 0x00, 0x20, 0x00, 0x20, 0x01, 0x6a, 0x0b
        ]);

        try {
            const result = await WebAssembly.instantiate(bytes);
            const value = result.instance.exports.add(19, 23);
            const passed = value === 42;
            setTest("wasm", passed ? "pass" : "fail", passed ? "WASM module executed correctly." : "Unexpected WASM result: " + value);
        } catch (error) {
            setTest("wasm", "fail", error && error.message ? error.message : error);
        }
    }

    function testWebGl() {
        const canvas = document.createElement("canvas");
        let context = null;
        let renderer = "Unavailable";

        try {
            context = canvas.getContext("webgl2", { failIfMajorPerformanceCaveat: false });
            if (context) {
                renderer = "WebGL 2 · max texture " + context.getParameter(context.MAX_TEXTURE_SIZE);
            } else {
                context = canvas.getContext("webgl", { failIfMajorPerformanceCaveat: false });
                if (context) renderer = "WebGL 1 · max texture " + context.getParameter(context.MAX_TEXTURE_SIZE);
            }
        } catch (error) {
            renderer = error && error.message ? error.message : String(error);
        }

        reportData.capabilities.webGl = renderer;
        setTest("webgl", context ? "pass" : "fail", renderer);
    }

    function testWebSocket() {
        const supported = typeof window.WebSocket === "function";
        setTest("websocket", supported ? "pass" : "fail", supported ? "WebSocket constructor is available; no server was contacted." : "WebSocket API is unavailable.");
    }

    function testAudio() {
        const supported = typeof window.AudioContext === "function" || typeof window.webkitAudioContext === "function";
        setTest("audio", supported ? "pass" : "warn", supported ? "Web Audio API is available; playback permission was not requested." : "Web Audio API is not exposed.");
    }

    function testStorage() {
        try {
            const key = "__mvs_probe__";
            localStorage.setItem(key, "ok");
            const value = localStorage.getItem(key);
            localStorage.removeItem(key);
            const passed = value === "ok";
            setTest("storage", passed ? "pass" : "fail", passed ? "Temporary write/read/remove succeeded." : "Stored value did not match.");
        } catch (error) {
            setTest("storage", "fail", error && error.message ? error.message : error);
        }
    }

    function testIndexedDb() {
        return new Promise(resolve => {
            if (!window.indexedDB) {
                setTest("indexeddb", "fail", "IndexedDB is unavailable.");
                resolve();
                return;
            }

            const databaseName = "__mvs_device_probe__";
            let request;

            try {
                request = indexedDB.open(databaseName, 1);
            } catch (error) {
                setTest("indexeddb", "fail", error && error.message ? error.message : error);
                resolve();
                return;
            }

            request.onupgradeneeded = () => { };
            request.onsuccess = () => {
                request.result.close();
                const cleanup = indexedDB.deleteDatabase(databaseName);
                cleanup.onsuccess = () => {
                    setTest("indexeddb", "pass", "Temporary database opened, closed, and removed.");
                    resolve();
                };
                cleanup.onerror = () => {
                    setTest("indexeddb", "warn", "Database opened, but cleanup was blocked.");
                    resolve();
                };
                cleanup.onblocked = () => {
                    setTest("indexeddb", "warn", "Database opened, but cleanup is blocked by another connection.");
                    resolve();
                };
            };
            request.onerror = () => {
                setTest("indexeddb", "fail", request.error ? request.error.message : "Database open failed.");
                resolve();
            };
            request.onblocked = () => {
                setTest("indexeddb", "warn", "Database open is blocked.");
                resolve();
            };
        });
    }

    function testFetch() {
        const supported = typeof window.fetch === "function" && typeof window.Request === "function";
        setTest("fetch", supported ? "pass" : "fail", supported ? "Fetch and Request APIs are available." : "Fetch or Request API is missing.");
    }

    function testPointer() {
        const supported = typeof window.PointerEvent === "function";
        const touchPoints = navigator.maxTouchPoints || 0;
        setTest("pointer", supported ? "pass" : "warn", supported ? "Pointer Events available; max touch points: " + touchPoints : "Pointer Events are not exposed.");
    }

    function testTvRemote() {
        const manager = window.tizen && window.tizen.tvinputdevice;
        if (!manager) {
            setTest("tv-remote", "warn", "Samsung TVInputDevice API not found. Use a real TV build to validate remote support.");
            return;
        }

        try {
            const supportedKeys = manager.getSupportedKeys() || [];
            const names = supportedKeys.map(key => key.name);
            const optionalKeys = [
                "MediaPlayPause", "MediaStop", "MediaRewind", "MediaFastForward",
                "MediaTrackNext", "MediaTrackPrevious", "ColorF0Red", "ColorF1Green",
                "ColorF2Yellow", "ColorF3Blue"
            ];
            let registered = 0;

            for (const keyName of optionalKeys) {
                if (!names.includes(keyName)) continue;
                try {
                    manager.registerKey(keyName);
                    registered++;
                } catch (error) {
                    // An unsupported optional key must not prevent mandatory key testing.
                }
            }

            const mandatory = ["ArrowLeft", "ArrowUp", "ArrowRight", "ArrowDown", "Enter", "Back"];
            reportData.capabilities.tvRemote = {
                mandatoryKeys: mandatory,
                registeredOptionalKeyCount: registered,
                supportedOptionalKeys: names
            };
            setTest("tv-remote", "pass", "TVInputDevice API available. " + registered + " optional key(s) registered; mandatory arrows, Enter, and Back need no registration.");
        } catch (error) {
            setTest("tv-remote", "fail", error && error.message ? error.message : error);
        }
    }

    function testAnimation() {
        const supported = typeof window.requestAnimationFrame === "function";
        setTest("animation", supported ? "pass" : "fail", supported ? "requestAnimationFrame is available." : "Animation frame API is unavailable.");
    }

    function handleKey(event) {
        const key = event.key || "Unknown";
        const code = event.keyCode || event.which || 0;
        setText("last-key", key);
        setText("last-key-code", "Key code: " + code);
        reportData.inputEvents.push({
            key,
            code,
            at: new Date().toISOString()
        });
        if (reportData.inputEvents.length > 40) reportData.inputEvents.shift();

        if (key !== "Back" && code !== 10009 && (key.startsWith("Arrow") || key === "Enter")) {
            event.preventDefault();
        }

        updateReport();
    }

    function handlePointer(event) {
        setText("remote-note", "Pointer event: " + event.type + " · " + event.pointerType);
    }

    function updateReport() {
        reportData.testedAt = new Date().toISOString();
        reportData.capabilities.tests = testResults;
        reportData.capabilities.webSocket = typeof window.WebSocket === "function";
        reportData.capabilities.webAudio = typeof window.AudioContext === "function" || typeof window.webkitAudioContext === "function";
        reportData.capabilities.fetch = typeof window.fetch === "function";
        reportData.capabilities.indexedDb = !!window.indexedDB;
        reportData.capabilities.pointerEvents = typeof window.PointerEvent === "function";

        const output = JSON.stringify(reportData, null, 2);
        setText("report", output);
        setText("test-time", "Last run: " + new Date(reportData.testedAt).toLocaleString());
    }

    async function runTests() {
        const button = byId("run-tests");
        button.disabled = true;
        button.textContent = "Testing…";
        for (const name of ["wasm", "webgl", "websocket", "audio", "storage", "indexeddb", "fetch", "pointer", "tv-remote", "animation"]) {
            setTest(name, "pending", "Running test");
        }

        collectDeviceInfo();
        await Promise.all([
            testWasm(),
            Promise.resolve(testWebGl()),
            Promise.resolve(testWebSocket()),
            Promise.resolve(testAudio()),
            Promise.resolve(testStorage()),
            testIndexedDb(),
            Promise.resolve(testFetch()),
            Promise.resolve(testPointer()),
            Promise.resolve(testTvRemote()),
            Promise.resolve(testAnimation())
        ]);

        updateOverallStatus();
        updateReport();
        button.disabled = false;
        button.textContent = "Run tests";
    }

    async function copyReport() {
        updateReport();
        const value = byId("report").textContent;

        try {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(value);
            } else {
                const input = document.createElement("textarea");
                input.value = value;
                input.style.position = "fixed";
                input.style.opacity = "0";
                document.body.appendChild(input);
                input.select();
                const copied = document.execCommand("copy");
                input.remove();
                if (!copied) throw new Error("Copy command was not accepted.");
            }
            setText("test-time", "Report copied");
        } catch (error) {
            setText("test-time", "Copy unavailable. Open Technical report and select the text.");
        }
    }

    document.addEventListener("keydown", handleKey, true);
    document.addEventListener("pointerdown", handlePointer, true);
    byId("run-tests").addEventListener("click", runTests);
    byId("copy-report").addEventListener("click", copyReport);
    collectDeviceInfo();
    runTests();
}());