using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public class FrameTimeCounter  : IFrameTimeCounter
{
    ProfilerRecorder mainThreadTimeRecorder;

    public FrameTimeCounter()
    {
        mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
    }

    public long GetFrameTime() =>
        mainThreadTimeRecorder.CurrentValue;

}

public interface IFrameTimeCounter
{
    public long GetFrameTime();
}
