using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Profiles.Helpers;
using ECS.Abstract;
using ECS.LifeCycle.Components;
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
        [None(typeof(DeleteEntityIntention))]
        private void CompleteProfilePictureDownload(Profile profile)
        {
            if (profile.PicturePromise is not { } promise) return;

            // Consume first: if the download already finished, the asset must be handled here even when cancellation was requested (late-cancel race).
            if (promise.TryConsume(World, out StreamableLoadingResult<TextureData> result))
            {
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

                return;
            }

            // Load hasn't completed yet; if cancellation was signaled, ForgetLoading cleans up the loading entity.
            if (promise.IsCancellationRequested(World))
                profile.PicturePromise = null;
        }
    }
}
