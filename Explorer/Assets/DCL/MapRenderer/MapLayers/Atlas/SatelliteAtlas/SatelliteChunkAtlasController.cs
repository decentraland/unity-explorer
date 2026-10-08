using Cysharp.Threading.Tasks;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers.Atlas;
using DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.SatelliteAtlas
{
    internal class SatelliteChunkAtlasController : MapLayerControllerBase, IAtlasController
    {
        public delegate UniTask<IChunkController> ChunkBuilder(Vector3 chunkLocalPosition, Vector2Int coordsCenter, Transform parent, CancellationToken ct);

        private const int CHUNKS_CREATED_PER_BATCH = 5;

        private readonly int gridSize;
        private readonly int parcelsInsideChunk;

        private readonly ChunkBuilder chunkBuilder;
        private readonly List<IChunkController> chunks;
        private readonly SatelliteDetailTiles? detailTiles;
        private readonly Transform genesisCityOcean;
        private readonly Transform bundledChunksRoot;

        public SatelliteChunkAtlasController(Transform parent, Transform genesisCityOcean, int gridSize, int parcelsInsideChunk, ICoordsUtils coordsUtils, IMapCullingController cullingController,
            ChunkBuilder chunkBuilder, SatelliteDetailTiles? detailTiles)
            : base(parent, coordsUtils, cullingController)
        {
            this.genesisCityOcean = genesisCityOcean;
            this.gridSize = gridSize;
            this.parcelsInsideChunk = parcelsInsideChunk;
            this.chunkBuilder = chunkBuilder;
            this.detailTiles = detailTiles;

            var chunkAmounts = new Vector2Int(gridSize, gridSize);
            chunks = new List<IChunkController>(chunkAmounts.x * chunkAmounts.y);

            // The bundled chunks are Genesis City's: they get their own root to hide them in worlds.
            bundledChunksRoot = new GameObject("Bundled chunks") { layer = parent.gameObject.layer }.transform;
            bundledChunksRoot.SetParent(parent, false);
        }

        /// <summary>Shows the satellite map of the world <paramref name="worldName" />, only inside <paramref name="localBounds" /> when they are known.</summary>
        public void ShowWorld(string worldName, Rect? localBounds)
        {
            SetGenesisCityVisible(false);
            detailTiles?.ShowWorld(worldName, localBounds);
        }

        public void ShowGenesisCity()
        {
            SetGenesisCityVisible(true);
            detailTiles?.ShowGenesisCity();
        }

        private void SetGenesisCityVisible(bool visible)
        {
            bundledChunksRoot.gameObject.SetActive(visible);
            genesisCityOcean.gameObject.SetActive(visible);
        }

        public async UniTask InitializeAsync(CancellationToken ct)
        {
            int chunkSpriteSize = parcelsInsideChunk * coordsUtils.ParcelSize;
            Vector3 offset = SatelliteMapOffset();

            detailTiles?.Initialize(new Vector2(offset.x - (chunkSpriteSize / 2f), offset.y + (chunkSpriteSize / 2f)), chunkSpriteSize);

            CancellationToken linkedCt = CancellationTokenSource.CreateLinkedTokenSource(ctsDisposing.Token, ct).Token;

            var chunksCreating = new List<UniTask<IChunkController>>(CHUNKS_CREATED_PER_BATCH);

            for (var i = 0; i < gridSize; i++)
            {
                float x = offset.x + (chunkSpriteSize * i);

                for (var j = 0; j < gridSize; j++)
                {
                    float y = offset.y - (chunkSpriteSize * j);

                    if (chunksCreating.Count >= CHUNKS_CREATED_PER_BATCH)
                    {
                        chunks.AddRange(await UniTask.WhenAll(chunksCreating));
                        chunksCreating.Clear();
                    }

                    var localPosition = new Vector3(x, y, 0);

                    UniTask<IChunkController> instance = chunkBuilder.Invoke(chunkLocalPosition: localPosition, new Vector2Int(i, j), bundledChunksRoot, linkedCt);
                    chunksCreating.Add(instance);
                }
            }

            if (chunksCreating.Count >= 0)
            {
                chunks.AddRange(await UniTask.WhenAll(chunksCreating));
                chunksCreating.Clear();
            }
        }

        private Vector3 SatelliteMapOffset()
        {
            // World minimum plus half size of the chunk to get position (in parcels units) for the center of first chunk
            Vector2Int topLeftCornerChunkCenter = coordsUtils.WorldMinCoords + new Vector2Int(parcelsInsideChunk / 2, parcelsInsideChunk / 2);
            // offset by (3,2) parcels because Satellite image has border parcels outside of the world
            topLeftCornerChunkCenter = new Vector2Int(topLeftCornerChunkCenter.x - 3, Math.Abs(topLeftCornerChunkCenter.y - 2));

            return coordsUtils.CoordsToPosition(topLeftCornerChunkCenter);
        }

        UniTask IMapLayerController.EnableAsync(CancellationToken cancellationToken)
        {
            instantiationParent.gameObject.SetActive(true);

            // Refreshes skipped while the layer was hidden would otherwise wait for the next camera change.
            detailTiles?.Refresh();
            return UniTask.CompletedTask;
        }

        UniTask IMapLayerController.Disable(CancellationToken cancellationToken)
        {
            instantiationParent.gameObject.SetActive(false);
            return UniTask.CompletedTask;
        }

        protected override void DisposeImpl()
        {
            detailTiles?.Dispose();

            foreach (IChunkController chunk in chunks)
                chunk.Dispose();

            chunks.Clear();
        }
    }
}
