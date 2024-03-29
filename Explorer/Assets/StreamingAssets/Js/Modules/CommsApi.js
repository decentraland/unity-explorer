//only for compatability with old scenes
//@deprecated

module.exports.getActiveVideoStreams = async function () {
    //video is not implemented on unity side yet
    const result = {
        streams: []
    }
    globalThis.CheckerStorage.checker('VideoTracksActiveStreamsResponse').strictCheck(result)
    return result
}