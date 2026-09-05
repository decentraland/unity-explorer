using DCL.Character;
using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.Multiplayer.Connections.Demo
{
    [Serializable]
    public class LoonCharacterObject : ICharacterObject
    {
        [SerializeField] private bool randomMoves = true;
        [SerializeField] private int magnitude = 200;

        public CharacterController Controller { get; }
        public Transform CameraFocus { get; }
        public Transform Transform { get; }
        public Vector3 Position => randomMoves ? new Vector3(RandomValue(), RandomValue(), RandomValue()) : Vector3.zero;

        private int RandomValue() =>
            Random.Range(-magnitude, magnitude);
    }
}
