using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using SceneRunner.Mapping;
using System;
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
            UniTask.FromResult(Execute(match.Value.AsSpan()));

        private string Execute(ReadOnlySpan<char> text)
        {
            int index = COMMAND_SHOW.Length;

            var worldResult = WorldFromCommand(text, ref index);

            if (worldResult.errorMessage != null)
                return worldResult.errorMessage;

            (int id, string? errorMessageForId) = EntityIdFromCommand(text, ref index);

            if (errorMessageForId != null)
                return errorMessageForId;

            object?[]? components = null;

            var world = worldResult.world!;

            world.Query(
                new QueryDescription().WithAll<Entity>(),
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

        private (World? world, string? errorMessage) WorldFromCommand(ReadOnlySpan<char> text, ref int index)
        {
            var sceneName = WhitespaceSliceOrEmpty(text, ref index).ToString()!;

            if (sceneName.Length is 0)
                return (null, $"Cannot fetch scene name, please provide a valid scene name as an example: {EXAMPLE}");

            var world = sceneMapping.GetWorld(sceneName);

            if (world is null)
                return (null, $"Scene {sceneName} not found");

            return (world, null);
        }

        private (int id, string? errorMessage) EntityIdFromCommand(ReadOnlySpan<char> text, ref int index)
        {
            var entityId = WhitespaceSliceOrEmpty(text, ref index);

            if (entityId.Length is 0)
                return (0, $"Cannot fetch entity id, please provide a valid entity id as an example: {EXAMPLE}");

            if (int.TryParse(entityId, out int id) == false)
                return (0, $"Cannot parse entity id: {entityId.ToString()}");

            return (id, null);
        }

        private static ReadOnlySpan<char> WhitespaceSliceOrEmpty(ReadOnlySpan<char> text, ref int currentIndex)
        {
            int start = -1;
            int finish = -1;

            for (; currentIndex < text.Length; currentIndex++)
                if (text[currentIndex] is ' ')
                    start = currentIndex + 1;

            for (; currentIndex < text.Length; currentIndex++)
                if (text[currentIndex] is ' ')
                    finish = currentIndex - 1;

            if (start == -1 || finish == -1)
                return new ReadOnlySpan<char>();

            return text.Slice(start, finish);
        }
    }
}
