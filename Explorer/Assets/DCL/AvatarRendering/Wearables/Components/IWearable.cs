using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface IWearable : IAvatarAttachment<WearableDTO>
    {
        WearableType Type { get; }

        /// <summary>
        ///     Per <see cref="BodyShape" /> [MALE, FEMALE]
        /// </summary>
        WearableAssets[] WearableAssetResults { get; }

        /// <summary>
        ///     DTO must be resolved only one
        /// </summary>
        bool TryResolveDTO(StreamableLoadingResult<WearableDTO> result);

        bool TryGetFileHashConditional(BodyShape bodyShape, Func<string, bool> contentMatch, out string? hash);

        void GetHidingList(string bodyShapeType, HashSet<string> hideListResult);

        bool IsCompatibleWithBodyShape(string bodyShape);

        bool HasSameModelsForAllGenders();

        class Fake : IWearable
        {
            private readonly string? mainHash;
            private HashSet<string> expectedUpperWearableHide;

            public bool IsLoading { get; }

            public void UpdateLoadingStatus(bool isLoading)
            {
                //ignore
            }

            public StreamableLoadingResult<SceneAssetBundleManifest>? ManifestResult { get; set; }
            public StreamableLoadingResult<Sprite>? ThumbnailAssetResult { get; set; }
            public AvatarAttachmentDTO DTO { get; }
            public StreamableLoadingResult<WearableDTO> Model { get; set; }

            public WearableType Type { get; }

            public WearableAssets[] WearableAssetResults { get; } = Array.Empty<WearableAssets>();

            public Fake(
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
}
