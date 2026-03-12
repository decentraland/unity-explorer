using System;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     A named face pose combining specific atlas slice indices for eyebrows, eyes, and mouth.
    ///     Eyebrows atlas layout: 0 Idle, 1 Up, 2 Down, 3 Angry, 4 Sad, 5 Surprised, 6-15 Unused.
    ///     Eyes atlas layout:     0 Idle, 1 HalfClosed, 2 Closed, 3 WideOpen, 4-7 Look directions, 8-15 Unused.
    ///     Mouth atlas layout:    0-11 Phonemes, 12 Sad, 13 Happy, 14 Smile, 15 Worried.
    /// </summary>
    [Serializable]
    public struct AvatarFaceExpressionDefinition
    {
        public string Name;

        [Range(0, 15)]
        public int EyebrowsIndex;

        [Range(0, 15)]
        public int EyesIndex;

        [Range(0, 15)]
        public int MouthIndex;
    }
}
