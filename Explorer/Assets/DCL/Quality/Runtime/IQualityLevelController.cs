using DCL.DebugUtilities;
using System;
using System.Collections.Generic;

namespace DCL.Quality.Runtime
{
    public interface IQualityLevelController : IDisposable
    {
        void SetLevel(int index);

        void AddDebugViews(DebugWidgetBuilder debugWidgetBuilder, List<Action> onUpdate);
    }
}
