// Clipboard helper for Blazor Server.
//
// Safari (and some other browsers) require clipboard writes to happen in a
// *synchronous* user-gesture handler.  Blazor Server's @onclick round-trips
// through SignalR, so by the time JS.InvokeAsync fires the user activation
// has expired and both navigator.clipboard.writeText AND execCommand('copy')
// are blocked.
//
// Solution: attach a *native* onclick handler via this helper so the copy
// runs entirely in the browser within the original user gesture.

/**
 * Copy the value of the <input> that is the previous sibling of the
 * clicked button, then swap the button text to "Copied!" for 2 seconds.
 *
 * Usage (Razor):
 *   <input class="form-control" value="@text" readonly />
 *   <button class="btn" onclick="clipboardCopyFromSibling(this)">Copy</button>
 */
window.clipboardCopyFromSibling = function (btn) {
    var input = btn.previousElementSibling;
    if (!input) return;
    var text = input.value || input.textContent || "";

    // Try modern API first (works in Chrome, Firefox, Safari 13.1+ with gesture)
    if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).then(function () {
            showCopied(btn);
        }).catch(function () {
            // Fallback for Safari blocking async clipboard
            if (fallbackCopy(text)) showCopied(btn);
        });
    } else {
        if (fallbackCopy(text)) showCopied(btn);
    }
};

/**
 * Legacy Blazor interop entry point — kept for Profile page and any other
 * callers that still use JS.InvokeAsync<bool>("clipboardCopy", text).
 */
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

function showCopied(btn) {
    var original = btn.textContent;
    btn.textContent = "Copied!";
    setTimeout(function () { btn.textContent = original; }, 2000);
}

/**
 * Trigger a file download from a base64-encoded string.
 * Used by the Audit page CSV export.
 */
window.downloadBase64File = function (filename, mimeType, base64) {
    var link = document.createElement("a");
    link.href = "data:" + mimeType + ";base64," + base64;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
