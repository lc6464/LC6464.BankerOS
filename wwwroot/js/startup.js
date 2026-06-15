import { setupBankerStorage } from "./banker-storage.js";
import { setupBankerBoot } from "./banker-boot.js";

function createRuntimeSignal() {
    const runtimeSignal = {};
    runtimeSignal.promise = new Promise(resolve => {
        runtimeSignal.resolve = resolve;
    });

    return runtimeSignal;
}

setupBankerStorage();
window.bankerBoot = setupBankerBoot();

const runtimeSignal = createRuntimeSignal();
window.Blazor.start().then(() => {
    runtimeSignal.resolve();
});

window.bankerBoot.playInitialBoot(runtimeSignal);

if ("serviceWorker" in navigator) {
    navigator.serviceWorker.register("service-worker.js", { updateViaCache: "none" });
}