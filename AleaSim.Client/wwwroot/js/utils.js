
window.aleaUtils = {
    downloadFile: (fileName, contentType, base64Data) => {
        const link = document.createElement('a');
        link.download = fileName;
        link.href = `data:${contentType};base64,${base64Data}`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    },
    downloadTextFile: (fileName, contentType, text) => {
        const blob = new Blob([text], { type: contentType });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    },
    setBackgroundColor: (color) => {
        document.documentElement.style.setProperty('--dynamic-bg', color);
        document.body.style.backgroundColor = color;
    }
};
