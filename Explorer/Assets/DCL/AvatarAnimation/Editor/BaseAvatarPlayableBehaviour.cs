
using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using Global.Dynamic;
using UnityEngine.Playables;

namespace DCL.AvatarAnimation
{
    /// <summary>
    /// A playable / clip for the Unity timeline that performs some animations on an AvatarBase.
    /// Inherit from this class to create new playable behaviours for the avatar.
    /// </summary>
    public class BaseAvatarPlayableBehaviour : PlayableBehaviour
    {
        static readonly private QueryDescription ALL_AVATARS_QUERY_DESCRIPTION = new QueryDescription().WithAll<AvatarBase>();

        /// <summary>
        /// Gets the Entity corresponding to the AvatarBase assigned to the track. It may be Entity.Null.
        /// </summary>
        protected Entity cachedEntity
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the AvatarBase assigned to the track. It may be null.
        /// </summary>
        protected AvatarBase cachedAvatar
        {
            get;
            private set;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            // Gets the asset associated to the track
            AvatarBase avatar = (AvatarBase)playerData;

            // If the asset was changed in the editor, update the cached data
            if (avatar != cachedAvatar)
            {
                cachedAvatar = avatar;
                cachedEntity = FindEntityFromAvatarBase(cachedAvatar);

                OnAvatarChanged(cachedEntity, cachedAvatar);
            }
        }

        /// <summary>
        /// Called when a different avatar (or none) is set in the track. Override it when you need to cache something related to the new avatar.
        /// </summary>
        /// <param name="newEntity">The entity of the new avatar. It may be Entity.Null.</param>
        /// <param name="newAvatar">The new avatar. It may be null.</param>
        protected virtual void OnAvatarChanged(Entity newEntity, AvatarBase newAvatar)
        {
            // Override when necessary
        }

        private static Entity FindEntityFromAvatarBase(AvatarBase avatar)
        {
            Entity foundEntity = Entity.Null;
            Query allAvatars = GlobalWorld.ECSWorldInstance.Query(ALL_AVATARS_QUERY_DESCRIPTION);

            foreach (ref var chunk in allAvatars)
            {
                AvatarBase[] avatars = chunk.GetArray<AvatarBase>();

                foreach (int entityIndex in chunk)
                    if (entityIndex > -1 && avatars[entityIndex] == avatar)
                    {
                        foundEntity = chunk.Entity(entityIndex);
                        break;
                    }

                if (foundEntity != Entity.Null)
                    break;
            }

            return foundEntity;
        }
    }
}
