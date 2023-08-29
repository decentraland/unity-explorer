using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ECS.Editor.GUI.Views
{
    public class EcsView : MonoBehaviour
    {
        [SerializeField]
        VisualTreeAsset mVisualTreeAsset = default;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Add(mVisualTreeAsset.Instantiate());
        }
    }
}
