using Arch.Core;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.Profiles.Helpers
{
    public static class ProfileUtils
    {
        public static readonly SpriteData DEFAULT_PROFILE_PIC = Texture2D.grayTexture.ToUnownedFulLRectSpriteData();

        public static void CreateProfilePicturePromise(Profile profile, World world, IPartitionComponent partitionComponent)
        {
            if (!profile.Avatar.FaceSnapshotUrl.Value.IsValidUrl())
            {
                profile.ProfilePicture = new StreamableLoadingResult<SpriteData>.WithFallback(DEFAULT_PROFILE_PIC);
                return;
            }

            var promise = Promise.Create(world,
                new GetTextureIntention(userId: profile.UserId,
                    wrapMode: TextureWrapMode.Clamp,
                    filterMode: FilterMode.Bilinear,
                    textureType: TextureType.Albedo,
                    reportSource: nameof(ProfileUtils)),
                partitionComponent);

            world.Create(profile, promise, partitionComponent);
        }
    }
}
