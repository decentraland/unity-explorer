using UnityEngine;

namespace DCL.SDKComponents.NFTShape.Frames
{
    public abstract class AbstractFrame : MonoBehaviour
    {
        public enum Status
        {
            Loading,
            Failed,
        }

        public abstract void Paint(Color color);

        public abstract void Place(Texture2D picture);

        public abstract void UpdateStatus(Status status);

    }
}
