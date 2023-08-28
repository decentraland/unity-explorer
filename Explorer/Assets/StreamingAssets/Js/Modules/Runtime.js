module.exports.getRealm = async function(message) {
    console.log('JSMODULE: getRealm')
    return {};
}

module.exports.getWorldTime = async function(message) {
    console.log('JSMODULE: getWorldTime')
    return {};
}

module.exports.readFile = async function(message) {
    console.log('JSMODULE: readFile')
    const res = await UnityRuntime.ReadFile(message.fileName)
    console.log('Done...')
    const data = new Uint8Array(res)

    console.log(`data: ${data.length}`)
    return {
        content: data,
        hash: '' // TODO: Get the hash from the read file
    };
}

module.exports.getSceneInformation = async function(message) {
    console.log('JSMODULE: getSceneInformation')
    return {};
}