window.authStorage = {
    setToken: (token) => localStorage.setItem('wf_token', token),
    getToken: () => localStorage.getItem('wf_token'),
    setRefreshToken: (token) => localStorage.setItem('wf_refresh', token),
    getRefreshToken: () => localStorage.getItem('wf_refresh'),
    clear: () => { localStorage.removeItem('wf_token'); localStorage.removeItem('wf_refresh'); }
};
