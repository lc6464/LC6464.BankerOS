const fastRevealWindowMs = 3000;
const quickFinishWindowMs = 1000;
const earlyMinDelayMs = 70;
const earlyMaxDelayMs = 240;
const slowMinDelayMs = 280;
const slowMaxDelayMs = 720;
const quickMinDelayMs = 18;
const quickMaxDelayMs = 60;

let bootLines;

function wait(ms) {
    return new Promise(resolve => window.setTimeout(resolve, ms));
}

function randomBetween(min, max) {
    return min + Math.random() * (max - min);
}

async function loadLines() {
    if (bootLines) {
        return bootLines;
    }

    const response = await fetch("data/boot-log-lines.json", { cache: "no-store" });
    bootLines = await response.json();
    return bootLines;
}

function formatSeconds(startTime, offsetMs) {
    const seconds = (Date.now() - startTime + offsetMs) / 1000;
    return seconds.toFixed(4).padStart(10, " ");
}

function appendLine(bootLog, startTime, offsetMs, text) {
    const row = document.createElement("div");
    row.className = "boot-line";
    row.textContent = `[${formatSeconds(startTime, offsetMs)}] ${text}`;
    bootLog.appendChild(row);
}

async function revealRemainingQuickly(lines, bootLog, startTime, lineIndex, durationMs) {
    const remaining = lines.length - lineIndex;
    if (remaining <= 0) {
        await wait(durationMs);
        return lineIndex;
    }

    const perLineDelay = Math.min(
        quickMaxDelayMs,
        Math.max(quickMinDelayMs, durationMs / remaining));

    for (; lineIndex < lines.length; lineIndex++) {
        appendLine(bootLog, startTime, 0, lines[lineIndex]);
        await wait(perLineDelay);
    }

    return lineIndex;
}

async function playBoot(options) {
    const settings = options ?? {};
    const bootOverlay = document.getElementById("boot-overlay");
    const bootLog = document.getElementById("boot-log");
    const appHost = document.getElementById("app");
    const runtimeSignal = settings.runtimeSignal ?? null;
    const lines = await loadLines();
    const startTime = Date.now();
    let runtimeLoaded = settings.waitForRuntime !== true;
    let runtimeLoadedAt = runtimeLoaded ? startTime : null;
    let lineIndex = 0;
    let nextRevealAt = startTime;

    if (!bootOverlay || !bootLog || !appHost) {
        return;
    }

    bootLog.textContent = "";
    bootOverlay.style.display = "flex";

    if (settings.hideAppDuringBoot !== false) {
        appHost.style.display = "none";
    }

    runtimeSignal?.promise.then(() => {
        runtimeLoaded = true;
        runtimeLoadedAt = Date.now();
    });

    appendLine(bootLog, startTime, 0, lines[lineIndex]);
    lineIndex++;
    nextRevealAt = startTime + randomBetween(earlyMinDelayMs, earlyMaxDelayMs);

    while (lineIndex < lines.length) {
        const now = Date.now();
        const elapsed = now - startTime;
        const hasReachedFastFinishGate = elapsed >= fastRevealWindowMs;
        const canFastFinish = hasReachedFastFinishGate && runtimeLoaded;

        if (canFastFinish) {
            lineIndex = await revealRemainingQuickly(lines, bootLog, startTime, lineIndex, quickFinishWindowMs);
            break;
        }

        if (now >= nextRevealAt) {
            if (elapsed < fastRevealWindowMs) {
                appendLine(bootLog, startTime, 0, lines[lineIndex]);
                lineIndex++;
                nextRevealAt = now + randomBetween(earlyMinDelayMs, earlyMaxDelayMs);
                continue;
            }

            if (!runtimeLoaded) {
                appendLine(bootLog, startTime, 0, lines[lineIndex]);
                lineIndex++;
                nextRevealAt = now + randomBetween(slowMinDelayMs, slowMaxDelayMs);
                continue;
            }
        }

        await wait(40);
    }

    while (!runtimeLoaded && settings.waitForRuntime === true) {
        await wait(70);
    }

    const finishGateAt = Math.max(
        startTime + fastRevealWindowMs,
        runtimeLoadedAt ?? startTime);

    if (lineIndex < lines.length) {
        await wait(Math.max(0, finishGateAt - Date.now()));
        await revealRemainingQuickly(lines, bootLog, startTime, lineIndex, quickFinishWindowMs);
    } else {
        await wait(Math.max(0, finishGateAt + quickFinishWindowMs - Date.now()));
    }

    bootOverlay.style.display = "none";
    if (settings.revealAppOnFinish !== false) {
        appHost.style.display = "";
    }
}

export function setupBankerBoot() {
    return {
        playInitialBoot(runtimeSignal) {
            return playBoot({
                waitForRuntime: true,
                revealAppOnFinish: true,
                hideAppDuringBoot: true,
                runtimeSignal
            });
        },
        playOverlayBoot() {
            return playBoot({
                waitForRuntime: false,
                revealAppOnFinish: false,
                hideAppDuringBoot: false
            });
        }
    };
}