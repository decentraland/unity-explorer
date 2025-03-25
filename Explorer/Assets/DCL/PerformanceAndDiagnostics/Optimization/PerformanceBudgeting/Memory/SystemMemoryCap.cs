using System;
using UnityEngine.Scripting;
using SystemInfo = UnityEngine.Device.SystemInfo;

namespace DCL.Optimization.PerformanceBudgeting
{
    public enum MemoryCapMode
    {
        MAX_SYSTEM_MEMORY,
        FROM_SETTINGS,
        SIMULATED_MEMORY,
    }

    public class SystemMemoryCap : ISystemMemoryCap
    {
        private const int DEFAULT_CAP = 16;

        public MemoryCapMode Mode { private get; set; }
        private long memoryCapInMB;

        public SystemMemoryCap(MemoryCapMode initialMode)
        {
            Mode = initialMode;
        }

        public SystemMemoryCap(MemoryCapMode initialMode, int valueInMB)
        {
            Mode = initialMode;
            memoryCapInMB = valueInMB;
        }


        public long MemoryCapInMB
        {
            get
            {
                if (memoryCapInMB == 0)
                    MemoryCap = DEFAULT_CAP;

                return Mode == MemoryCapMode.MAX_SYSTEM_MEMORY ? SystemInfo.systemMemorySize : memoryCapInMB;
            }
        }

        public int MemoryCap
        {
            set
            {
                if (Mode == MemoryCapMode.SIMULATED_MEMORY)
                    return;

                memoryCapInMB = Math.Min(value * 1024L, SystemInfo.systemMemorySize);
            }
        }
    }
}
