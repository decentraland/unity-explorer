using Arch.Core;
using ECS.Unity.Transforms.Components;
using System;
using UnityEngine;

namespace DCL.DemoWorlds
{
    public class GridDemoWorld : IDemoWorld
    {
        private readonly World world;
        private readonly int countInRow;
        private readonly float distanceBetween;

        public GridDemoWorld(World world, int countInRow, float distanceBetween)
        {
            this.world = world;
            this.countInRow = countInRow;
            this.distanceBetween = distanceBetween;
        }

        public void SetUp()
        {
            var z = 0;
            var currentCountInRow = 0;

            world.Query(
                in new QueryDescription().WithAll<TransformComponent>(),
                (ref TransformComponent transformComponent) =>
                {
                    transformComponent.Transform.position = new Vector3(currentCountInRow * distanceBetween, 0, z);
                    currentCountInRow++;

                    if (currentCountInRow == countInRow)
                    {
                        currentCountInRow = 0;
                        z++;
                    }
                }
            );
        }

        public void Update()
        {
            //ignore
        }
    }
}
