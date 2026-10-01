using ECS.SceneLifeCycle.SceneDefinition;
using UnityEngine;

namespace ECS.SceneLifeCycle.SingleScene
{
    public class SingleSceneMode
    {
        public bool IsActive { get; private set; }
        public bool HasAnchor { get; private set; }
        public Vector2Int AnchorParcel { get; private set; }
        public bool IsRestricting => IsActive && HasAnchor;

        public void SetActive(bool isActive) =>
            IsActive = isActive;

        public void SetAnchor(Vector2Int parcel)
        {
            AnchorParcel = parcel;
            HasAnchor = true;
        }

        public void ClearAnchor()
        {
            AnchorParcel = Vector2Int.zero;
            HasAnchor = false;
        }

        public bool IsAnchorScene(in SceneDefinitionComponent definition) =>
            HasAnchor && definition.Contains(AnchorParcel.x, AnchorParcel.y);
    }
}
