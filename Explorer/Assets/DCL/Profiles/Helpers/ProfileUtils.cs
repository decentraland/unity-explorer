using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.Profiles.Helpers
{
    public static class ProfileUtils
    {
        public static readonly SpriteData DEFAULT_PROFILE_PIC = Texture2D.grayTexture.ToUnownedFulLRectSpriteData();

        private static readonly QueryDescription PROFILE_PICTURE_PROMISE_QUERY = new QueryDescription().WithAll<ProfileTier, Promise>();

        public static void CreateProfilePicturePromise(ProfileTier profile, World world, IPartitionComponent partitionComponent)
        {
            URLAddress faceUrl = profile.FaceSnapshotUrl;

            // Reuse an existing in-flight promise for the same user+URL if present; cancel any stale ones.
            if (ReuseOrCancelInFlightPromisesForUser(world, profile, faceUrl))
                return;

            if (!faceUrl.Value.IsValidUrl())
            {
                profile.ProfilePicture = new StreamableLoadingResult<SpriteData>.WithFallback(DEFAULT_PROFILE_PIC);
                return;
            }

            var promise = Promise.Create(world,
                new GetTextureIntention(userId: profile.UserId,
                    wrapMode: TextureWrapMode.Clamp,
                    filterMode: FilterMode.Bilinear,
                    textureType: TextureType.Albedo,
                    reportSource: nameof(ProfileUtils),
                    faceSnapshotUrl: faceUrl),
                partitionComponent);

            world.Create(profile, promise);
        }

        // Reuses or cancels in-flight (ProfileTier, Promise) entities for the same user; returns true if an existing promise was reused.
        private static bool ReuseOrCancelInFlightPromisesForUser(World world, ProfileTier newProfile, URLAddress desiredUrl)
        {
            Entity reuseEntity = Entity.Null;
            List<Entity> toCancel = ListPool<Entity>.Get();

            world.Query(in PROFILE_PICTURE_PROMISE_QUERY, (Entity entity, ref ProfileTier profile, ref Promise promise) =>
            {
                if (profile.UserId != newProfile.UserId)
                    return;

                if (desiredUrl.Value.IsValidUrl() &&
                    reuseEntity == Entity.Null &&
                    promise.LoadingIntention.CommonArguments.URL == desiredUrl)
                    reuseEntity = entity;
                else
                    toCancel.Add(entity);
            });

            CancelPromises(world, toCancel);
            ListPool<Entity>.Release(toCancel);

            if (reuseEntity == Entity.Null)
                return false;

            // Redirect ProfileTier to the new instance so the promise's eventual completion writes to the current profile, not the old one.
            ref ProfileTier tier = ref world.Get<ProfileTier>(reuseEntity);
            tier = newProfile;
            return true;
        }

        private static void CancelPromises(World world, List<Entity> entities)
        {
            foreach (Entity entity in entities)
            {
                // Copy by value so no ref remains live across the subsequent world.Destroy (§5).
                Promise promise = world.Get<Promise>(entity);

                // Consume-first handles the late-completion race: finished downloads must dispose the asset manually; otherwise ForgetLoading cancels cleanly.
                if (promise.TryConsume(world, out StreamableLoadingResult<TextureData> result))
                    result.Asset?.Dispose();
                else
                    promise.ForgetLoading(world);

                world.Destroy(entity);
            }
        }
    }
}
