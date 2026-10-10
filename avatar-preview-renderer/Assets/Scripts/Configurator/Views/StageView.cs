using UnityEngine.UIElements;

namespace Configurator.Views
{
    public abstract class StageView: IRefreshableView
    {
        private readonly VisualElement _root;

        public readonly string Title;
        public readonly string PrimaryButtonText;
        public readonly string PrimaryButtonTextMobile;
        public readonly string SecondaryButtonText;
        public readonly string SecondaryButtonTextMobile;
        public readonly int PrimaryButtonWidth; // This is ugly but the only way to get it to animate nicely

        public abstract string SelectedCategory { get; }

        protected bool UsingMobile;
        
        protected StageView(VisualElement root, string title, string primaryButtonText, int primaryButtonWidth, string primaryButtonTextMobile, string secondaryButtonText, string secondaryButtonTextMobile)
        {
            _root = root;
            Title = title;
            PrimaryButtonText = primaryButtonText;
            PrimaryButtonTextMobile = primaryButtonTextMobile;
            PrimaryButtonWidth = primaryButtonWidth;
            SecondaryButtonText = secondaryButtonText;
            SecondaryButtonTextMobile = secondaryButtonTextMobile;
        }

        public void HideLeft()
        {
            _root.AddToClassList("customization-window__content-item--hide-left");
        }

        public void HideRight()
        {
            _root.AddToClassList("customization-window__content-item--hide-right");
        }

        public void Show()
        {
            _root.RemoveFromClassList("customization-window__content-item--hide-left");
            _root.RemoveFromClassList("customization-window__content-item--hide-right");
        }

        public virtual void SetUsingMobileMode(bool usingMobile)
        {
            UsingMobile = usingMobile;
        }

        public abstract object GetData();
        public abstract void SetData(object data);
    }
}