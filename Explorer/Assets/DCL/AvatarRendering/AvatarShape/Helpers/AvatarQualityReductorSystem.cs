using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using ECS.StreamableLoading.DeferredLoading;

namespace DCL.AvatarRendering.AvatarShape.Helpers
{
    public class AvatarQualityReductorSystem
    {
        private static QueryDescription ALL_AVATARS_QUERY_DESCRIPTION
            = new QueryDescription().WithAll<AvatarBase>()
                                    .WithNone<PlayerComponent>();

        private readonly World World;

        public AvatarQualityReductorSystem(World world)
        {
            World = world;
        }
    }
}
