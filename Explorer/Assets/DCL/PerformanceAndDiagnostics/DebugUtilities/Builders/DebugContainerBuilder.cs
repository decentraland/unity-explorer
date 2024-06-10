using DCL.DebugUtilities.Views;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities
{
    public class DebugContainerBuilder : IDebugContainerBuilder, IComparer<DebugWidgetBuilder>
    {
        private readonly List<DebugWidgetBuilder> widgetBuilders = new (100);
        private readonly HashSet<string> names = new (100);
        private readonly Dictionary<string, DebugWidget> widgets = new (100);

        private readonly Func<DebugWidget> widgetFactoryMethod;
        private readonly Func<DebugControl> controlFactoryMethod;
        private readonly Dictionary<Type, IDebugElementFactory> factories;

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
                    widget.visible = value;
            }
        }

        public DebugContainerBuilder(
            Func<DebugWidget> widgetFactoryMethod,
            Func<DebugControl> controlFactoryMethod,
            Dictionary<Type, IDebugElementFactory> factories
        )
        {
            this.widgetFactoryMethod = widgetFactoryMethod;
            this.controlFactoryMethod = controlFactoryMethod;
            this.factories = factories;
        }

        public DebugWidgetBuilder AddWidget(string name)
        {
            if (isBuilt)
                throw new InvalidOperationException("Container has already been built");

            name = name.ToUpper();

            if (names.Contains(name))
                throw new InvalidOperationException($"Name is already added: {name}");

            var w = new DebugWidgetBuilder(name);
            names.Add(name);
            widgetBuilders.Add(w);
            return w;
        }

        public void Build(UIDocument debugRootCanvas)
        {
            if (isBuilt)
                throw new InvalidOperationException("Container has already been built");

            isBuilt = true;

            // Sort by name
            widgetBuilders.Sort(this);

            container = debugRootCanvas.rootVisualElement!.Q<DebugContainer>();
            Container.Initialize();

            debugRootCanvas.rootVisualElement!.Add(Container);

            // Instantiate widgets
            foreach (DebugWidgetBuilder widgetBuilder in widgetBuilders)
            {
                DebugWidget widget = widgetBuilder.Build(widgetFactoryMethod, controlFactoryMethod, factories);
                widget.name = widgetBuilder.name;
                widget.visible = false;

                widgets.Add(widget.name, widget);
                Container.containerRoot.Add(widget);
            }

            Container.visible = false;
        }

        public int Compare(DebugWidgetBuilder x, DebugWidgetBuilder y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (ReferenceEquals(null, y)) return 1;
            if (ReferenceEquals(null, x)) return -1;
            return string.Compare(x.name, y.name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
