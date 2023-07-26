using System;

namespace ECS.Profiling
{
    public interface IProfilerView
    {
        void SetFPS(float averageFrameTimeInSeconds);

        void SetHiccups(int hiccupCount);

        event Action OnOpen;
        event Action OnClose;
    }
}
