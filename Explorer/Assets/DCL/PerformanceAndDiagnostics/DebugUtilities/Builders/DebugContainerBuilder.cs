using DCL.DebugUtilities.Views;
using DCL.Utility.Types;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities
{
    public class DebugContainerBuilder : IDebugContainerBuilder, IComparer<string>
    {
        private readonly SortedDictionary<string, DebugWidgetBuilder> widgetBuilders;
        private readonly Dictionary<string, DebugWidget> widgets = new (100);

        private readonly Func<DebugWidget> widgetFactoryMethod;
        private readonly Func<DebugControl> controlFactoryMethod;
        private readonly Dictionary<Type, IDebugElementFactory> factories;
        private readonly ISet<string>? allowOnlyNames;

        private bool isBuilt;
        private DebugContainer? container;

        public DebugContainer Container => container ?? throw new InvalidOperationException("Container has not been built yet");

        public IReadOnlyDictionary<string, DebugWidget> Widgets => widgets;

        public bool IsVisible
        {
            get => Container.visible;

            set
            {
                Container.visible = value;

                foreach (DebugWidget widget in widgets.Values)
                {
                    widget.visible = value;
                    SetChildrenVisibility(widget, value);
                }
            }
        }

        private static void SetChildrenVisibility(VisualElement element, bool visible)
        {
            foreach (var child in element.Children())
            {
                child.visible = visible;
                SetChildrenVisibility(child, visible);
            }
        }

        public DebugContainerBuilder(
            Func<DebugWidget> widgetFactoryMethod,
            Func<DebugControl> controlFactoryMethod,
            Dictionary<Type, IDebugElementFactory> factories,
            ISet<string>? allowOnlyNames
        )
        {
            this.widgetFactoryMethod = widgetFactoryMethod;
            this.controlFactoryMethod = controlFactoryMethod;
            this.factories = factories;
            this.allowOnlyNames = allowOnlyNames;
            widgetBuilders = new SortedDictionary<string, DebugWidgetBuilder>(this);
        }

        public Result<DebugWidgetBuilder> GetOrAddWidget(WidgetName widgetName)
        {
            string name = widgetName.Name;

            if (allowOnlyNames != null && allowOnlyNames.Contains(name) == false)
                return Result<DebugWidgetBuilder>.ErrorResult($"Name {name} not allowed");

            if (isBuilt)
                throw new InvalidOperationException("Container has already been built");

            name = name.ToUpper();

            if (widgetBuilders.TryGetValue(name, out DebugWidgetBuilder? widget))
                return Result<DebugWidgetBuilder>.SuccessResult(widget);

            var w = new DebugWidgetBuilder(name);
            widgetBuilders.Add(name, w);
            return Result<DebugWidgetBuilder>.SuccessResult(w);
        }

        public void BuildWithFlex(UIDocument debugRootCanvas)
        {
            if (isBuilt)
                throw new InvalidOperationException("Container has already been built");

            isBuilt = true;

            container = debugRootCanvas.rootVisualElement.Q<DebugContainer>();
            container.style.display = DisplayStyle.Flex;
            Container.Initialize();

            // Instantiate widgets
            foreach ((string name, DebugWidgetBuilder widgetBuilder) in widgetBuilders)
            {
                DebugWidget widget = widgetBuilder.Build(widgetFactoryMethod, controlFactoryMethod, factories);
                widget.name = name;
                widget.visible = false;

                widgets.Add(widget.name, widget);
                Container.containerRoot.Add(widget);
            }

            Container.visible = false;
        }

        public int Compare(string x, string y) =>
            string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }
}
