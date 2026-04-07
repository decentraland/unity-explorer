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
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;

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
        private void CompleteProfilePictureDownload(in Entity entity, ref ProfileTier profile, ref Promise promise)
        {
            if (promise.TryConsume(World, out StreamableLoadingResult<TextureData> result))
            {
                try { profile.ProfilePicture = result.ToFullRectSpriteData(fallback: ProfileUtils.DEFAULT_PROFILE_PIC); }
                catch (Exception e)
                {
                    ReportHub.LogError(ReportCategory.PROFILE, $"Error when processing texture promise for {promise.LoadingIntention.CommonArguments.URL} for profile {profile.UserId} error {e.Message}");
                }
                finally { World.Destroy(entity); }
            }
        }
    }
}
