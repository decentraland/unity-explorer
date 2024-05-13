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

        void AddNonRealScene(IReadOnlyList<Vector2Int> parcels);
        void RemoveNonRealScene(IReadOnlyList<Vector2Int> parcels);

        void RemoveSceneFacade(IReadOnlyList<Vector2Int> parcels);

        bool Contains(Vector2Int parcel);

        bool TryGetByParcel(Vector2Int parcel, out ISceneFacade sceneFacade);

        void Clear();
    }

    public class ScenesCache : IScenesCache
    {
        private readonly Dictionary<Vector2Int, ISceneFacade> scenesByParcels = new (PoolConstants.SCENES_COUNT * 2);
        private readonly HashSet<Vector2Int> nonRealSceneByParcel = new (PoolConstants.SCENES_COUNT * 2);


        private readonly HashSet<ISceneFacade> scenes = new (PoolConstants.SCENES_COUNT);

        public IReadOnlyCollection<ISceneFacade> Scenes => scenes;

        public void Add(ISceneFacade sceneFacade, IReadOnlyList<Vector2Int> parcels)
        {
            for (var i = 0; i < parcels.Count; i++)
                scenesByParcels.Add(parcels[i], sceneFacade);

            scenes.Add(sceneFacade);
        }

        public void AddNonRealScene(IReadOnlyList<Vector2Int> parcels)
        {
            foreach (var parcel in parcels)
                nonRealSceneByParcel.Add(parcel);
        }

        public void RemoveNonRealScene(IReadOnlyList<Vector2Int> parcels)
        {
            foreach (var parcel in parcels)
                nonRealSceneByParcel.Remove(parcel);
        }

        public void RemoveSceneFacade(IReadOnlyList<Vector2Int> parcels)
        {
            for (var i = 0; i < parcels.Count; i++)
            {
                if (scenesByParcels.TryGetValue(parcels[i], out var sceneFacade))
                {
                    scenes.Remove(sceneFacade);
                    scenesByParcels.Remove(parcels[i]);
                }
            }
        }


        public bool Contains(Vector2Int parcel) =>
            scenesByParcels.ContainsKey(parcel) || nonRealSceneByParcel.Contains(parcel);

        public bool TryGetByParcel(Vector2Int parcel, out ISceneFacade sceneFacade) =>
            scenesByParcels.TryGetValue(parcel, out sceneFacade);

        public void Clear()
        {
            scenesByParcels.Clear();
            nonRealSceneByParcel.Clear();
            scenes.Clear();
        }
    }
}
