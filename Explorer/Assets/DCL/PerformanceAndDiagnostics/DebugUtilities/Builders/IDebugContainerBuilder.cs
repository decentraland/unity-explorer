using DCL.DebugUtilities.Views;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Builder used by Plugins to schedule the creation of individual debug widgets
    /// </summary>
    public interface IDebugContainerBuilder
    {
        bool IsVisible { get; set; }

        DebugContainer Container { get; }

        DebugWidgetBuilder AddWidget(string name);

        IReadOnlyDictionary<string, DebugWidget> Widgets { get; }

        void Build(UIDocument debugRootCanvas);
    }

    public static class DebugContainerBuilderExtensions
    {
        public static void BuildWithFlex(this IDebugContainerBuilder debugContainerBuilder, UIDocument debugRootCanvas)
        {
            debugRootCanvas.rootVisualElement!.style!.display = DisplayStyle.Flex;
            debugContainerBuilder.Build(debugRootCanvas);
        }
    }
}
