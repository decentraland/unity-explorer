using Arch.Core;
using RealmSceneEmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetSceneEmoteFromRealmIntention>;
using LocalSceneEmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetSceneEmoteFromLocalSceneIntention>;

namespace ECS.Unity.AvatarShape.Components
{
    public struct SDKAvatarShapeComponent
    {
        public Entity GlobalWorldEntity;
        public RealmSceneEmotePromise? RealmSceneEmotePromise;
        public LocalSceneEmotePromise? LocalSceneEmotePromise;

        public SDKAvatarShapeComponent(Entity globalWorldEntity)
        {
            this.GlobalWorldEntity = globalWorldEntity;
            RealmSceneEmotePromise = null;
            LocalSceneEmotePromise = null;
        }
    }
}
