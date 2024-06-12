using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using SceneRunner.Mapping;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Global.Dynamic.ChatCommands
{
    public class ShowEntityInfoChatCommand : IChatCommand
    {
        private const string COMMAND_SHOW = "show-entity-components";
        private const string EXAMPLE = "/show-entity-components scene_name entity_id";

        public static readonly Regex REGEX = new ($@"^/({COMMAND_SHOW}).*", RegexOptions.Compiled);

        private readonly IReadOnlySceneMapping sceneMapping;

        public ShowEntityInfoChatCommand(IReadOnlySceneMapping sceneMapping)
        {
            this.sceneMapping = sceneMapping;
        }

        public UniTask<string> ExecuteAsync(Match match, CancellationToken ct) =>
            UniTask.FromResult(Execute(match.Value));

        private string Execute(string text)
        {
            var (world, id, errorMessage) = ArgsFromCommand(text);

            if (errorMessage != null)
                return errorMessage;

            object?[]? components = null;

            world!.Query(
                new QueryDescription().WithNone<FindMarker>(),
                entity =>
                {
                    if (entity.Id == id)
                        components = world.GetAllComponents(entity);
                }
            );

            if (components == null)
                return $"Entity not found: {id}";

            var sb = new StringBuilder();
            sb.AppendLine($"Components of entity {id}, total count: {components.Length}");

            foreach (object? component in components)
                sb.AppendLine(component == null ? "NULL_COMPONENT" : component.ToString()!);

            return sb.ToString();
        }

        private struct FindMarker { }

        private (World? world, int entityId, string? errorMessage) ArgsFromCommand(string text)
        {
            string[] split = text.Split(' ')!;

            if (split.Length < 3)
                return (null, 0, $"Invalid command, please provide a valid scene name and entity id as an example: {EXAMPLE}");

            string sceneName = split[1];

            if (sceneName.Length is 0)
                return (null, 0, $"Cannot fetch scene name, please provide a valid scene name as an example: {EXAMPLE}");

            var world = sceneMapping.GetWorld(sceneName);

            if (world is null)
                return (null, 0, $"Scene {sceneName} not found");

            string entityIdString = split[2];

            if (entityIdString.Length is 0)
                return (null, 0, $"Cannot fetch entity id, please provide a valid entity id as an example: {EXAMPLE}");

            if (int.TryParse(entityIdString, out int id) == false)
                return (null, 0, $"Cannot parse entity id: {entityIdString}");

            return (world, id, null);
        }
    }
}
