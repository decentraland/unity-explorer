using DCL.PerformanceBudgeting;
using System.Collections.Generic;

namespace Global
{
    public static class BudgetingConfig
    {
        // FPS
        public const int FPS_CAP = 40; // [ms]

        // Concurrent assets loading
        public const int SCENES_LOADING_BUDGET = 100;
        public const int ASSETS_LOADING_BUDGET = 50;

        // Memory
        public static readonly Dictionary<MemoryUsageStatus, float> MEM_THRESHOLD = new ()
        {
            { MemoryUsageStatus.Warning, 0.5f },
            { MemoryUsageStatus.Full, 0.95f },
        };
    }
}
