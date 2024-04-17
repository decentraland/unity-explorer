module.exports.getRealm = async function (message) {
    return await UnityRuntime.GetRealm();
}

module.exports.getWorldTime = async function (message) {
    return await UnityRuntime.GetWorldTime();
}

module.exports.readFile = async function (message) {
    return await UnityRuntime.ReadFile(message.fileName)
}

module.exports.getSceneInformation = async function (message) {
    return UnityRuntime.GetSceneInformation()
}