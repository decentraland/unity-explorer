using System;
using SystemInfo = UnityEngine.Device.SystemInfo;

namespace DCL.Optimization.PerformanceBudgeting
{
    public enum MemoryCapMode
    {
        REAL_MEMORY,
        SIMULATED_MEMORY,
    }

    public class SystemMemoryCap : ISystemMemoryCap
    {
        private readonly MemoryCapMode mode;

        public SystemMemoryCap()
        {
            mode = MemoryCapMode.REAL_MEMORY;
            //Default value will be later set in `MemoryLimitSettingController`. We start with the max value
            MemoryCap = -1;
        }

        public SystemMemoryCap(int simulatedMemory)
        {
            mode = MemoryCapMode.SIMULATED_MEMORY;
            MemoryCapInMB = simulatedMemory;
        }

        public long MemoryCapInMB { get; private set; }

        public int MemoryCap
        {
            set
            {
                //Memory cannot be changed if we are simulating it
                if (mode == MemoryCapMode.SIMULATED_MEMORY)
                    return;

                //-1 means set to max
                if (value == -1)
                    MemoryCapInMB = SystemInfo.systemMemorySize;
                else
                    MemoryCapInMB = Math.Min(value * 1024L, SystemInfo.systemMemorySize);
            }
        }
    }
}
