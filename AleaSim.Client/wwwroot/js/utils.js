
window.aleaUtils = {
    downloadFile: (fileName, contentType, base64Data) => {
        const link = document.createElement('a');
        link.download = fileName;
        link.href = `data:${contentType};base64,${base64Data}`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    },
    setBackgroundColor: (color) => {
        document.documentElement.style.backgroundColor = color;
        document.body.style.backgroundColor = color;
    }
};
