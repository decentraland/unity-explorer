using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.Profiles.Helpers
{
    public static class ProfileUtils
    {
        public static readonly SpriteData DEFAULT_PROFILE_PIC = Texture2D.grayTexture.ToUnownedFulLRectSpriteData();

        public static void CreateProfilePicturePromise(Profile profile, World world, IPartitionComponent partitionComponent)
        {
            URLAddress faceUrl = profile.Compact.FaceSnapshotUrl;

            // Reuse an existing in-flight promise for the same URL; cancel it if the URL changed or is now invalid.
            if (profile.PicturePromise is { } existing)
            {
                if (faceUrl.Value.IsValidUrl() && existing.LoadingIntention.CommonArguments.URL == faceUrl)
                    return;

                CancelPromise(world, existing);
                profile.PicturePromise = null;
            }

            if (!faceUrl.Value.IsValidUrl())
            {
                profile.ProfilePicture = new StreamableLoadingResult<SpriteData>.WithFallback(DEFAULT_PROFILE_PIC);
                return;
            }

            profile.PicturePromise = Promise.Create(world,
                new GetTextureIntention(userId: profile.UserId,
                    wrapMode: TextureWrapMode.Clamp,
                    filterMode: FilterMode.Bilinear,
                    textureType: TextureType.Albedo,
                    reportSource: nameof(ProfileUtils),
                    faceSnapshotUrl: faceUrl),
                partitionComponent);
        }

        // Consume-first handles the late-completion race: finished downloads must dispose of the asset manually; otherwise ForgetLoading cancels cleanly.
        private static void CancelPromise(World world, Promise promise)
        {
            if (promise.TryConsume(world, out StreamableLoadingResult<TextureData> result))
                result.Asset?.Dispose();
            else
                promise.ForgetLoading(world);
        }
    }
}
