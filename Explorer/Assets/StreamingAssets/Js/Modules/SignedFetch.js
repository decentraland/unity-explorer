module.exports.signedFetch = async function(message) {    
    return UnitySignedFetch.SignedFetch(message)
}

module.exports.getHeaders = async function(message) {
    return UnitySignedFetch.Headers(message)
}