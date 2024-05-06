using Arch.Core;
using DCL.DemoWorlds;
using DCL.Utilities.Extensions;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.TextShape.Fonts;
using DCL.SDKComponents.TextShape.System;
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
            origin = new DemoWorld(
                world,
                w =>
                {
                    foreach ((PBTextShape textShape, PBVisibilityComponent visibility, PBBillboard billboard) in list)
                        w.Create(textShape, visibility, billboard, NewTransform());
                },
                // w => new InstantiateTextShapeSystem(w, new GameObjectPool<TextMeshPro>(null)),
                // w => new UpdateTextShapeSystem(w),
                w => new VisibilityTextShapeSystem(w));
        }

        private static TransformComponent NewTransform() =>
            new (new GameObject("text test"));

        public void SetUp() =>
            origin.SetUp();

        public void Update() =>
            origin.Update();
    }
}
