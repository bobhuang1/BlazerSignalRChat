window.chatScroll = (el) => {
    if (el) el.scrollTop = el.scrollHeight;
};

window.chatFocus = (id) => {
    const el = document.getElementById(id);
    if (el) {
        el.focus();
        try { el.setSelectionRange(el.value.length, el.value.length); } catch { }
    }
};

// Closes popovers (emoji picker / reaction bar) when the user clicks anywhere
// outside them. Re-bound whenever a circuit connects.
window.chatBindCloser = (dotNetRef, selector, triggerSelector) => {
    window.__chatCloser && document.removeEventListener('mousedown', window.__chatCloser);
    window.__chatCloser = (e) => {
        if (e.target.closest(selector)) return;
        if (triggerSelector && e.target.closest(triggerSelector)) return;
        dotNetRef.invokeMethodAsync('ClosePopovers');
    };
    document.addEventListener('mousedown', window.__chatCloser);
};

window.chatUnbindCloser = () => {
    if (window.__chatCloser) {
        document.removeEventListener('mousedown', window.__chatCloser);
        window.__chatCloser = null;
    }
};
