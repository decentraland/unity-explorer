using Arch.Core;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.Profiles.Helpers
{
    public static class ProfileUtils
    {
        internal static readonly SpriteData DEFAULT_PROFILE_PIC = Texture2D.grayTexture.ToUnownedFulLRectSpriteData();

        public static void CreateProfilePicturePromise(Profile profile, World world, IPartitionComponent partitionComponent)
        {
            if (string.IsNullOrEmpty(profile.Avatar.FaceSnapshotUrl.Value))
            {
                profile.ProfilePicture = new StreamableLoadingResult<SpriteData>.WithFallback(DEFAULT_PROFILE_PIC);
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
