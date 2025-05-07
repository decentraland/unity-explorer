using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.SuggestionPanel
{
    [Serializable]
    public struct SuggestionElementData
    {
        [field: SerializeField] public int MaxSuggestionAmount { get; private set; }
        [field: SerializeField] public float SuggestionElementHeight { get; private set; }
    }

    public abstract class BaseInputSuggestionElement : MonoBehaviour
    {
        public delegate void SuggestionSelectedDelegate(string suggestionId);

        public event SuggestionSelectedDelegate SuggestionSelectedEvent;

        [field:SerializeField] public SuggestionElementData SuggestionElementData { get; private set; }

        /// <summary>
        /// The value returned in the SuggestionSelectedEvent, each suggestion type should assign this the correct value that is expected on the receiver end
        /// </summary>
        public string SuggestionId { get; protected set; }

        public abstract void Setup<T>(T data) where T : IInputSuggestionElementData;

        /// <summary>
        /// Method to call when the SuggestionElement is Created in a pool
        /// </summary>
        public virtual void OnCreated() { }

        /// <summary>
        /// Method to call when the SuggestionElement is Get from a pool
        /// </summary>
        public virtual void OnGet() { }

        /// <summary>
        /// Method to call when the SuggestionElement is Released from a pool
        /// </summary>
        public virtual void OnReleased() { }

        /// <summary>
        /// This changes the selection state of a suggestion, in case it needs to alter its visuals
        /// </summary>
        public virtual void SetSelectionState(bool isSelected) { }

        protected void OnSuggestionSelected()
        {
            SuggestionSelectedEvent?.Invoke(SuggestionId);
        }

        public virtual InputSuggestionType GetSuggestionType() =>
            InputSuggestionType.NONE;
    }

    public abstract class BaseInputSuggestionElement<T> : BaseInputSuggestionElement
        where T: IInputSuggestionElementData
    {
        [field: SerializeField] private Button selectionButton;
        [field: SerializeField] private GameObject selectedBackground;

        private T elementData;

        public override void Setup<TData>(TData data)
        {
            if (data is T typedData)
            {
                elementData = typedData;
                SetupContinuation(typedData);
            }
        }

        /// <summary>
        /// This method allows each suggestion to add to the setup process, usually by configuring its visual state using the typed data
        /// </summary>
        protected virtual void SetupContinuation(T suggestionElementData) { }

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
