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

            if (!faceUrl.Value.IsValidUrl())
            {
                // No valid URL: cancel any existing in-flight picture promise for this user since the new profile state invalidates it and set the fallback picture.
                CancelInFlightPromisesForUser(world, profile.UserId, skipEntity: Entity.Null);
                profile.ProfilePicture = new StreamableLoadingResult<SpriteData>.WithFallback(DEFAULT_PROFILE_PIC);
                return;
            }

            // If an in-flight promise with the same URL already exists for this user, redirect its ProfileTier to the new profile instance and reuse the in-flight work.
            // Cancel any other stale promises with different URLs.
            if (TryReuseInFlightPromise(world, profile, faceUrl))
                return;

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

        private static bool TryReuseInFlightPromise(World world, ProfileTier newProfile, URLAddress desiredUrl)
        {
            string userId = newProfile.UserId;
            Entity matchingEntity = Entity.Null;

            world.Query(in PROFILE_PICTURE_PROMISE_QUERY, (Entity entity, ref ProfileTier profile, ref Promise promise) =>
            {
                if (profile.UserId != userId) return;
                if (matchingEntity != Entity.Null) return;
                if (promise.LoadingIntention.CommonArguments.URL != desiredUrl) return;

                matchingEntity = entity;
            });

            if (matchingEntity == Entity.Null)
            {
                CancelInFlightPromisesForUser(world, userId, skipEntity: Entity.Null);
                return false;
            }

            // Redirect the existing promise's ProfileTier to the new profile instance so that when it completes, the result is written to the current profile, not the old one.
            ref ProfileTier tier = ref world.Get<ProfileTier>(matchingEntity);
            tier = newProfile;

            // Cancel any other stale promises for this user (different URL or duplicates).
            CancelInFlightPromisesForUser(world, userId, skipEntity: matchingEntity);
            return true;
        }

        private static void CancelInFlightPromisesForUser(World world, string userId, Entity skipEntity)
        {
            List<Entity> toCancel = ListPool<Entity>.Get();

            world.Query(in PROFILE_PICTURE_PROMISE_QUERY, (Entity entity, ref ProfileTier profile, ref Promise _) =>
            {
                if (profile.UserId != userId) return;
                if (entity == skipEntity) return;

                toCancel.Add(entity);
            });

            foreach (var entity in toCancel)
            {
                ref Promise promise = ref world.Get<Promise>(entity);

                // Consume first to handle late-completion race: if the download already finished, the loaded asset must be disposed of manually (we are discarding it).
                // Otherwise, ForgetLoading cancels the in-flight request cleanly.
                if (promise.TryConsume(world, out StreamableLoadingResult<TextureData> result))
                    result.Asset?.Dispose();
                else
                    promise.ForgetLoading(world);

                world.Destroy(entity);
            }

            ListPool<Entity>.Release(toCancel);
        }
    }
}
