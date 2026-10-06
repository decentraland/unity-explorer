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

        /// <summary>Replaces the entity's profile with a copy of <paramref name="profile"/> that takes over its picture, and disposes the replaced one; no-op without a profile.</summary>
        public static void ReplaceOnEntity(World world, Entity entity, Profile profile)
        {
            // Set throws on an entity without the component.
            if (!world.TryGet(entity, out Profile? replaced))
                return;

            Profile onEntity = new ProfileBuilder().From(profile).Build();

            if (replaced != null)
                InheritDynamicState(replaced, onEntity);

            onEntity.IsDirty = true;
            world.Set(entity, onEntity);

            // An inherited picture is kept; a download would replace it without releasing it.
            if (onEntity.ProfilePicture == null)
                CreateProfilePicturePromise(onEntity, world, PartitionComponent.TOP_PRIORITY);
            replaced?.Dispose();
        }

        /// <summary>Moves the loaded picture and its in-flight download to the replacement when the snapshot URL is unchanged.</summary>
        public static void InheritDynamicState(ProfileTier from, ProfileTier to)
        {
            // Only inherit if the snapshot URL hasn't changed — otherwise the old picture/promise is stale.
            if (from.FaceSnapshotUrl != to.FaceSnapshotUrl)
                return;

            // Detach on the source after transfer so Dispose doesn't cancel or dereference work we just moved.
            to.ProfilePicture = from.ProfilePicture;
            from.ProfilePicture = null;

            if (from.IsFull(out Profile? fromFull) && to.IsFull(out Profile? toFull))
            {
                toFull.PicturePromise = fromFull.PicturePromise;
                fromFull.PicturePromise = null;
            }
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
