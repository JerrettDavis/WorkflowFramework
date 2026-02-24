window.downloadJson = (filename, data) => {
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
};

window.downloadText = (filename, text, mimeType) => {
    const blob = new Blob([text], { type: mimeType || 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
};

window.openFilePicker = async (accept) => {
    return new Promise(resolve => {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = accept;
        input.onchange = async () => {
            if (input.files.length > 0) {
                resolve(await input.files[0].text());
            } else {
                resolve(null);
            }
        };
        input.click();
    });
};
