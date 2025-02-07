using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.SuggestionPanel
{
    public abstract class BaseInputSuggestionElement : MonoBehaviour
    {
        public delegate void SuggestionSelectedDelegate(string suggestionId);

        public event SuggestionSelectedDelegate SuggestionSelectedEvent;

        [field: SerializeField] public int MaxSuggestionAmount { get; private set; }

        public string SuggestionId { get; protected set; }

        public virtual void Setup(IInputSuggestionElementData data) { }

        public virtual void OnCreated() { }

        public virtual void OnGet() { }

        public virtual void OnReleased() { }

        public virtual void SetSelectionState(bool isSelected) { }

        protected void OnSuggestionSelected()
        {
            SuggestionSelectedEvent?.Invoke(SuggestionId);
        }

        public virtual InputSuggestionType GetSuggestionType() =>
            InputSuggestionType.NONE;

        public virtual IInputSuggestionElementData GetElementData() =>
            null;
    }

    public abstract class BaseInputSuggestionElement<T> : BaseInputSuggestionElement
        where T: IInputSuggestionElementData
    {
        [field: SerializeField] private Button selectionButton;
        [field: SerializeField] private GameObject selectedBackground;

        private T elementData;

        public override void Setup(IInputSuggestionElementData data)
        {
            if (data is T typedData)
            {
                elementData = typedData;
                SuggestionId = elementData.GetId();
                SetupContinuation(typedData);
            }
        }

        protected virtual void SetupContinuation(T suggestionElementData) { }

        public override IInputSuggestionElementData GetElementData() =>
            elementData;

        public override InputSuggestionType GetSuggestionType() =>
            elementData.GetInputSuggestionType();

        private void Awake()
        {
            selectionButton.onClick.AddListener(OnSuggestionSelected);
        }

        public override void OnGet()
        {
            base.OnGet();
            gameObject.SetActive(true);
        }

        public override void OnReleased()
        {
            base.OnReleased();
            selectedBackground.SetActive(false);
            gameObject.SetActive(false);
        }

        public override void SetSelectionState(bool isSelected)
        {
            base.SetSelectionState(isSelected);
            selectedBackground.SetActive(isSelected);
        }
    }

    public enum InputSuggestionType
    {
        NONE,
        EMOJIS,
        PROFILE,
    }
}
