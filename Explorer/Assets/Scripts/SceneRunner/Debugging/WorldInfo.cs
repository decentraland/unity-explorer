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

            foreach (object? component in components)
                sb.AppendLine(component == null ? "NULL_COMPONENT" : component.ToString()!);

            return sb.ToString();
        }

        private struct FindMarker { }
    }
}
