async function messageToString(dict: MessageDict) {
    const header = `# DCL Signed message\n`
    const payload = Object.entries(dict)
        .map(([key, value]) => `${key}: ${value}`)
        .join('\n')

    return header.concat(payload)
}

module.exports.signMessage = async function (message) {
    const stringedMessage = await messageToString(message.message)
    return UnityEthereumApi.SignMessage(stringedMessage)
}

module.exports.sendAsync = async function (message) {
    const result = await UnityEthereumApi.SendAsync(message.id, message.method, message.jsonParams)
    return result;
}