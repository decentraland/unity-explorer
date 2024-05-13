using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using Ipfs;
using System.Threading;
using UnityEngine;

namespace DCL.ParcelsService
{
    public interface IRetrieveScene
    {
        /// <summary>
        ///     Initiate a request to retrieve a scene definition from a parcel coordinates.
        ///     The request is dependent on the type of the current realm.
        /// </summary>
        /// <param name="parcel"></param>
        /// <param name="ct"></param>
        /// <returns>Null if the parcel does not belong to the real scene</returns>
        UniTask<SceneEntityDefinition?> ByParcelAsync(Vector2Int parcel, CancellationToken ct);

        World World { get; set; }
    }
}
