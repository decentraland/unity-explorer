module.exports.signedFetch = async function(message) {    
    return UnitySignedFetch.SignedFetch(message.url, message.init.body) //TODO compare and try other signature
}

module.exports.getHeaders = async function(message) {
    return UnitySignedFetch.Headers(message)
}