const fetchApi = require('~system/FetchApi');

module.exports.signedFetch = async function(message) {
    let body = ''
    let headers = ''
    let method = ''
    if (message.init != undefined) {
        body = message.init.body ?? ''
        headers = JSON.stringify(message.init.headers)
        method = message.init.method ?? ''
    }
    
    let response = await UnitySignedFetch.SignedFetch(message.url, body, headers, method);
    
    response = { ...response };
    response.headers = new fetchApi.RequestHeaders(response.headers);
    
    return response;
}

module.exports.getHeaders = async function(message) {
    return UnitySignedFetch.Headers(message)
}