using Cysharp.Threading.Tasks;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Atlas
{
    internal class ParcelChunkAtlasController : MapLayerControllerBase, IAtlasController
    {
        public delegate UniTask<IChunkController> ChunkBuilder(Vector3 chunkLocalPosition, Vector2Int coordsCenter, Transform parent, CancellationToken ct);

        public const int CHUNKS_CREATED_PER_BATCH = 5;

        private readonly int chunkSize;
        private readonly int parcelsInsideChunk;
        private readonly ChunkBuilder chunkBuilder;
        private readonly List<IChunkController> chunks;

        private int parcelSize => coordsUtils.ParcelSize;

        private bool isFetched;

        public ParcelChunkAtlasController(Transform parent, int chunkSize,
            ICoordsUtils coordsUtils, IMapCullingController cullingController, ChunkBuilder chunkBuilder)
            : base(parent, coordsUtils, cullingController)
        {
            this.chunkSize = chunkSize;
            this.chunkBuilder = chunkBuilder;

            var worldSize = ((Vector2)coordsUtils.WorldMaxCoords - coordsUtils.WorldMinCoords) * parcelSize;
            var chunkAmounts = new Vector2Int(Mathf.CeilToInt(worldSize.x / this.chunkSize), Mathf.CeilToInt(worldSize.y / this.chunkSize));
            chunks = new List<IChunkController>(chunkAmounts.x * chunkAmounts.y);
            parcelsInsideChunk = Mathf.Max(1, chunkSize / parcelSize);
        }

        public UniTask InitializeAsync(CancellationToken ct)
        {
            //Lazy Initialization to avoid unnecessary memory usage.
            //This images are not squared neither compressed. They takea big chunk of memory
            //Whatsmore, they are not commonly used. So, we can lazily get them
            //TODO: If we want to do it on initialization, we should work on the images from this map
            return UniTask.CompletedTask;
        }

        private async UniTask LocalInitializeAsync()
        {
            ClearCurrentChunks();
            float halfParcelSize = parcelSize * 0.5f;

            List<UniTask<IChunkController>> chunksCreating = new List<UniTask<IChunkController>>(CHUNKS_CREATED_PER_BATCH);

            for (int i = coordsUtils.WorldMinCoords.x; i <= coordsUtils.WorldMaxCoords.x; i += parcelsInsideChunk)
            {
                for (int j = coordsUtils.WorldMinCoords.y; j <= coordsUtils.WorldMaxCoords.y; j += parcelsInsideChunk)
                {
                    if (chunksCreating.Count >= CHUNKS_CREATED_PER_BATCH)
                    {
                        chunks.AddRange(await UniTask.WhenAll(chunksCreating));
                        chunksCreating.Clear();
                    }

                    Vector2Int coordsCenter = new Vector2Int(i, j);

                    // Subtract half parcel size to displace the pivot, this allow easier PositionToCoords calculations.
                    Vector3 localPosition = new Vector3((parcelSize * i) - halfParcelSize, (parcelSize * j) - halfParcelSize, 0);

                    var instance = chunkBuilder.Invoke(chunkLocalPosition: localPosition, coordsCenter, instantiationParent, CancellationToken.None);
                    chunksCreating.Add(instance);
                }
            }

            if (chunksCreating.Count >= 0)
            {
                chunks.AddRange(await UniTask.WhenAll(chunksCreating));
                chunksCreating.Clear();
            }
        }

        private void ClearCurrentChunks()
        {
            foreach (IChunkController chunk in chunks)
                chunk.Dispose();

            chunks.Clear();
        }

        protected override void DisposeImpl()
        {
            ClearCurrentChunks();
        }

        UniTask IMapLayerController.EnableAsync(CancellationToken cancellationToken)
        {
            if (!isFetched)
            {
                LocalInitializeAsync().Forget();
                isFetched = true;
            }
            instantiationParent.gameObject.SetActive(true);
            return UniTask.CompletedTask;
        }

        UniTask IMapLayerController.Disable(CancellationToken cancellationToken)
        {
            instantiationParent.gameObject.SetActive(false);
            return UniTask.CompletedTask;
        }
    }
}
