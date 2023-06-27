using Arch.System;
using System;

namespace Diagnostics.ReportsHandling
{
    /// <summary>
    ///     Exception happened in the ECS system
    /// </summary>
    public class EcsSystemException : Exception
    {
        /// <summary>
        ///     Can be used for stability analytics
        /// </summary>
        public readonly ISystem<float> FaultySystem;

        public readonly ReportData ReportData;

        public EcsSystemException(ISystem<float> faultySystem, Exception innerException, ReportData reportData) : base($"[{reportData.Category}]", innerException)
        {
            ReportData = reportData;
            FaultySystem = faultySystem;
        }
    }
}
