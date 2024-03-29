using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Profiles.Helpers;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

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
            if (promise.TryConsume(World, out StreamableLoadingResult<Texture2D> result))
            {
                profile.ProfilePicture = new StreamableLoadingResult<Sprite>(
                    result.Succeeded
                        ? Sprite.Create(result.Asset, new Rect(0, 0, result.Asset.width, result.Asset.height),
                            VectorUtilities.OneHalf, 50, 0, SpriteMeshType.FullRect, Vector4.one, false)
                        : ProfileUtils.DEFAULT_PROFILE_PIC);

                World.Destroy(entity);
            }
        }
    }
}
