using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities
{
    [CreateAssetMenu(fileName = "DebugViewsCatalogSO", menuName = "DebugViewsCatalogSO")]
    public class DebugViewsCatalogSO: ScriptableObject
    {
        [field: SerializeField]
        public UIDocument RootDocumentPrefab { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset Widget { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset ControlContainer { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset Button { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset Toggle { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset ConstLabel { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset SetOnlyLabel { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset Hint { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset TextField { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset LongMarker { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset IntField { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset IntSlider { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset FloatField { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset FloatSlider { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset Vector2IntField { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset Vector3Field { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset DropdownField { get; private set; }

        [field: SerializeField]
        public VisualTreeAsset AverageFpsBanner { get; private set; }
    }
}
