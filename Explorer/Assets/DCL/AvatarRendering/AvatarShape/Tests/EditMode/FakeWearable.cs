using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Tests.EditMode
{
    public class FakeWearable : IWearable
    {
        private readonly string? mainHash;
        private HashSet<string> expectedUpperWearableHide;

        public bool IsLoading { get; }

        public void UpdateLoadingStatus(bool isLoading)
        {
            //ignore
        }

        public StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
        public StreamableLoadingResult<SpriteData>.WithFallback? ThumbnailAssetResult { get; set; }
        public AvatarAttachmentDTO DTO { get; }
        public StreamableLoadingResult<WearableDTO> Model { get; set; }

        public WearableType Type { get; }

        public WearableAssets[] WearableAssetResults { get; }

        public FakeWearable(
            WearableDTO dto,
            HashSet<string>? expectedUpperWearableHide = null,
            StreamableLoadingResult<WearableDTO> model = default,
            string? mainHash = null,
            WearableAssets[]? wearableAssetResults = null
        )
        {
            DTO = dto;
            Model = model;
            WearableAssetResults = wearableAssetResults ?? Array.Empty<WearableAssets>();
            this.mainHash = mainHash;
            this.expectedUpperWearableHide = expectedUpperWearableHide ?? new HashSet<string>();
        }

        public bool IsOnChain() =>
            true;

        public bool TryResolveDTO(StreamableLoadingResult<WearableDTO> result) =>
            true;

        public bool TryGetFileHashConditional(BodyShape bodyShape, Func<string, bool> contentMatch, out string? hash)
        {
            hash = mainHash;
            return mainHash != null;
        }

        public void GetHidingList(string bodyShapeType, HashSet<string> hideListResult)
        {
            foreach (string item in expectedUpperWearableHide)
                hideListResult.Add(item);
        }

        public bool IsCompatibleWithBodyShape(string bodyShape) =>
            false;

        public bool HasSameModelsForAllGenders() =>
            false;
    }
}
