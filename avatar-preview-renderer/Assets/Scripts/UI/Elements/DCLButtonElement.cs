using System;
using UI.Manipulators;
using UnityEngine.UIElements;

namespace UI.Elements
{
    [UxmlElement]
    public partial class DCLButtonElement : VisualElement
    {
        private const string USS_BLOCK = "dcl-button";
        private const string USS_PRIMARY = USS_BLOCK + "--primary";
        private const string USS_SECONDARY = USS_BLOCK + "--secondary";
        private const string USS_ICON_NONE = USS_BLOCK + "--icon-none";
        private const string USS_ICON_FORWARD = USS_BLOCK + "--icon-forward";
        private const string USS_ICON_BACK = USS_BLOCK + "--icon-back";
        private const string USS_ICON_CHECK = USS_BLOCK + "--icon-check";

        private const string USS_LABEL = USS_BLOCK + "__label";
        private const string USS_ICON = USS_BLOCK + "__icon";

        [UxmlAttribute]
        public string Text
        {
            get => _label.text;
            set => _label.text = value;
        }

        [UxmlAttribute]
        public Type ButtonType
        {
            get => _buttonType;
            set
            {
                _buttonType = value;
                RefreshType();
            }
        }

        [UxmlAttribute]
        public Icon ButtonIcon
        {
            get => _buttonIcon;
            set
            {
                _buttonIcon = value;
                RefreshIcon();
            }
        }

        public event Action Clicked;

        private Type _buttonType;
        private Icon _buttonIcon;

        private readonly Label _label;

        public DCLButtonElement()
        {
            AddToClassList(USS_BLOCK);
            AddToClassList("dcl-clickable");

            Add(_label = new Label { name = "label", pickingMode = PickingMode.Ignore });
            _label.AddToClassList(USS_LABEL);

            var icon = new VisualElement { name = "icon", pickingMode = PickingMode.Ignore };
            Add(icon);
            icon.AddToClassList(USS_ICON);

            this.AddManipulator(new AudioClickable(() => Clicked?.Invoke()));
        }

        private void RefreshIcon()
        {
            RemoveFromClassList(USS_ICON_NONE);
            RemoveFromClassList(USS_ICON_BACK);
            RemoveFromClassList(USS_ICON_FORWARD);
            RemoveFromClassList(USS_ICON_CHECK);

            switch (_buttonIcon)
            {
                case Icon.None:
                    AddToClassList(USS_ICON_NONE);
                    break;
                case Icon.Forward:
                    AddToClassList(USS_ICON_FORWARD);
                    break;
                case Icon.Back:
                    AddToClassList(USS_ICON_BACK);
                    break;
                case Icon.Check:
                    AddToClassList(USS_ICON_CHECK);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            MarkDirtyRepaint();
        }

        private void RefreshType()
        {
            RemoveFromClassList(USS_PRIMARY);
            RemoveFromClassList(USS_SECONDARY);

            switch (_buttonType)
            {
                case Type.Primary:
                    AddToClassList(USS_PRIMARY);
                    break;
                case Type.Secondary:
                    AddToClassList(USS_SECONDARY);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            MarkDirtyRepaint();
        }

        public enum Type
        {
            Primary,
            Secondary
        }

        public enum Icon
        {
            None,
            Forward,
            Back,
            Check
        }
    }
}