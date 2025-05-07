namespace MVC.PopupsController.PopupCloser
{
    public interface IPopupCloserView : IView
    {
        public ButtonWithRightClickHandler CloseButton { get; }
    }
}
