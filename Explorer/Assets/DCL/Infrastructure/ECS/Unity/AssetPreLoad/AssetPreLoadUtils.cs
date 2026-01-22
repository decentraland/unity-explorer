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

        public void Dispose()
        {
            ReleaseAllLists();
            assetLoadingUpdates.Clear();
        }

        public void AppendAssetLoadingMessage(CRDTEntity crdtEntity, LoadingState loadingState, string assetPath)
        {
            if (assetLoadingUpdates.TryGetValue(assetPath, out var entry))
                entry.States.Add(loadingState);
            else
            {
                List<LoadingState> states = ListPool<LoadingState>.Get();
                states.Add(loadingState);

                assetLoadingUpdates[assetPath] = new LoadingUpdate(states, -1, crdtEntity);
            }
        }

        private void ReleaseAllLists()
        {
            foreach (var kvp in assetLoadingUpdates)
            {
                kvp.Value.States.Clear();
                ListPool<LoadingState>.Release(kvp.Value.States);
            }
        }

        public class LoadingUpdate
        {
            public readonly List<LoadingState> States;
            public int LastTick;
            public CRDTEntity CrdtEntity;

            public LoadingUpdate(List<LoadingState> states, int lastTick, CRDTEntity crdtEntity)
            {
                this.States = states;
                this.LastTick = lastTick;
                this.CrdtEntity = crdtEntity;
            }
        }
    }
}
