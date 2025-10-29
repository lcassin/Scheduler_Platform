window.downloadFile = function(filename, contentType, content) {
    const byteCharacters = atob(content);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], { type: contentType });
    
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
};

window.getBrowserTimeZone = function() {
    try {
        const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
        
        const tzMap = {
            'America/New_York': 'Eastern Standard Time',
            'America/Chicago': 'Central Standard Time',
            'America/Denver': 'Mountain Standard Time',
            'America/Los_Angeles': 'Pacific Standard Time',
            'America/Phoenix': 'US Mountain Standard Time',
            'America/Anchorage': 'Alaskan Standard Time',
            'Pacific/Honolulu': 'Hawaiian Standard Time',
            'America/Indiana/Indianapolis': 'US Eastern Standard Time',
            'America/Detroit': 'Eastern Standard Time',
            'America/Kentucky/Louisville': 'Eastern Standard Time',
            'America/North_Dakota/Center': 'Central Standard Time',
            'America/Boise': 'Mountain Standard Time',
            'America/Juneau': 'Alaskan Standard Time'
        };
        
        return tzMap[timeZone] || 'Central Standard Time';
    } catch (e) {
        console.error('Error detecting timezone:', e);
        return 'Central Standard Time';
    }
};
