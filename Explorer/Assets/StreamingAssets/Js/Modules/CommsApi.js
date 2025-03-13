module.exports.getActiveVideoStreams = async function () {
    const json = await CommsApi.GetActiveVideoStreams();
    const result = JSON.parse(json);
    return result;
}