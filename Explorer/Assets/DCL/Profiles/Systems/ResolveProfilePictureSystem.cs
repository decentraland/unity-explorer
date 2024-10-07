using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Profiles.Helpers;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using UnityEngine;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

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
        private void CompleteProfilePictureDownload(in Entity entity, ref Profile profile, ref Promise promise)
        {
            if (promise.TryConsume(World, out StreamableLoadingResult<Texture2DData> result))
            {
                profile.ProfilePicture = result.ToFullRectSpriteData(ProfileUtils.DEFAULT_PROFILE_PIC);
                World.Destroy(entity);
            }
        }
    }
}
