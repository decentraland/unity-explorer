using UnityEngine;

namespace DCL.Character.Components
{
    public struct CharacterLastPosition
    {
        public Vector3 LastPosition;

        public CharacterLastPosition(Vector3 lastPosition)
        {
            LastPosition = lastPosition;
        }
    }
}
