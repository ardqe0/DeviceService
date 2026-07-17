window.deviceServiceSession = (() => {
    let timerId;
    let dotNetReference;
    let lastActivityAt = Date.now();
    let inactivityWarningShown = false;
    let expiryWarningShown = false;
    const activityEvents = ["pointerdown", "keydown", "touchstart", "scroll"];

    const registerActivity = () => {
        lastActivityAt = Date.now();
        inactivityWarningShown = false;
    };

    const stop = () => {
        if (timerId) {
            clearInterval(timerId);
            timerId = undefined;
        }
        activityEvents.forEach(eventName => document.removeEventListener(eventName, registerActivity, true));
        dotNetReference = undefined;
    };

    const getOrCreateDevice = () => {
        const storageKey = "device-service-device-id";
        let id = localStorage.getItem(storageKey);
        if (!id) {
            id = window.crypto && typeof window.crypto.randomUUID === "function"
                ? window.crypto.randomUUID()
                : `${Date.now()}-${Math.random().toString(36).slice(2)}`;
            localStorage.setItem(storageKey, id);
        }
        return { id, name: navigator.userAgent || "Bilinmeyen cihaz" };
    };

    const start = (reference, expirationUnixSeconds, inactivitySeconds, inactivityWarningSeconds, expiryWarningSeconds) => {
        stop();
        dotNetReference = reference;
        lastActivityAt = Date.now();
        expiryWarningShown = false;
        activityEvents.forEach(eventName => document.addEventListener(eventName, registerActivity, true));

        const check = async () => {
            if (!dotNetReference) return;
            const now = Date.now();
            const idleSeconds = Math.floor((now - lastActivityAt) / 1000);
            const remainingSeconds = Math.floor((expirationUnixSeconds * 1000 - now) / 1000);

            if (idleSeconds >= inactivitySeconds) {
                await dotNetReference.invokeMethodAsync("EndSession", "inactivity");
                return;
            }
            if (!inactivityWarningShown && idleSeconds >= inactivitySeconds - inactivityWarningSeconds) {
                inactivityWarningShown = true;
                await dotNetReference.invokeMethodAsync("ShowSessionWarning", "inactivity", inactivitySeconds - idleSeconds);
            }
            if (remainingSeconds <= 0) {
                await dotNetReference.invokeMethodAsync("EndSession", "expiration");
                return;
            }
            if (!expiryWarningShown && remainingSeconds <= expiryWarningSeconds) {
                expiryWarningShown = true;
                await dotNetReference.invokeMethodAsync("ShowSessionWarning", "expiration", remainingSeconds);
            }
        };

        check();
        timerId = setInterval(check, 1000);
    };

    return { getOrCreateDevice, start, stop };
})();