using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using SceneRunner.Debugging;
using SceneRunner.Debugging.Hub;
using System.Threading;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Show entity components info.
    ///
    /// Usage:
    ///     /show-entity *scene* *entity*
    /// </summary>
    public class ShowEntityChatCommand : IChatCommand
    {
        public string Command => "show-entity";
        public string Description => "<b>/show-entity <i><scene> <entity></i></b>\n  Show entity components info";

        private readonly IWorldInfoHub worldInfoHub;

        public bool DebugOnly => true;

        public ShowEntityChatCommand(IWorldInfoHub worldInfoHub)
        {
            this.worldInfoHub = worldInfoHub;
        }

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length == 2 && int.TryParse(parameters[1], out _);

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct) =>
            UniTask.FromResult(Execute(parameters));

        private string Execute(string[] parameters)
        {
            (IWorldInfo? world, int id, string? errorMessage) = ArgsFromCommand(parameters);
            return errorMessage ?? world!.EntityComponentsInfo(id);
        }

        private (IWorldInfo? world, int entityId, string? errorMessage) ArgsFromCommand(string[] parameters)
        {
            string sceneName = parameters[1];

            var world = worldInfoHub.WorldInfo(sceneName);

            if (world is null)
                return (null, 0, $"Scene {sceneName} not found");

            var id = int.Parse(parameters[1]);

            return (world, id, null);
        }
    }
}
