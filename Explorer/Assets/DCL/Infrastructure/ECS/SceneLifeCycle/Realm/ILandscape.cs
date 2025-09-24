using Cysharp.Threading.Tasks;
using DCL.Utilities;
using DCL.Utility.Types;
using System.Threading;
using UnityEngine;

namespace ECS.SceneLifeCycle.Realm
{
    public enum LandscapeError
    {
        MessageError,
        LandscapeDisabled,
    }

    public interface ILandscape
    {
        UniTask<EnumResult<LandscapeError>> LoadTerrainAsync(AsyncLoadProcessReport loadReport, CancellationToken ct);

        float GetHeight(float x, float z);

        Result IsParcelInsideTerrain(Vector2Int parcel, bool isLocal);
    }
}
