using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Billboard.Extensions;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using ECS.Unity.Billboard.DebugTools;
using ECS.Unity.Billboard.System;
using ECS.Unity.Transforms.Components;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using CameraType = DCL.ECSComponents.CameraType;

namespace ECS.Unity.Billboard.DemoTest
{
    public class BillboardDemoTestBoot : MonoBehaviour
    {
        [SerializeField] private float cubeStep = 3;
        [SerializeField] private Vector3 cubeSize = new Vector3(1.6f, 1, 0.5f);
        [SerializeField] private int randomCounts = 5;
        [SerializeField] private int countInRow = 10;

        private async void Start()
        {
            var world = World.Create();
            var system = new BillboardSystem(world, new FromTransformExposedCameraData());
            FillUp(world);

            var query = new QueryDescription().WithAll<PBBillboard, TransformComponent>();
            world.Query(in query, (ref PBBillboard b, ref TransformComponent t) => t.Transform.name = b.AsString());

            while (this)
            {
                system.Update(Time.deltaTime);
                await UniTask.Yield();
            }
        }

        private void FillUp(World world)
        {
            var billboards = new[] { BillboardMode.BmAll, BillboardMode.BmNone, BillboardMode.BmX, BillboardMode.BmY, BillboardMode.BmZ }
                            .Select((e, i) => world.Create(new PBBillboard { BillboardMode = e }, NewTransform(i)))
                            .ToList();

            Enumerable
               .Range(billboards.Count, randomCounts)
               .Select(i => world.Create(RandomBillboard(), NewTransform(i)))
               .ToList();
        }

        private TransformComponent NewTransform(int offset = 0)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Cube)!.transform!;
            t.localScale = cubeSize;
            int row = offset % countInRow;
            int column = offset / countInRow;

            t.position = (Vector3.right * row) + (Vector3.forward * column);
            t.position *= cubeStep;
            t.gameObject.AddComponent<GizmosForward>();
            var component = new TransformComponent(t);
            component.SetTransform(t);
            return component;
        }

        [SuppressMessage("ReSharper", "BitwiseOperatorOnEnumWithoutFlags")]
        private static PBBillboard RandomBillboard()
        {
            static bool RandomBool()
            {
                return Random.value > 0.5f;
            }

            var mode = BillboardMode.BmNone;

            if (RandomBool()) mode |= BillboardMode.BmX;
            if (RandomBool()) mode |= BillboardMode.BmY;
            if (RandomBool()) mode |= BillboardMode.BmZ;

            return new PBBillboard { BillboardMode = mode };
        }

        private class FromTransformExposedCameraData : IExposedCameraData
        {
            private readonly Transform t;

            public FromTransformExposedCameraData() : this(Camera.main!) { }

            public FromTransformExposedCameraData(Camera camera) : this(camera.transform, CameraType.CtCinematic, false) { }

            public FromTransformExposedCameraData(Transform t, CameraType cameraType, bool pointerIsLocked)
            {
                this.t = t;
                CameraType = cameraType;
                PointerIsLocked = pointerIsLocked;
            }

            public Vector3 WorldPosition => t.position;
            public Quaternion WorldRotation => t.rotation;
            public CameraType CameraType { get; }
            public bool PointerIsLocked { get; }
        }
    }
}
