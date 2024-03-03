module.exports.getRealm = async function(message) {
    console.log('JSMODULE: getRealm')
    return {};
}

module.exports.getWorldTime = async function(message) {
    const { time } = await UnityRuntime.GetWorldTime()
    return { time };
}

module.exports.readFile = async function(message) {
    const { content, hash } = await UnityRuntime.ReadFile(message.fileName)
    return {
        hash,
        content,
    };
}

module.exports.getSceneInformation = async function(message) {
    console.log('JSMODULE: getSceneInformation')
    return {};
}