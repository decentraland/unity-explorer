module.exports.getActiveVideoStreams = async function () {
    const json = CommsApi.GetActiveVideoStreams();
    const result = JSON.parse(json);
    return result;
}