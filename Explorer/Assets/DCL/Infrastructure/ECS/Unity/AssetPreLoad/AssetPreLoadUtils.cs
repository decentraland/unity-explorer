using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.Unity.AssetLoad
{
    public class AssetPreLoadUtils : IDisposable
    {
        internal readonly Dictionary<string, LoadingUpdate> assetLoadingUpdates = new ();
        private readonly IObjectPool<LoadingUpdate> loadingUpdatePool = new ObjectPool<LoadingUpdate>(
            createFunc: () => new LoadingUpdate(),
            actionOnRelease: state =>
            {
                state.States.Clear();
                ListPool<LoadingState>.Release(state.States);
                state.States = null;
            },
            actionOnDestroy: state =>
            {
                if (state.States == null) return;

                state.States.Clear();
                ListPool<LoadingState>.Release(state.States);
            }
        );

        private LoadingUpdate GetLoadingUpdate(CRDTEntity crdtEntity, LoadingState loadingState)
        {
            LoadingUpdate result = loadingUpdatePool.Get();
            result.CrdtEntity = crdtEntity;
            result.States = ListPool<LoadingState>.Get();
            result.States.Add(loadingState);
            result.LastTick = -1;
            return result;
        }

        public void Dispose()
        {
             foreach (var kvp in assetLoadingUpdates)
                loadingUpdatePool.Release(kvp.Value);

            assetLoadingUpdates.Clear();
        }

        public void AppendAssetLoadingMessage(CRDTEntity crdtEntity, LoadingState loadingState, string assetPath)
        {
            if (assetLoadingUpdates.TryGetValue(assetPath, out var entry))
                entry.States.Add(loadingState);
            else
                assetLoadingUpdates[assetPath] = GetLoadingUpdate(crdtEntity, loadingState);
        }

        public class LoadingUpdate
        {
            public List<LoadingState> States;
            public int LastTick;
            public CRDTEntity CrdtEntity;
        }
    }
}
