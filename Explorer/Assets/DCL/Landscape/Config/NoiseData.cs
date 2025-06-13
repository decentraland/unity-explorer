using System;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace DCL.Landscape.Config
{
    [CreateAssetMenu(fileName = "NoiseData", menuName = "DCL/Landscape/Noise Data")]
    public class NoiseData : NoiseDataBase
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

        public void ApplyRandomness(Transform transform, ref Random random, float objHeight)
        {
            transform.eulerAngles = RandomVector(ref random, randomRotationX, randomRotationY, randomRotationZ);

            if (proportionalScale)
                transform.localScale = Vector3.one * RandomRange(ref random, in randomScale) * objHeight;
            else
                transform.localScale = RandomVector(ref random, randomScaleX, randomScaleY, randomScaleZ);

            transform.localPosition += GetRandomizedPositionOffset(ref random);
        }

        public Vector3 GetRandomizedPositionOffset(ref Random random) =>
            RandomVector(ref random, positionOffsetX, positionOffsetY, positionOffsetZ);

        private Vector3 RandomVector(ref Random random, in Vector2 rangeX, in Vector2 rangeY, in Vector2 rangeZ)
        {
            float randX = RandomRange(ref random, in rangeX);
            float randY = RandomRange(ref random, in rangeY);
            float randZ = RandomRange(ref random, in rangeZ);
            return new Vector3(randX, randY, randZ);
        }

        private float RandomRange(ref Random random, in Vector2 range) =>
            (random.NextFloat() * (range.y - range.x)) + range.x;
    }
}
