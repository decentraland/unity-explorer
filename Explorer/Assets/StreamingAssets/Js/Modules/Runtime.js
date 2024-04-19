module.exports.getRealm = async function (message) {
    return await UnityRuntime.GetRealm();
}

module.exports.getWorldTime = async function (message) {
    return await UnityRuntime.GetWorldTime();
}

module.exports.readFile = async function (message) {
    return await UnityRuntime.ReadFile(message.fileName)
}

module.exports.getSceneInformation = function (message) {
    const result = UnityRuntime.GetSceneInformation()
    
    const content = JSON.parse(result.contentJson)
    
    return {
        urn: result.urn,
        content: content
        metadata: result.metadataJson
        baseUrl: result.baseUrl
    }
}