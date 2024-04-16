using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.Profiles.Helpers
{
    public static class ProfileUtils
    {
        internal static readonly Sprite DEFAULT_PROFILE_PIC = Sprite.Create(Texture2D.grayTexture, new Rect(0, 0, 1, 1), new Vector2());

        public static void CreateProfilePicturePromise(Profile profile, World world, IPartitionComponent partitionComponent)
        {
            if (string.IsNullOrEmpty(profile.Avatar.FaceSnapshotUrl.Value))
            {
                profile.ProfilePicture = new StreamableLoadingResult<Sprite>(DEFAULT_PROFILE_PIC);
                return;
            }

            var promise = Promise.Create(world,
                new GetTextureIntention
                {
                    CommonArguments = new CommonLoadingArguments(profile.Avatar.FaceSnapshotUrl),
                },
                partitionComponent);

            world.Create(profile, promise, partitionComponent);
        }
    }
}
