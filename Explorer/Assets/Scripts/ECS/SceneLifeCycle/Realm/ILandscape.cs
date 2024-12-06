using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System.Threading;
using UnityEngine;
using Utility.Types;

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

        Result IsParcelInsideTerrain(Vector2Int parcel, bool isLocal);
    }
}
