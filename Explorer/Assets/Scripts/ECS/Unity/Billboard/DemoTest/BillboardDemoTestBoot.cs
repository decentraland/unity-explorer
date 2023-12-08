using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using ECS.Unity.Billboard.Component;
using ECS.Unity.Billboard.DebugTools;
using ECS.Unity.Billboard.System;
using ECS.Unity.Transforms.Components;
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

        private async void Start()
        {
            var world = World.Create();
            var system = new BillboardSystem(world, new FromTransformExposedCameraData());
            FillUp(world);

            var query = new QueryDescription().WithAll<BillboardComponent, TransformComponent>();
            world.Query(in query, (ref BillboardComponent b, ref TransformComponent t) => t.Transform.name = b.ToString());

            while (this)
            {
                system.Update(Time.deltaTime);
                await UniTask.Yield();
            }
        }

        private void FillUp(World world)
        {
            var billboards = new[] { BillboardMode.BmAll, BillboardMode.BmNone, BillboardMode.BmX, BillboardMode.BmY, BillboardMode.BmZ }
                            .Select((e, i) => world.Create(new BillboardComponent(e), NewTransform(i)))
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
            t.position = Vector3.right * cubeStep * offset;
            t.gameObject.AddComponent<GizmosForward>();
            return new TransformComponent(t);
        }

        private static BillboardComponent RandomBillboard()
        {
            static bool RandomBool()
            {
                return Random.value > 0.5f;
            }

            return new BillboardComponent(RandomBool(), RandomBool(), RandomBool());
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
