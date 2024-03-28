using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities.UIBindings
{
    public class Vector2IntPlayerPrefsElementBinding : IElementBinding<Vector2Int>
    {
        private readonly ElementBinding<Vector2Int> elementBinding;
        private readonly string key;
        private Vector2Int value;

        public Vector2Int Value => elementBinding.Value;

        public Vector2IntPlayerPrefsElementBinding(string key)
        {
            this.key = key;

            elementBinding = new ElementBinding<Vector2Int>(
                SavedValue(),
                changeEvent => Save(changeEvent.newValue)
            );
        }

        private Vector2Int SavedValue() =>
            new (
                PlayerPrefs.GetInt($"{key}_x", 0),
                PlayerPrefs.GetInt($"{key}_y", 0)
            );

        private void Save(Vector2Int value)
        {
            PlayerPrefs.SetInt($"{key}_x", value.x);
            PlayerPrefs.SetInt($"{key}_y", value.y);
        }

        public void Connect(INotifyValueChanged<Vector2Int> element)
        {
            elementBinding.Connect(element);
        }

        public void PreUpdate()
        {
            elementBinding.PreUpdate();
        }

        public void Update()
        {
            elementBinding.Update();
        }

        public void Release()
        {
            elementBinding.Release();
        }
    }
}
