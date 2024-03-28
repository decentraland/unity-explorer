module.exports.getRealm = async function(message) {
    return UnityRuntime.GetRealm();
}

module.exports.getWorldTime = async function(message) {
    const { time } = await UnityRuntime.GetWorldTime()
    if (time === undefined) {
        console.log('JSMODULE: An error ocurred when getting World Time')
        return {};
    } else {
        return { time };
    }
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