// Responses should always correspond to the protocol definitions at
// https://github.com/decentraland/protocol/blob/main/proto/decentraland/kernel/apis/scene_rewards.proto

// `~system/SceneRewards` pays Credits, and only a scene's authoritative server can
// do that: code running in the explorer never pays. The module exists so a scene
// that imports it still loads here, and the call resolves the same way the server
// answers a scene that earns no rewards.
module.exports.grantReward = async function (message) {
    return { ok: false, error: 'NOT_AVAILABLE' };
}
