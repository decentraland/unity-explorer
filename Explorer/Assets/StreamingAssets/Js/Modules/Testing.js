// Responses should always correspond to the protocol definitions at
// https://github.com/decentraland/protocol/blob/main/proto/decentraland/kernel/apis/testing.proto

module.exports.logTestResult = async function(message) {
    console.log('JSMODULE: logTestResult')
    return {};
}

module.exports.plan = async function(message) {
    console.log('JSMODULE: plan')
    return {};
}

module.exports.setCameraTransform = async function(message) {
    console.log('JSMODULE: setCameraTransform')
    return {};
}
