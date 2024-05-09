using DCL.Optimization.Pools;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using DCL.LOD.Components;
using UnityEngine;

namespace ECS.SceneLifeCycle
{
    public interface IScenesCache
    {
        IReadOnlyCollection<ISceneFacade> Scenes { get; }

        void Add(ISceneFacade sceneFacade, IReadOnlyList<Vector2Int> parcels);

        void Add(SceneLODInfo sceneLODInfo, IReadOnlyList<Vector2Int> parcels);

        void RemoveSceneFacade(IReadOnlyList<Vector2Int> parcels);

        void RemoveSceneLOD(IReadOnlyList<Vector2Int> parcels);

        bool Contains(Vector2Int parcel);

        bool TryGetByParcel(Vector2Int parcel, out ISceneFacade sceneFacade);

        void Clear();
    }

    public class ScenesCache : IScenesCache
    {
        private readonly Dictionary<Vector2Int, ISceneFacade> scenesByParcels = new (PoolConstants.SCENES_COUNT * 2);
        private readonly Dictionary<Vector2Int, SceneLODInfo> sceneLODInfoByParcels = new (PoolConstants.SCENES_COUNT * 2);

        private readonly HashSet<ISceneFacade> scenes = new (PoolConstants.SCENES_COUNT);

        public IReadOnlyCollection<ISceneFacade> Scenes => scenes;

        public void Add(ISceneFacade sceneFacade, IReadOnlyList<Vector2Int> parcels)
        {
            for (var i = 0; i < parcels.Count; i++)
                scenesByParcels.Add(parcels[i], sceneFacade);

            scenes.Add(sceneFacade);
        }

        public void Add(SceneLODInfo sceneLOD, IReadOnlyList<Vector2Int> parcels)
        {
            for (int i = 0; i < parcels.Count; i++)
                sceneLODInfoByParcels[parcels[i]] = sceneLOD;
        }

        public void RemoveSceneFacade(IReadOnlyList<Vector2Int> parcels)
        {
            for (var i = 0; i < parcels.Count; i++)
            {
                if (scenesByParcels.TryGetValue(parcels[i], out var sceneFacade))
                {
                    Debug.Log($"VVV removing scene facade from cache:  {sceneFacade.Info.BaseParcel} - {sceneFacade.Info.Name}");
                    scenes.Remove(sceneFacade);
                    scenesByParcels.Remove(parcels[i]);
                }
            }
        }

        public void RemoveSceneLOD(IReadOnlyList<Vector2Int> parcels)
        {
            for (int i = 0; i < parcels.Count; i++)
                sceneLODInfoByParcels.Remove(parcels[i]);
        }

        public bool Contains(Vector2Int parcel) =>
            scenesByParcels.ContainsKey(parcel) || sceneLODInfoByParcels.ContainsKey(parcel);

        public bool TryGetByParcel(Vector2Int parcel, out ISceneFacade sceneFacade) =>
            scenesByParcels.TryGetValue(parcel, out sceneFacade);

        public void Clear()
        {
            scenesByParcels.Clear();
            sceneLODInfoByParcels.Clear();
            scenes.Clear();
        }
    }
}
