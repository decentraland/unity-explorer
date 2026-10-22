using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Navmap
{
    public interface INavmapCommand : IDisposable
    {
        UniTask ExecuteAsync(CancellationToken ct);

        void Undo();
    }

    public interface INavmapCommand<TParam> : INavmapCommand where TParam : struct
    {
        UniTask ExecuteAsync(TParam? param, CancellationToken ct);
    }

    public struct AdditionalParams
    {
        public readonly bool IsFromSearchResults;
        public readonly Vector2Int? OriginalParcel;
        public readonly bool LandOnOriginalParcel;

        public AdditionalParams(bool isFromSearchResults, Vector2Int? originalParcel = null, bool landOnOriginalParcel = false)
        {
            this.IsFromSearchResults = isFromSearchResults;
            this.OriginalParcel = originalParcel;
            this.LandOnOriginalParcel = landOnOriginalParcel;
        }
    }
}
