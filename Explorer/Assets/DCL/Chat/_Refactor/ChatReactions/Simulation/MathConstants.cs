using System;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Simulation
{
    internal static class MathConstants
    {
        public const float TWO_PI = Mathf.PI * 2f;

        /// <summary>
        /// Fast approximate 1/√x (Quake III algorithm, one Newton-Raphson iteration).
        /// Max relative error ≈ 0.2 % — more than adequate for visual particle math.
        /// </summary>
        public static float FastRsqrt(float x)
        {
            float xhalf = 0.5f * x;
            int i = BitConverter.SingleToInt32Bits(x);
            i = 0x5f3759df - (i >> 1);
            float y = BitConverter.Int32BitsToSingle(i);
            y *= 1.5f - xhalf * y * y;
            return y;
        }
    }
}
