window.setTheme = function (name) {
    document.documentElement.setAttribute('data-theme', name);
};

window.getTheme = function () {
    return document.documentElement.getAttribute('data-theme') || 'crt';
};
