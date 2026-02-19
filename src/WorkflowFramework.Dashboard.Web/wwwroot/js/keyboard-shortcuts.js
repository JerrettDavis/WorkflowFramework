// Keyboard shortcuts interop for Blazor
window.keyboardShortcuts = {
    _dotNetRef: null,
    _handler: null,

    initialize: function (dotNetRef) {
        this._dotNetRef = dotNetRef;
        this._handler = (e) => {
            // Don't intercept when typing in inputs
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA' || e.target.isContentEditable) {
                if (e.key === 'Escape') {
                    e.target.blur();
                }
                return;
            }

            let shortcut = null;

            if (e.ctrlKey || e.metaKey) {
                if (e.shiftKey) {
                    if (e.key === 'S' || e.key === 's') shortcut = 'Ctrl+Shift+S';
                    else if (e.key === 'Z' || e.key === 'z') shortcut = 'Ctrl+Shift+Z';
                } else {
                    if (e.key === 's') shortcut = 'Ctrl+S';
                    else if (e.key === 'n') shortcut = 'Ctrl+N';
                    else if (e.key === 'o') shortcut = 'Ctrl+O';
                    else if (e.key === 'z') shortcut = 'Ctrl+Z';
                    else if (e.key === 'y') shortcut = 'Ctrl+Y';
                    else if (e.key === 'c') shortcut = 'Ctrl+C';
                    else if (e.key === 'v') shortcut = 'Ctrl+V';
                    else if (e.key === 'a') shortcut = 'Ctrl+A';
                    else if (e.key === 'd') shortcut = 'Ctrl+D';
                    else if (e.key === 'Enter') shortcut = 'Ctrl+Enter';
                }
            } else {
                if (e.key === 'Delete') shortcut = 'Delete';
                else if (e.key === 'Backspace') shortcut = 'Backspace';
                else if (e.key === 'Escape') shortcut = 'Escape';
                else if (e.key === '?') shortcut = '?';
                else if (e.key === 'F1') shortcut = 'F1';
            }

            if (shortcut) {
                e.preventDefault();
                dotNetRef.invokeMethodAsync('HandleShortcut', shortcut);
            }
        };
        document.addEventListener('keydown', this._handler);
    },

    destroy: function () {
        if (this._handler) {
            document.removeEventListener('keydown', this._handler);
            this._handler = null;
        }
        this._dotNetRef = null;
    }
};
