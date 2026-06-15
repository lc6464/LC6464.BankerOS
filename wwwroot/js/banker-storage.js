export function setupBankerStorage() {
    window.bankerStorage = {
        getItem(storeName, key) {
            return window[storeName].getItem(key);
        },
        setItem(storeName, key, value) {
            window[storeName].setItem(key, value);
        },
        removeItem(storeName, key) {
            window[storeName].removeItem(key);
        }
    };
}