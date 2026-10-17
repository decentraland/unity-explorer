// Host-optional module used by the SDK6 compatibility adapter.

module.exports.getTextureSize = async function (body) {
    const size = await UnityAdaptationLayerHelper.GetTextureSize(body.src)
    return { src: body.src, size: { width: size.width, height: size.height } }
}
