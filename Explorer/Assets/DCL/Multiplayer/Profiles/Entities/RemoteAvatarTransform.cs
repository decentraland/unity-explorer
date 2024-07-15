using UnityEngine;

namespace DCL.Multiplayer.Profiles.Entities
{
    public class RemoteAvatarTransform : MonoBehaviour
    {
        [field: SerializeField]
        public Collider Collider { get; private set; }
    }
}
