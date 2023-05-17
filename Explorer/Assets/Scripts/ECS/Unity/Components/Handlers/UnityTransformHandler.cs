using UnityEngine;

namespace ECS.Unity.Components
{
    public class UnityTransformHandler : IUnityComponentHandler<Transform>
    {
        internal readonly string defaultName = "POOL_OBJECT_TRANSFORM";

        public UnityTransformHandler()
        {
            parentContainer = new GameObject("POOL_CONTAINER_TRANSFORM").transform;
        }

        public Transform HandleCreation()
        {
            var go = new GameObject(defaultName);
            go.gameObject.SetActive(false);
            go.transform.SetParent(parentContainer.transform);
            return go.transform;
        }

        public void HandleGet(Transform component)
        {
            component.gameObject.SetActive(true);
        }

        public void HandleRelease(Transform component)
        {
            component.gameObject.SetActive(false);
            component.gameObject.name = defaultName;
            component.transform.SetParent(parentContainer.transform);
        }

        public Transform parentContainer { get; }
    }
}
