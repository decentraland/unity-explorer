module.exports.getPlayerData = async function (message) {
    return UnityPlayers.PlayerData(message.userId);
}

module.exports.getPlayersInScene = async function (message) {
    return UnityPlayers.PlayersInScene();
}

module.exports.getConnectedPlayers = async function (message) {
    return UnityPlayers.ConnectedPlayers();
}