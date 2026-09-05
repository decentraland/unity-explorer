using Arch.Core;
using LocalSceneEmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetSceneEmoteFromLocalSceneIntention>;
using RealmSceneEmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetSceneEmoteFromRealmIntention>;

namespace ECS.Unity.AvatarShape.Components
{
    public struct SDKAvatarShapeComponent
    {
        public Entity GlobalWorldEntity;
        public RealmSceneEmotePromise? RealmSceneEmotePromise;
        public LocalSceneEmotePromise? LocalSceneEmotePromise;
        public string? EmoteId;
        public long EmoteTimestamp;

        public SDKAvatarShapeComponent(Entity globalWorldEntity)
        {
            this.GlobalWorldEntity = globalWorldEntity;
            RealmSceneEmotePromise = null;
            LocalSceneEmotePromise = null;
            EmoteId = null;
            EmoteTimestamp = 0;
        }
    }
}
