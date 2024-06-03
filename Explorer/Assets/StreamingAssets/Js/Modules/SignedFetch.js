module.exports.signedFetch = async function(message) {
    let body
    let headers
    let method
    if (message.init != undefined) {
        body = message.init.body ?? ''
        headers = JSON.stringify(message.init.headers)
        method = message.init.method ?? ''
    }
    
    return UnitySignedFetch.SignedFetch(message.url, body, headers, method)
}

module.exports.getHeaders = async function(message) {
    return UnitySignedFetch.Headers(message)
}