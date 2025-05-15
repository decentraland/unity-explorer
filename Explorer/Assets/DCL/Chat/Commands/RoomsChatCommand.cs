using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Multiplayer.Connections.RoomHubs;
using System.Threading;
using DCL.Chat.History;

namespace Global.Dynamic.ChatCommands
{
    public class RoomsChatCommand : IChatCommand
    {
        private readonly IRoomHub roomHub;

        public string Command => "room";
        public string Description => "<b>/room <i><start|stop></i></b>\n  Start/Stop livekit rooms";

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length == 1;

        public RoomsChatCommand(IRoomHub roomHub)
        {
            this.roomHub = roomHub;
        }

        public async UniTask<string> ExecuteCommandAsync(ChatChannel channel, string[] parameters, CancellationToken ct)
        {
            string mode = parameters[0];

            switch (mode)
            {
                case "start":
                    bool result = await roomHub.StartAsync();
                    return $"Room started with result: {result}";
                case "stop":
                    roomHub.StopAsync();
                    return "Room stopped";
                default:
                    return $"Command unknown: {mode}";
            }
        }
    }
}
