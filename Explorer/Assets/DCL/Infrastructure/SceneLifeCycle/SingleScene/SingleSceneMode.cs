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

        /// <summary>
        /// Should only be called once during the session. Single scene mode cannot change dynamically
        /// </summary>
        /// <param name="enabled">Either load single scene or load the world normally</param>
        public void Init(bool enabled) =>
            IsActive = enabled;

        public void SetAnchor(Vector2Int parcel) =>
            anchor = parcel;

        public void ClearAnchor() =>
            anchor = null;

        public bool IsAnchorScene(in SceneDefinitionComponent definition) =>
            HasAnchor && definition.Contains(AnchorParcel.x, AnchorParcel.y);
    }
}
