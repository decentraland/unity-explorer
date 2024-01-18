using System;
using UnityEngine;
using Random = System.Random;

namespace DCL.Landscape.Config
{
    [CreateAssetMenu(menuName = "Landscape/Noise Data", fileName = "NoiseData", order = 1)]
    public class NoiseData : ScriptableObject
    {
        public NoiseSettings settings = new ()
        {
            scale = 100,
            octaves = 1,
            persistance = 0.3f,
        };
    }

    [Serializable]
    public struct ObjectRandomization
    {
        public Vector2 randomRotationX;
        public Vector2 randomRotationY;
        public Vector2 randomRotationZ;
        public bool proportionalScale;
        public Vector2 randomScale;

        // Hide if bool proportionalScale
        public Vector2 randomScaleX;

        // Hide if bool proportionalScale
        public Vector2 randomScaleY;

        // Hide if bool proportionalScale
        public Vector2 randomScaleZ;

        public Vector2 positionOffsetX;
        public Vector2 positionOffsetY;
        public Vector2 positionOffsetZ;

        public void ApplyRandomness(Transform transform, Random random, float objHeight)
        {
            transform.eulerAngles = RandomVector(random, randomRotationX, randomRotationY, randomRotationZ);

            if (proportionalScale)
                transform.localScale = Vector3.one * RandomRange(random, in randomScale) * objHeight;
            else
                transform.localScale = RandomVector(random, randomScaleX, randomScaleY, randomScaleZ);

            transform.localPosition += GetRandomizedPositionOffset(random);
        }

        public Vector3 GetRandomizedPositionOffset(Random random) =>
            RandomVector(random, positionOffsetX, positionOffsetY, positionOffsetZ);

        private Vector3 RandomVector(Random random, in Vector2 rangeX, in Vector2 rangeY, in Vector2 rangeZ)
        {
            float randX = RandomRange(random, in rangeX);
            float randY = RandomRange(random, in rangeY);
            float randZ = RandomRange(random, in rangeZ);
            return new Vector3(randX, randY, randZ);
        }

        private float RandomRange(Random rand, in Vector2 range) =>
            (float)((rand.NextDouble() * (range.y - range.x)) + range.x);
    }
}
