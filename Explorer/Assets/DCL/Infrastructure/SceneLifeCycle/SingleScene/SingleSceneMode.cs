using ECS.SceneLifeCycle.SceneDefinition;
using UnityEngine;

namespace ECS.SceneLifeCycle.SingleScene
{
    public class SingleSceneMode
    {
        private Vector2Int? anchor;

        public bool IsActive { get; private set; }
        public bool HasAnchor => anchor.HasValue;
        public Vector2Int AnchorParcel => anchor.GetValueOrDefault();
        public bool IsRestricting => IsActive && HasAnchor;

        public void SetActive(bool enabled) =>
            IsActive = enabled;

        public void SetAnchor(Vector2Int parcel) =>
            anchor = parcel;

        public void ClearAnchor() =>
            anchor = null;

        public bool IsAnchorScene(in SceneDefinitionComponent definition) =>
            HasAnchor && definition.Contains(AnchorParcel.x, AnchorParcel.y);
    }
}
