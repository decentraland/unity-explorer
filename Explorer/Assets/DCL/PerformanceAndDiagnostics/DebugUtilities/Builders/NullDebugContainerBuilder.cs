using DCL.DebugUtilities.Views;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Utility.Types;

namespace DCL.DebugUtilities
{
    public class NullDebugContainerBuilder : IDebugContainerBuilder
    {
        public bool IsVisible { get; set; }

        public DebugContainer Container => throw new InvalidOperationException("Container is null implementation");

        public Result<DebugWidgetBuilder> AddWidget(string name) =>
            //ignore
            Result<DebugWidgetBuilder>.ErrorResult("Null implementation");

        public IReadOnlyDictionary<string, DebugWidget> Widgets { get; } = new Dictionary<string, DebugWidget>();

        public void BuildWithFlex(UIDocument debugRootCanvas)
        {
            //ignore
        }
    }
}
