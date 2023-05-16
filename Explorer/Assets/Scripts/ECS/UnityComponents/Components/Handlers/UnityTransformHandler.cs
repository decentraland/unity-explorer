using UnityEngine;

public class UnityTransformHandler : IUnityComponentHandler<Transform>
{
    private readonly GameObject parentContainer;
    private readonly string defaultName = "POOL_OBJECT_TRANSFORM";

    public UnityTransformHandler(Transform rootContainer)
    {
        parentContainer = new GameObject("POOL_CONTAINER_TRANSFORM");
        parentContainer.transform.SetParent(rootContainer);
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

    public UnityComponentPool<Transform> GetPool() =>
        new (HandleCreation, HandleGet, HandleRelease);
}
