using System;
using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Profiles;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;

namespace DCL.AvatarRendering.Export
{
    using WearablePromise = AssetPromise<WearablesResolution, GetWearablesByPointersIntention>;
    
    public static class VRMExportRequestBuilder
    {
        /// <summary>
        /// Creates a new entity dedicated to VRM export with its own wearable promise.
        /// </summary>
        public static void CreateExportRequest(World world, Profile profile, string savePath, Action onSuccessAction)
        {
            var avatar = profile.Avatar;
            BodyShape bodyShape = avatar.BodyShape;
            
            var intention = WearableComponentsUtils.CreateGetWearablesByPointersIntention(
                bodyShape, 
                avatar.Wearables, 
                avatar.ForceRender);
            
            var promise = WearablePromise.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            
            var avatarShape = new AvatarShapeComponent(
                name: "VRM_Export",
                id: "VRM_Export",
                bodyShape: bodyShape,
                wearablePromise: promise,
                skinColor: avatar.SkinColor,
                hairColor: avatar.HairColor,
                eyesColor: avatar.EyesColor);
            
            var exportIntent = new VRMExportIntention()
            {
                AuthorName = profile.Name,
                SavePath = savePath,
                OnFinishedAction = onSuccessAction
            };
            
            world.Create(avatarShape, exportIntent, PartitionComponent.TOP_PRIORITY);
        }
    }
}