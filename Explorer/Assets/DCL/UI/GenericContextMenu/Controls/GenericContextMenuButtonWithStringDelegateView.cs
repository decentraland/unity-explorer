namespace DCL.UI.Controls
{
    public class GenericContextMenuButtonWithStringDelegateView : GenericContextMenuButtonWithDelegateView<string>
    {
        public override void SetAsInteractable(bool isInteractable) =>
            ButtonComponent.interactable = isInteractable;
    }
}
