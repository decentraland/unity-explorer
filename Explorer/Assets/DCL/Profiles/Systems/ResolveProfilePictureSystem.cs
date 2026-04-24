using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Profiles.Helpers;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System;

namespace DCL.Profiles
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.PROFILE)]
    public partial class ResolveProfilePictureSystem : BaseUnityLoopSystem
    {
        public ResolveProfilePictureSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            CompleteProfilePictureDownloadQuery(World);
        }

        [Query]
        private void CompleteProfilePictureDownload(Profile profile)
        {
            if (profile.PicturePromise is not { } promise) return;

            if (promise.IsCancellationRequested(World))
            {
                profile.PicturePromise = null;
                return;
            }

            if (!promise.TryConsume(World, out StreamableLoadingResult<TextureData> result))
            {
                // TryConsume may have cached Result on the struct; persist that state back to the owner.
                profile.PicturePromise = promise;
                return;
            }

            try
            {
                // Guard: the Profile may have been pooled and reused for a different user by the time the texture resolved.
                if (profile.UserId == promise.LoadingIntention.AvatarTextureUserId)
                    profile.ProfilePicture = result.ToFullRectSpriteData(fallback: ProfileUtils.DEFAULT_PROFILE_PIC);
                else
                    result.Asset?.Dispose();
            }
            catch (Exception e)
            {
                result.Asset?.Dispose();
                ReportHub.LogException(e, ReportCategory.PROFILE);
            }
            finally
            {
                profile.PicturePromise = null;
            }
        }
    }
}
