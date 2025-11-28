namespace DCL.UI.Controls
{
    public class GenericContextMenuButtonWithStringDelegateView : GenericContextMenuButtonWithDelegateView<string>
    {
        public override bool IsInteractable
        {
            get => ButtonComponent.interactable;
            set => ButtonComponent.interactable = value;
        }
    }
}
