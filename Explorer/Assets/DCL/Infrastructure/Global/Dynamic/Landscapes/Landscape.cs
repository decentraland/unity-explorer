using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.Landscape;
using DCL.Landscape.Config;
using DCL.Landscape.Settings;
using DCL.RealmNavigation;
using DCL.Utilities;
using ECS;
using ECS.SceneLifeCycle.Realm;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using GPUInstancerPro;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;
using Utility.Types;

namespace Global.Dynamic.Landscapes
{
    public class Landscape : ILandscape
    {
        private readonly IGlobalRealmController realmController;
        private readonly TerrainGenerator genesisTerrain;
        private readonly WorldTerrainGenerator worldsTerrain;
        private readonly bool landscapeEnabled;
        private readonly bool isLocalSceneDevelopment;

        public Landscape(IGlobalRealmController realmController, TerrainGenerator genesisTerrain, WorldTerrainGenerator worldsTerrain, bool landscapeEnabled, bool isLocalSceneDevelopment)
        {
            this.realmController = realmController;
            this.genesisTerrain = genesisTerrain;
            this.worldsTerrain = worldsTerrain;
            this.landscapeEnabled = landscapeEnabled;
            this.isLocalSceneDevelopment = isLocalSceneDevelopment;
        }

        public async UniTask<EnumResult<LandscapeError>> LoadTerrainAsync(AsyncLoadProcessReport landscapeLoadReport, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return EnumResult<LandscapeError>.CancelledResult(LandscapeError.MessageError);

            if (landscapeEnabled == false)
                return EnumResult<LandscapeError>.ErrorResult(LandscapeError.LandscapeDisabled);

            if (realmController.RealmData.IsGenesis())
            {
                //TODO (Juani): The globalWorld terrain would be hidden. We need to implement the re-usage when going back
                worldsTerrain.SwitchVisibility(false);

                if (!genesisTerrain.IsTerrainGenerated)
                    await genesisTerrain.GenerateGenesisTerrainAndShowAsync(processReport: landscapeLoadReport,
                        cancellationToken: ct, hideTrees: LandscapeData.LOAD_TREES_FROM_STREAMINGASSETS);
                else
                    await genesisTerrain.ShowAsync(landscapeLoadReport);
            }
            else
            {
                genesisTerrain.Hide();

                if (realmController.RealmData.IsLocalScene())
                    await GenerateStaticScenesTerrainAsync(landscapeLoadReport, ct);
                else
                    await GenerateFixedScenesTerrainAsync(landscapeLoadReport, ct);
            }

            return EnumResult<LandscapeError>.SuccessResult();
        }

        //TODO should it accept isLocal instead of encapsulating it?
        public Result IsParcelInsideTerrain(Vector2Int parcel, bool isLocal)
        {
            IContainParcel terrain = isLocal && !realmController.RealmData.IsGenesis() ? worldsTerrain : genesisTerrain;

            return !terrain.Contains(parcel)
                ? Result.ErrorResult($"Parcel {parcel} is outside of the bounds.")
                : Result.SuccessResult();
        }

        private async UniTask GenerateStaticScenesTerrainAsync(AsyncLoadProcessReport landscapeLoadReport, CancellationToken ct)
        {
            if (!worldsTerrain.IsInitialized)
                return;

            var staticScenesEntityDefinitions = await realmController.WaitForStaticScenesEntityDefinitionsAsync(ct);
            if (!staticScenesEntityDefinitions.HasValue) return;

            int parcelsAmount = staticScenesEntityDefinitions.Value.Value.Count;

            using (var parcels = new NativeParallelHashSet<int2>(parcelsAmount, AllocatorManager.Persistent))
            {
                foreach (var staticScene in staticScenesEntityDefinitions.Value.Value)
                {
                    foreach (Vector2Int parcel in staticScene.metadata.scene.DecodedParcels) { parcels.Add(parcel.ToInt2()); }
                }

                await worldsTerrain.GenerateTerrainAsync(parcels, (uint)realmController.RealmData.GetHashCode(), landscapeLoadReport, cancellationToken: ct);
            }
        }

        private async UniTask GenerateFixedScenesTerrainAsync(AsyncLoadProcessReport landscapeLoadReport, CancellationToken ct)
        {
            if (!worldsTerrain.IsInitialized)
                return;

            AssetPromise<SceneEntityDefinition, GetSceneDefinition>[]? promises = await realmController.WaitForFixedScenePromisesAsync(ct);

            var parcelsAmount = 0;

            foreach (AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise in promises)
                parcelsAmount += promise.Result!.Value.Asset!.metadata.scene.DecodedParcels.Count;

            using (var parcels = new NativeParallelHashSet<int2>(parcelsAmount, AllocatorManager.Persistent))
            {
                foreach (AssetPromise<SceneEntityDefinition, GetSceneDefinition> promise in promises)
                {
                    foreach (Vector2Int parcel in promise.Result!.Value.Asset!.metadata.scene.DecodedParcels)
                        parcels.Add(parcel.ToInt2());
                }

                await worldsTerrain.GenerateTerrainAsync(parcels, (uint)realmController.RealmData.GetHashCode(), landscapeLoadReport, cancellationToken: ct);
            }
        }

        private static string TreeFilePath => $"{Application.streamingAssetsPath}/Trees.bin";

        private static void SaveTrees(ChunkModel[] chunks)
        {
            TreePrototype[] treePrototypes = chunks[0].TerrainData.treePrototypes;
            var treeTransforms = new List<Matrix4x4>[treePrototypes.Length];

            for (int i = 0; i < treeTransforms.Length; i++)
                treeTransforms[i] = new List<Matrix4x4>();

            foreach (ChunkModel chunk in chunks)
            {
                Vector3 terrainPosition = chunk.terrain.GetPosition();
                Vector3 terrainSize = chunk.TerrainData.size;

                foreach (TreeInstance tree in chunk.TerrainData.treeInstances)
                {
                    Vector3 position = Vector3.Scale(tree.position, terrainSize) + terrainPosition;
                    Quaternion rotation = Quaternion.Euler(0f, tree.rotation * Mathf.Rad2Deg, 0f);
                    Vector3 scale = new Vector3(tree.widthScale, tree.heightScale, tree.widthScale);
                    treeTransforms[tree.prototypeIndex].Add(Matrix4x4.TRS(position, rotation, scale));
                }
            }

            using (var stream = new FileStream(TreeFilePath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false)))
            {
                // To help us allocate the correct size buffer when loading.
                writer.Write(treeTransforms.Max(i => i.Count));

                foreach (List<Matrix4x4> transforms in treeTransforms)
                {
                    writer.Write(transforms.Count);

                    foreach (Matrix4x4 transform in transforms)
                        for (int i = 0; i < 16; i++)
                            writer.Write(transform[i]);
                }
            }
        }

#if GPUI_PRO_PRESENT
        private static unsafe void LoadTrees(LandscapeAsset[] treePrototypes, Transform terrainRoot)
        {
            const int SIZE_OF_MATRIX = 64;

            if (SIZE_OF_MATRIX != sizeof(Matrix4x4))
                throw new Exception("The size of the Matrix4x4 struct is wrong");

            var rendererKeys = new int[treePrototypes.Length];

            for (int i = 0; i < treePrototypes.Length; i++)
                GPUICoreAPI.RegisterRenderer(terrainRoot, treePrototypes[i].asset, out rendererKeys[i]);

            using (var stream = new FileStream(TreeFilePath, FileMode.Open, FileAccess.Read,
                       FileShare.Read))
            using (var reader = new BinaryReader(stream, new UTF8Encoding(false)))
            {
                int maxInstanceCount = reader.ReadInt32();
                var buffer = new Matrix4x4[maxInstanceCount];

                for (int i = 0; i < treePrototypes.Length; i++)
                {
                    int instanceCount = reader.ReadInt32();

                    fixed (Matrix4x4* bufferPtr = buffer)
                        ReadReliably(reader, new Span<byte>(bufferPtr, instanceCount * SIZE_OF_MATRIX));

                    GPUICoreAPI.SetTransformBufferData(rendererKeys[i], buffer, count: instanceCount);
                }
            }
        }

        private static void ReadReliably(BinaryReader reader, Span<byte> buffer)
        {
            while (buffer.Length > 0)
            {
                int read = reader.Read(buffer);

                if (read <= 0)
                    throw new EndOfStreamException("Read zero bytes");

                buffer = buffer.Slice(read);
            }
        }
#endif
    }
}
