using UnityEngine.Device;

namespace DCL.Optimization.PerformanceBudgeting
{
    public enum MemoryCapMode
    {
        MAX_SYSTEM_MEMORY,
        FROM_SETTINGS,
    }

    public class SystemMemoryCap : ISystemMemoryCap
    {
        private const int DEFAULT_CAP = 16;

        public MemoryCapMode Mode { private get; set; }

        public SystemMemoryCap(MemoryCapMode initialMode)
        {
            Mode = initialMode;
        }

        private long memoryCapInMB;

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
            set => memoryCapInMB = value * 1024L;
        }
    }
}
