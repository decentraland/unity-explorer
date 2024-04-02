module.exports.signedFetch = async function(message) {
    return  UnitySignedFetch.SignedFetch(message) //TODO compare and try other signature
}

module.exports.getHeaders = async function(message) {
    console.log('JSMODULE: getHeaders')
    return {};
}