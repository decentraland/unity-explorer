using DCL.Landscape.Settings;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public class MessageEncodingSettings : ScriptableObject
    {
        public LandscapeData landscapeData;

        public const int TWO_BITS_MASK = 0x3;

        public const int PARCEL_BITS = 17;
        private const int MOVEMENT_KIND_BITS = 2;

        // int32 - 9 (Anim) - 2 (Tiers) = 21 bits
        [Header("TIMESTAMP [21]")]
        public float TIMESTAMP_QUANTUM = 0.02f;
        public int TIMESTAMP_BITS = 15;
        public int ROTATION_Y_BITS = 6;

        // int64 - 17 (Parcel) = 47 bits
        [Header("POSITION [47]")]
        public MovementEncodingConfig tier0;
        public MovementEncodingConfig tier1;
        public MovementEncodingConfig tier2;
        public MovementEncodingConfig tier3;

        public int MOVEMENT_KIND_START_BIT => TIMESTAMP_BITS;
        public int SLIDING_BIT => MOVEMENT_KIND_START_BIT + MOVEMENT_KIND_BITS;
        public int STUNNED_BIT => SLIDING_BIT + 1;
        public int GROUNDED_BIT => STUNNED_BIT + 1;
        public int JUMPING_BIT => GROUNDED_BIT + 1;
        public int LONG_JUMP_BIT => JUMPING_BIT + 1;
        public int FALLING_BIT => LONG_JUMP_BIT + 1;
        public int LONG_FALL_BIT => FALLING_BIT + 1;

        public int ROTATION_START_BIT => LONG_FALL_BIT + 1;
        public int TIER_START_BIT => ROTATION_START_BIT + ROTATION_Y_BITS;

        public MovementEncodingConfig GetConfigForTier(int tier)
        {
            return tier switch
                   {
                       0 => tier0,
                       1 => tier1,
                       2 => tier2,
                       3 => tier3,
                       _ => throw new ArgumentOutOfRangeException(),
                   };
        }
    }

    [Serializable]
    public class MovementEncodingConfig
    {
        [SerializeField]
        private byte XZ_BITS = 9;

        [Space]
        [SerializeField] private int Y_MAX = 500;
        [SerializeField] private byte Y_BITS = 13;

        [Space]
        [SerializeField] private int MAX_VELOCITY = 40;
        [SerializeField] private byte VELOCITY_BITS = 1;

        public byte XZ__BITS => XZ_BITS;

        public int Y__MAX => Y_MAX;

        public byte Y__BITS => Y_BITS;

        public int MAX__VELOCITY => MAX_VELOCITY;

        public byte VELOCITY_AXIS_FIELD_SIZE_IN_BITS => VELOCITY_BITS;
    }
}
