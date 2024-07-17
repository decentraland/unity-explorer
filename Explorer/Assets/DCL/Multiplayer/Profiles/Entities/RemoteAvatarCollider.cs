using UnityEngine;

namespace DCL.Multiplayer.Profiles.Entities
{
    public class RemoteAvatarCollider : MonoBehaviour
    {
        [field: SerializeField]
        public Collider Collider { get; private set; }
    }
}
