// Responses should always correspond to the protocol definitions at
// https://github.com/decentraland/protocol/blob/main/proto/decentraland/kernel/apis/players.proto

module.exports.getPlayerData = async function (message) {
    return UnityPlayers.PlayerData(message.userId);
}

module.exports.getPlayersInScene = function (message) {
    const result = UnityPlayers.PlayersInScene() 
    const players = JSON.parse(result.playersJson)
    return {
        players: players
    };
}

module.exports.getConnectedPlayers = function (message) {
    const result = UnityPlayers.ConnectedPlayers();
    const players = JSON.parse(result.playersJson)
    return {
        players: players
    };
}