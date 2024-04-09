using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

//Removed all references to EmoteData in WearableItem
namespace DCL.AvatarRendering.Wearables.Components
{
    public enum WearableType : byte
    {
        Regular,
        BodyShape,
        FacialFeature
    }

    [Serializable]
    public class Wearable : IWearable
    {
        public StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }

        public WearableAssets[] WearableAssetResults { get; } = new WearableAssets[BodyShape.COUNT];

        public StreamableLoadingResult<WearableDTO> WearableDTO { get; private set; }

        public StreamableLoadingResult<Sprite>? ThumbnailAssetResult { get; set; }

        public WearableType Type { get; private set; }

        public bool IsLoading { get; set; } = true;

        public Wearable()
        {
        }

        public Wearable(StreamableLoadingResult<WearableDTO> dto)
        {
            ResolveDTO(dto);
            IsLoading = false;
        }

        public void ResolveDTO(StreamableLoadingResult<WearableDTO> result)
        {
            Assert.IsTrue(!WearableDTO.IsInitialized || !WearableDTO.Succeeded);
            WearableDTO = result;

            if (!result.Succeeded) return;

            IAvatarAttachment avatarAttachment = this;

            if (avatarAttachment.IsFacialFeature())
                Type = WearableType.FacialFeature;
            else if (avatarAttachment.IsBodyShape())
                Type = WearableType.BodyShape;
            else
                Type = WearableType.Regular;
        }

        public AvatarAttachmentDTO GetDTO() =>
            WearableDTO.Asset!;
    }
}
