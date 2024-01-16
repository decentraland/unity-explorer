using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Billboard.Demo.Properties;
using DCL.DemoWorlds;
using DCL.ECSComponents;
using DCL.SDKComponents.NftShape.Component;
using DCL.SDKComponents.NftShape.Frames.Pool;
using DCL.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.SDKComponents.NftShape.Demo
{
    public class AllNftShapePlaygroundBoot : MonoBehaviour
    {
        [SerializeField]
        private NftShapeProperties nftShapeProperties = new ();
        [SerializeField]
        private bool visible = true;
        [SerializeField]
        private NftShapeSettings settings = null!;
        [Header("Grid")]
        [SerializeField] private int countInRow = 5;
        [SerializeField] private float distanceBetween = 1f;

        private void Start()
        {
            var world = World.Create();
            var framesPool = new FramesPool(settings.EnsureNotNull());

            new SeveralDemoWorld(
                    AllFrameTypes()
                       .Select(e => nftShapeProperties.With(e))
                       .Select(e => new WarmUpSettingsNftShapeDemoWorld(world, framesPool, e, () => visible) as IDemoWorld)
                       .Append(new GridDemoWorld(world, countInRow, distanceBetween))
                       .ToList()
                ).SetUpAndRunAsync(destroyCancellationToken)
                 .Forget();
        }

        private IEnumerable<NftFrameType> AllFrameTypes() =>
            Enum.GetNames(typeof(NftFrameType)).Select(Enum.Parse<NftFrameType>);
    }
}
