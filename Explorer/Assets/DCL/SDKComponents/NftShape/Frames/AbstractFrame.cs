using UnityEngine;

namespace DCL.SDKComponents.NftShape.Frames
{
    public abstract class AbstractFrame : MonoBehaviour
    {
        public enum Status
        {
            Loading,
            Failed,
        }

        public abstract void Paint(Color color);

        public abstract void Place(Material picture);

        public abstract void UpdateStatus(Status status);
    }
}
