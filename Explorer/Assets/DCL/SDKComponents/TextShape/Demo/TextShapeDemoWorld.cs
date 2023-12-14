using Arch.Core;
using DCL.DemoWorlds;
using DCL.DemoWorlds.Extensions;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Renderer.Factory;
using DCL.SDKComponents.TextShape.System;
using ECS.Unity.Transforms.Components;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.TextShape.Demo
{
    public class TextShapeDemoWorld : IDemoWorld
    {
        private readonly IDemoWorld origin;

        public TextShapeDemoWorld(params (PBTextShape textShape, PBVisibilityComponent visibility)[] list) : this(list.AsReadOnly()) { }

        public TextShapeDemoWorld(IReadOnlyList<(PBTextShape textShape, PBVisibilityComponent visibility)> list)
        {
            origin = new DemoWorld(
                World.Create(),
                world =>
                {
                    foreach ((PBTextShape textShape, PBVisibilityComponent visibility) in list)
                        world.Create(textShape, visibility, NewTransform());
                },
                world => new InstantiateTextShapeSystem(world, new TextShapeRendererFactory()),
                world => new UpdateTextShapeSystem(world),
                world => new VisibilityTextShapeSystem(world));
        }

        private static TransformComponent NewTransform() =>
            new (new GameObject("text test"));

        public void SetUp() =>
            origin.SetUp();

        public void Update() =>
            origin.Update();
    }
}
