module.exports.getRealm = async function(message) {
    return UnityRuntime.GetRealm();
}

module.exports.getWorldTime = async function(message) {
    return UnityRuntime.GetWorldTime();
}

module.exports.readFile = async function(message) {
    const { content, hash } = await UnityRuntime.ReadFile(message.fileName)
    return {
        hash,
        content,
    };
}

module.exports.getSceneInformation = async function(message) {

    const { urn, baseUrl, content, metadataJson } = UnityRuntime.GetSceneInformation()    

    return { 
        urn, 
        baseUrl, 
        content, 
        metadataJson 
    };
}