using DCL.Optimization.Pools;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.SceneLifeCycle
{
    public interface IScenesCache
    {
        IReadOnlyCollection<ISceneFacade> Scenes { get; }
        IReadOnlyCollection<ISceneFacade> PortableExperiencesScenes { get; }
        ISceneFacade? CurrentScene { get; }

        void Add(ISceneFacade sceneFacade, IReadOnlyList<Vector2Int> parcels);

        void AddNonRealScene(IReadOnlyList<Vector2Int> parcels);

        void AddNonRealScene(Vector2Int parcel);

        void AddPortableExperienceScene(ISceneFacade sceneFacade, string sceneUrn);

        void RemoveNonRealScene(IReadOnlyList<Vector2Int> parcels);

        void RemoveSceneFacade(IReadOnlyList<Vector2Int> parcels);

        bool Contains(Vector2Int parcel);

        bool TryGetByParcel(Vector2Int parcel, out ISceneFacade sceneFacade);

        bool TryGetBySceneId(string sceneId, out ISceneFacade? sceneFacade);

        bool TryGetPortableExperienceBySceneUrn(string sceneUrn, out ISceneFacade sceneFacade);

        void RemovePortableExperienceFacade(string sceneUrn);

        void ClearScenes(bool clearPortableExperiences = false);

        void SetCurrentScene(ISceneFacade sceneFacade);
    }

    public class ScenesCache : IScenesCache
    {
        private readonly Dictionary<Vector2Int, ISceneFacade> scenesByParcels = new (PoolConstants.SCENES_COUNT);
        private readonly HashSet<Vector2Int> nonRealSceneByParcel = new (PoolConstants.SCENES_COUNT);
        private readonly Dictionary<string, ISceneFacade> portableExperienceScenesByUrn = new (PoolConstants.PORTABLE_EXPERIENCES_INITIAL_COUNT);

        private readonly HashSet<ISceneFacade> scenes = new (PoolConstants.SCENES_COUNT);

        public IReadOnlyCollection<ISceneFacade> Scenes => scenes;
        public IReadOnlyCollection<ISceneFacade> PortableExperiencesScenes => portableExperienceScenesByUrn.Values;
        public ISceneFacade? CurrentScene { get; private set; }

        public void Add(ISceneFacade sceneFacade, IReadOnlyList<Vector2Int> parcels)
        {
            for (var i = 0; i < parcels.Count; i++)
                scenesByParcels.Add(parcels[i], sceneFacade);

            scenes.Add(sceneFacade);
        }

        public void AddNonRealScene(IReadOnlyList<Vector2Int> parcels)
        {
            foreach (Vector2Int parcel in parcels)
                nonRealSceneByParcel.Add(parcel);
        }

        public void AddNonRealScene(Vector2Int parcel)
        {
            nonRealSceneByParcel.Add(parcel);
        }

        public void RemoveNonRealScene(IReadOnlyList<Vector2Int> parcels)
        {
            foreach (Vector2Int parcel in parcels)
                nonRealSceneByParcel.Remove(parcel);
        }

        public void RemoveSceneFacade(IReadOnlyList<Vector2Int> parcels)
        {
            for (var i = 0; i < parcels.Count; i++)
            {
                if (scenesByParcels.TryGetValue(parcels[i], out ISceneFacade? sceneFacade))
                {
                    scenes.Remove(sceneFacade);
                    scenesByParcels.Remove(parcels[i]);
                }
            }
        }

        public void AddPortableExperienceScene(ISceneFacade sceneFacade, string sceneUrn)
        {
            portableExperienceScenesByUrn.TryAdd(sceneUrn, sceneFacade);
        }

        public bool Contains(Vector2Int parcel) =>
            scenesByParcels.ContainsKey(parcel) || nonRealSceneByParcel.Contains(parcel);

        public bool TryGetByParcel(Vector2Int parcel, out ISceneFacade sceneFacade) =>
            scenesByParcels.TryGetValue(parcel, out sceneFacade);

        public bool TryGetBySceneId(string sceneId, out ISceneFacade? sceneFacade)
        {
            sceneFacade = null;
            foreach (ISceneFacade facade in scenes)
            {
                if (facade.SceneData.SceneEntityDefinition.id!.Equals(sceneId))
                {
                    sceneFacade = facade;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetPortableExperienceBySceneUrn(string urn, out ISceneFacade sceneFacade) =>
            portableExperienceScenesByUrn.TryGetValue(urn, out sceneFacade);

        public void RemovePortableExperienceFacade(string urn)
        {
            portableExperienceScenesByUrn.Remove(urn);
        }

        public void ClearScenes(bool clearPortableExperiences)
        {
            scenesByParcels.Clear();
            nonRealSceneByParcel.Clear();
            if (clearPortableExperiences) portableExperienceScenesByUrn.Clear();
            scenes.Clear();
        }

        public void SetCurrentScene(ISceneFacade sceneFacade)
        {
            if (CurrentScene != sceneFacade)
                CurrentScene = sceneFacade;
        }
    }
}
