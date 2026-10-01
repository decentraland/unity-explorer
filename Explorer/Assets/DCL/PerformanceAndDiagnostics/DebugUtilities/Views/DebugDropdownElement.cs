using UnityEngine.UIElements;

namespace DCL.DebugUtilities.Views
{
    [UxmlElement]
    public partial class DebugDropdownElement : DebugElementBase<DebugDropdownElement, DebugDropdownDef>
    {
        protected override void ConnectBindings()
        {
            DropdownField dropdown = this.Q<DropdownField>();
            dropdown.choices = definition.Choices;
            dropdown.label = definition.Label;

            // Hooks the gestures that open the popup, so it can be styled once it is in the panel.
            dropdown.RegisterCallback<PointerDownEvent>(_ => OnMenuOpened());
            dropdown.RegisterCallback<NavigationSubmitEvent>(_ => OnMenuOpened());

            definition.Binding.Connect(dropdown);
        }

        private void OnMenuOpened()
        {
            StyleOpenMenu();

            // The menu may be attached after the handlers finish, so try once more on the next frame.
            schedule.Execute(StyleOpenMenu);
        }

        /// <summary>
        ///     Attaches every stylesheet found on this element's ancestors to the open popup menu, so the debug
        ///     dropdown-menu rules apply inside it.
        /// </summary>
        private void StyleOpenMenu()
        {
            VisualElement? menuRoot = panel?.visualTree.Q(className: GenericDropdownMenu.ussClassName);

            if (menuRoot == null)
                return;

            for (VisualElement ancestor = this; ancestor != null; ancestor = ancestor.parent)
                for (var i = 0; i < ancestor.styleSheets.count; i++)
                    if (!menuRoot.styleSheets.Contains(ancestor.styleSheets[i]))
                        menuRoot.styleSheets.Add(ancestor.styleSheets[i]);
        }
    }
}
