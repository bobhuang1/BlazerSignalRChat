window.chatScroll = (el) => {
    if (el) el.scrollTop = el.scrollHeight;
};

window.chatFocus = (id) => {
    const el = document.getElementById(id);
    if (el) el.focus();
};