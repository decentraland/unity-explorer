using Arch.Core;
using System.Text;

namespace SceneRunner.Debugging
{
    public class WorldInfo : IWorldInfo
    {
        private readonly World world;

        public WorldInfo(World world)
        {
            this.world = world;
        }

        public string EntityComponentsInfo(int entityId)
        {
            object?[]? components = null;

            world!.Query(
                new QueryDescription().WithNone<FindMarker>(),
                entity =>
                {
                    if (entity.Id == entityId)
                        components = world.GetAllComponents(entity);
                }
            );

            if (components == null)
                return $"Entity not found: {entityId}";

            var sb = new StringBuilder();
            sb.AppendLine($"Components of entity {entityId}, total count: {components.Length}");

            var itemIndex = 1;

            foreach (object? component in components)
            {
                string text = component == null ? "NULL_COMPONENT" : component.ToString()!;
                sb.AppendLine($"{itemIndex}) {text}");
                itemIndex++;
            }

            return sb.ToString();
        }

        private struct FindMarker { }
    }
}
