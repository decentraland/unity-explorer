using DCL.Optimization.Pools;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.SceneLifeCycle
{
    public interface IScenesCache
    {
        IReadOnlyCollection<ISceneFacade> Scenes { get; }

        void Add(ISceneFacade sceneFacade, IReadOnlyList<Vector2Int> parcels);

        void Remove(IReadOnlyList<Vector2Int> parcels);

        bool TryGetByParcel(Vector2Int parcel, out ISceneFacade sceneFacade);

        void Clear();
    }

    public class ScenesCache : IScenesCache
    {
        private readonly Dictionary<Vector2Int, ISceneFacade> scenesByParcels = new (PoolConstants.SCENES_COUNT * 2);

        public IReadOnlyCollection<ISceneFacade> Scenes => scenesByParcels.Values;

        public void Add(ISceneFacade sceneFacade, IReadOnlyList<Vector2Int> parcels)
        {
            //TODO: ASk Misha
            for (var i = 0; i < parcels.Count; i++)
                scenesByParcels.TryAdd(parcels[i], sceneFacade);
        }

        public void Remove(IReadOnlyList<Vector2Int> parcels)
        {
            for (var i = 0; i < parcels.Count; i++)
                scenesByParcels.Remove(parcels[i]);
        }

        public bool TryGetByParcel(Vector2Int parcel, out ISceneFacade sceneFacade) =>
            scenesByParcels.TryGetValue(parcel, out sceneFacade);

        public void Clear()
        {
            scenesByParcels.Clear();
        }
    }
}
