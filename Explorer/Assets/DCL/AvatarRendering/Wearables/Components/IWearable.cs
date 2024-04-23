using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface IWearable : IAvatarAttachment
    {
        WearableType Type { get; }

        StreamableLoadingResult<WearableDTO> WearableDTO { get; }

        /// <summary>
        ///     Per <see cref="BodyShape" /> [MALE, FEMALE]
        /// </summary>
        WearableAssets[] WearableAssetResults { get; }

        /// <summary>
        ///     DTO must be resolved only one
        /// </summary>
        void ResolveDTO(StreamableLoadingResult<WearableDTO> result);

        bool TryGetFileHashConditional(BodyShape bodyShape, Func<string, bool> contentMatch, out string? hash);

        void GetHidingList(string bodyShapeType, HashSet<string> hideListResult);

        bool IsCompatibleWithBodyShape(string bodyShape);

        bool HasSameModelsForAllGenders();
    }
}
