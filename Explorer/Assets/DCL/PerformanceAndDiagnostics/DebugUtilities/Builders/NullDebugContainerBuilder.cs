using DCL.DebugUtilities.Views;
using DCL.Utility.Types;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DCL.DebugUtilities
{
    public class NullDebugContainerBuilder : IDebugContainerBuilder
    {
        public bool IsVisible { get; set; }

        public DebugContainer Container => throw new InvalidOperationException("Container is null implementation");

        public Result<DebugWidgetBuilder> AddWidget(WidgetName name) =>
            //ignore
            Result<DebugWidgetBuilder>.ErrorResult("Null implementation");

        public IReadOnlyDictionary<string, DebugWidget> Widgets { get; } = new Dictionary<string, DebugWidget>();

        public void BuildWithFlex(UIDocument debugRootCanvas)
        {
            //ignore
        }
    }
}
