using Arch.Core;
using DCL.Billboard.DebugTools;
using DCL.Billboard.Demo.CameraData;
using DCL.Billboard.Extensions;
using DCL.Billboard.System;
using DCL.CharacterCamera;
using DCL.DemoWorlds;
using DCL.ECSComponents;
using ECS.Unity.Transforms.Components;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.Billboard.Demo.World
{
    public class BillboardDemoWorld : IDemoWorld
    {
        private readonly int countInRow;
        private readonly int randomCounts;
        private readonly float spawnStep;
        private readonly Vector3 cubeSize;
        private readonly BillboardMode[] predefinedBillboards;
        private readonly IDemoWorld origin;
        private readonly IExposedCameraData cameraData;

        public BillboardDemoWorld(
            Arch.Core.World world,
            Vector3 cubeSize = default,
            IExposedCameraData? cameraData = null,
            int countInRow = 10,
            int randomCounts = 50,
            float spawnStep = 3
        ) : this(
            world,
            cubeSize,
            cameraData,
            countInRow,
            randomCounts,
            spawnStep,
            BillboardMode.BmAll, BillboardMode.BmNone, BillboardMode.BmX, BillboardMode.BmY, BillboardMode.BmZ
        ) { }

        public BillboardDemoWorld(
            Arch.Core.World world,
            Vector3 cubeSize,
            IExposedCameraData? cameraData = null,
            int countInRow = 10,
            int randomCounts = 50,
            float spawnStep = 3,
            params BillboardMode[] predefinedBillboards
        )
        {
            this.countInRow = countInRow;
            this.randomCounts = randomCounts;
            this.spawnStep = spawnStep;
            this.predefinedBillboards = predefinedBillboards;
            this.cubeSize = cubeSize;
            this.cameraData = cameraData ?? new FromTransformExposedCameraData();

            origin = new DemoWorld(
                world,
                SetUpWorld,
                NewBillboardSystem
            );
        }

        public void SetUp() =>
            origin.SetUp();

        public void Update() =>
            origin.Update();

        private void SetUpWorld(Arch.Core.World world)
        {
            FillUp(world);
            AssignNames(world);
        }

        private void FillUp(Arch.Core.World world)
        {
            var billboards = predefinedBillboards
                            .Select((e, i) => world.Create(new PBBillboard { BillboardMode = e }, NewTransform(i)))
                            .ToList();

            Enumerable
               .Range(billboards.Count, randomCounts)
               .Select(i => world.Create(RandomBillboard(), NewTransform(i)))
               .ToList();
        }

        private static void AssignNames(Arch.Core.World world)
        {
            var query = new QueryDescription().WithAll<PBBillboard, TransformComponent>();
            world.Query(in query, (ref PBBillboard b, ref TransformComponent t) => t.Transform.name = b.AsString());
        }

        private BillboardSystem NewBillboardSystem(Arch.Core.World world) =>
            new (world, cameraData);

        private TransformComponent NewTransform(int offset = 0)
        {
            Transform t = GameObject.CreatePrimitive(PrimitiveType.Cube)!.transform;

            t.localScale = cubeSize;
            int row = offset % countInRow;
            int column = offset / countInRow;
            t.position = (Vector3.right * row) + (Vector3.forward * column);
            t.position *= spawnStep;

            t.gameObject.AddComponent<GizmosForward>();
            DestroyCollider(t);

            var component = new TransformComponent(t);
            component.SetTransform(t);
            return component;
        }

        private static void DestroyCollider(Transform transform)
        {
            if (transform.TryGetComponent(out Collider collider))
                Object.Destroy(collider!);
        }

        [SuppressMessage("ReSharper", "BitwiseOperatorOnEnumWithoutFlags")]
        private static PBBillboard RandomBillboard()
        {
            var billboard = new PBBillboard();
            billboard.Apply(RandomBool(), RandomBool(), RandomBool());
            return billboard;

            static bool RandomBool() =>
                Random.value > 0.5f;
        }
    }
}
