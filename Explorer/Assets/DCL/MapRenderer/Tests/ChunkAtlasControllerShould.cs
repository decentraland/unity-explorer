using Cysharp.Threading.Tasks;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers.Atlas;
using NSubstitute;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.MapRenderer.Tests
{

    public class ChunkAtlasControllerShould
    {
        private const int PARCEL_SIZE = 20;
        private const int CHUNK_SIZE = 1000;
        private const int FRAME_DELAY = 5;

        private ParcelChunkAtlasController atlasController;
        private ParcelChunkAtlasController.ChunkBuilder builder;
        private int iterationsNumber;


        public void Setup()
        {
            var coordUtils = Substitute.For<ICoordsUtils>();
            coordUtils.ParcelSize.Returns(PARCEL_SIZE);
            coordUtils.WorldMinCoords.Returns(new Vector2Int(-150, -150));
            coordUtils.WorldMaxCoords.Returns(new Vector2Int(175, 175));

            builder = Substitute.For<ParcelChunkAtlasController.ChunkBuilder>();

            atlasController = new ParcelChunkAtlasController(null, CHUNK_SIZE, coordUtils, Substitute.For<IMapCullingController>(), builder);

            var parcelsInsideChunk = CHUNK_SIZE / PARCEL_SIZE;

            iterationsNumber =
                Mathf.CeilToInt((coordUtils.WorldMaxCoords.x - coordUtils.WorldMinCoords.x) / (float)parcelsInsideChunk)
                * Mathf.CeilToInt((coordUtils.WorldMaxCoords.y - coordUtils.WorldMinCoords.y) / (float)parcelsInsideChunk);
        }


        public async Task PerformsCorrectNumberOfIterations()
        {
            builder.Invoke(Arg.Any<Vector3>(), Arg.Any<Vector2Int>(), Arg.Any<Transform>(), Arg.Any<CancellationToken>())
                   .Returns(_ => UniTask.DelayFrame(FRAME_DELAY).ContinueWith(() => Substitute.For<IChunkController>()));

            await atlasController.InitializeAsync(CancellationToken.None);

            builder.Received(iterationsNumber);
        }
    }
}
