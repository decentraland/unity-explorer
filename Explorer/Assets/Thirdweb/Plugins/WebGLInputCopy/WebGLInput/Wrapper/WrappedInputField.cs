using UnityEngine;
using UnityEngine.UI;
using WebGLSupport.Detail;

namespace WebGLSupport
{
    /// <summary>
    ///     Wrapper for UnityEngine.UI.InputField
    /// </summary>
    internal class WrappedInputField : IInputField
    {
        private readonly InputField input;
        private readonly RebuildChecker checker;

        public bool ReadOnly => input.readOnly;

        public string text
        {
            get => input.text;
            set => input.text = value;
        }

        public string placeholder
        {
            get
            {
                if (!input.placeholder)
                    return "";

                Text text = input.placeholder.GetComponent<Text>();
                return text ? text.text : "";
            }
        }

        public int fontSize => input.textComponent.fontSize;

        public ContentType contentType => (ContentType)input.contentType;

        public LineType lineType => (LineType)input.lineType;

        public int characterLimit => input.characterLimit;

        public int caretPosition => input.caretPosition;

        public bool isFocused => input.isFocused;

        public int selectionFocusPosition
        {
            get => input.selectionFocusPosition;
            set => input.selectionFocusPosition = value;
        }

        public int selectionAnchorPosition
        {
            get => input.selectionAnchorPosition;
            set => input.selectionAnchorPosition = value;
        }

        public bool OnFocusSelectAll => true;

        public bool EnableMobileSupport =>

            // return false to use unity mobile keyboard support
            false;

        public WrappedInputField(InputField input)
        {
            this.input = input;
            checker = new RebuildChecker(this);
        }

        public void ActivateInputField()
        {
            input.ActivateInputField();
        }

        public void DeactivateInputField()
        {
            input.DeactivateInputField();
        }

        public void Rebuild()
        {
            if (checker.NeedRebuild())
            {
                input.textComponent.SetAllDirty();
                input.Rebuild(CanvasUpdate.LatePreRender);
            }
        }

        public Rect GetScreenCoordinates() =>
            Support.GetScreenCoordinates(input.GetComponent<RectTransform>());
    }
}
