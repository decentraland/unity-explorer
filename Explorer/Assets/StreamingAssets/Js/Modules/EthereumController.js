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

module.exports.getUserAccount = async function (message) {
    return UnityEthereumApi.UserAddress()
}

module.exports.requirePayment = async function (message) {
    return UnityEthereumApi.TryPay(message.amount, message.currency, message.toAddress);
}

module.exports.convertMessageToObject = async function (message) {
    let parsedMessage = message

    // Remove `# DCL Signed message` header
    if (message.indexOf('# DCL Signed message') === 0) {
        parsedMessage = message.slice(21)
    }
    // First, split the string parts into nested array
    const arr = parsedMessage
        .split('\n')
        .map((m) => m.split(':'))
        .map(([key, value]) => [key, value.trim()])

    // convert the array into object of type MessageDict
    return arr.reduce((o, [key, value]) => ({...o, [key]: value}), {})
}