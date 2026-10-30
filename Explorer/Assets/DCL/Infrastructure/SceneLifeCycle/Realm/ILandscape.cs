using Cysharp.Threading.Tasks;
using DCL.Utilities;
using DCL.Utility.Types;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ECS.SceneLifeCycle.Realm
{
    public enum LandscapeError
    {
        MessageError,
        LandscapeDisabled,
        TerrainDataUnavailable,
    }

    public interface ILandscape
    {
        UniTask<EnumResult<LandscapeError>> LoadTerrainAsync(AsyncLoadProcessReport loadReport, CancellationToken ct);

        void GenerateSingleSceneTerrain(IReadOnlyList<Vector2Int> parcels);

        float GetHeight(float x, float z);

        Result IsParcelInsideTerrain(Vector2Int parcel, bool isLocal);
    }
}
