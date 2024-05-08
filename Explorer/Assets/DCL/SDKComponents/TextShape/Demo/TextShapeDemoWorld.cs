using Arch.Core;
using DCL.DemoWorlds;
using DCL.Utilities.Extensions;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.SDKComponents.TextShape.Fonts;
using DCL.SDKComponents.TextShape.System;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DCL.SDKComponents.TextShape.Demo
{
    public class TextShapeDemoWorld : IDemoWorld
    {
        private readonly IDemoWorld origin;

        public TextShapeDemoWorld(World world, IFontsStorage fontsStorage, params (PBTextShape textShape, PBVisibilityComponent visibility, PBBillboard billboard)[] list) : this(world, fontsStorage, list.AsReadOnly()) { }

        public TextShapeDemoWorld(World world, IFontsStorage fontsStorage, IReadOnlyList<(PBTextShape textShape, PBVisibilityComponent visibility, PBBillboard billboard)> list)
        {
            var pool = new GameObjectPool<TextMeshPro>(null, () => new GameObject().AddComponent<TextMeshPro>());

            var buffer = new EntityEventBuffer<TextMeshPro>(10);

            origin = new DemoWorld(
                world,
                w =>
                {
                    foreach ((PBTextShape textShape, PBVisibilityComponent visibility, PBBillboard billboard) in list)
                        w.Create(textShape, visibility, billboard, NewTransform());
                },
                w => new InstantiateTextShapeSystem(w, pool, fontsStorage, new MaterialPropertyBlock(), new NullPerformanceBudget(), buffer),
                w => new UpdateTextShapeSystem(w, fontsStorage, new MaterialPropertyBlock(), buffer),
                w => new VisibilityTextShapeSystem(w, buffer));
        }

        private static TransformComponent NewTransform() =>
            new (new GameObject("text test"));

        public void SetUp() =>
            origin.SetUp();

        public void Update() =>
            origin.Update();
    }
}
