using DCL.DebugUtilities;
using System;

namespace DCL.Quality.Runtime
{
    public interface IQualityLevelController : IDisposable
    {
        void SetLevel(int index);

        void AddDebugViews(DebugWidgetBuilder debugWidgetBuilder);
    }
}
