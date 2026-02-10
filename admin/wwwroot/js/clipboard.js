// Clipboard helper for Blazor Server — Safari blocks navigator.clipboard
// from async SignalR callbacks, so fall back to execCommand('copy').
window.clipboardCopy = function (text) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
        return navigator.clipboard.writeText(text).then(function () {
            return true;
        }).catch(function () {
            return fallbackCopy(text);
        });
    }
    return Promise.resolve(fallbackCopy(text));
};

function fallbackCopy(text) {
    var ta = document.createElement("textarea");
    ta.value = text;
    ta.style.position = "fixed";
    ta.style.left = "-9999px";
    document.body.appendChild(ta);
    ta.focus();
    ta.select();
    try {
        document.execCommand("copy");
        return true;
    } catch (e) {
        return false;
    } finally {
        document.body.removeChild(ta);
    }
}
