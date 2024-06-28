using Arch.Core;
using DCL.Diagnostics;
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

            world.Query(
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
                string text = component == null
                    ? "NULL_COMPONENT"
                    : $"{component.GetType().Name} {component}";
                sb.AppendLine($"{itemIndex}) {text}");
                itemIndex++;
            }

            var result = sb.ToString();
            ReportHub.Log(ReportCategory.DEBUG, result);
            return result;
        }

        private struct FindMarker { }
    }
}
