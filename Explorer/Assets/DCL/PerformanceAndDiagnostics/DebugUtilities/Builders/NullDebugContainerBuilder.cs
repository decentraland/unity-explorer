using DCL.DebugUtilities.Views;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities
{
    public class NullDebugContainerBuilder : IDebugContainerBuilder
    {
        public bool IsVisible { get; set; }

        public DebugContainer Container => throw new InvalidOperationException("Container is null implementation");

        public DebugWidgetBuilder AddWidget(string name) =>

            //ignore
            new (name);

        public IReadOnlyDictionary<string, DebugWidget> Widgets { get; } = new Dictionary<string, DebugWidget>();

        public void BuildWithFlex(UIDocument debugRootCanvas)
        {
            //ignore
        }
    }
}
