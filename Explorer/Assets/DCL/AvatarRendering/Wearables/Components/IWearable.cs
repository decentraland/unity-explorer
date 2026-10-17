using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Components
{
    // It implements ITrimmedWearable to allow using trimmed wearables in systems that only need trimmed data, such as the builder flow. Check LoadWearablesByParamSystem and ApplicationParametersWearablesProvider
    public interface IWearable : IAvatarAttachment<WearableDTO>, ITrimmedWearable
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

        new bool IsCompatibleWithBodyShape(string bodyShape);

        bool HasSameModelsForAllGenders();

        bool IsOutlineCompatible();

        public static IWearable NewEmpty() =>
            new Wearable();

        new int Amount { get; set; }

        new void SetAmount(int amount);

        new string GetCategory() =>
            ((IAvatarAttachment)this).GetCategory();

        new string GetRarity() =>
            ((IAvatarAttachment)this).GetRarity();

        new string GetName() =>
            ((IAvatarAttachment)this).GetName();

        new URLPath GetThumbnail() =>
            ((IAvatarAttachment)this).GetThumbnail();

        new URN GetUrn() =>
            ((IAvatarAttachment)this).GetUrn();

        URLPath IThumbnailAttachment.GetThumbnail() =>
            ((IAvatarAttachment)this).GetThumbnail();

        URN IThumbnailAttachment.GetUrn() =>
            ((IAvatarAttachment)this).GetUrn();

        string IThumbnailAttachment.GetHash() =>
            ((IAvatarAttachment)this).GetHash();

        AssetBundleManifestVersion? IThumbnailAttachment.GetAssetBundleManifestVersion() =>
            ((IAvatarAttachment)this).GetAssetBundleManifestVersion();

        string? IThumbnailAttachment.GetContentDownloadUrl() =>
            ((IAvatarAttachment)this).GetContentDownloadUrl();

        string? IThumbnailAttachment.GetEntityId() =>
            ((IAvatarAttachment)this).GetEntityId();

        UniTask<Sprite> IThumbnailAttachment.WaitForThumbnailAsync(int checkInterval, CancellationToken ct) =>
            WaitForThumbnailImplAsync(this, checkInterval, ct);

        private static async UniTask<Sprite> WaitForThumbnailImplAsync(IThumbnailAttachment attachment, int checkInterval, CancellationToken ct)
        {
            do await UniTask.Delay(checkInterval, cancellationToken: ct);
            while (attachment.ThumbnailAssetResult is not { IsInitialized: true });

            return attachment.ThumbnailAssetResult!.Value.Asset;
        }
    }
}
