using GPUInstancerPro.PrefabModule;
using UnityEngine;

public class AddInstancesToGPUIManager : MonoBehaviour
{
    public void AddInstancesToGPUIManages(GameObject iGameObject)
    {
        var prefabManager = FindFirstObjectByType<GPUIPrefabManager>();

        foreach (var componentsInChild in iGameObject.GetComponentsInChildren<GPUIPrefab>(true))
        {
            prefabManager.AddPrefabInstance(componentsInChild);
        }
    }
}