using Arch.System;
using System;

namespace Diagnostics.ReportsHandling
{
    /// <summary>
    ///     Exception happened in the ECS system
    /// </summary>
    public class EcsSystemException : Exception, IManagedEcsException
    {
        /// <summary>
        ///     Can be used for stability analytics
        /// </summary>
        public readonly ISystem<float> FaultySystem;

        /// <summary>
        ///     Indicates that the exception was intercepted by a higher level abstraction and not handled by the user code itself
        /// </summary>
        public readonly bool Unhandled;

        private string messagePrefix;

        internal ReportData reportData;

        public EcsSystemException(ISystem<float> faultySystem, Exception innerException, ReportData reportData, bool unhandled = true)
            : base(faultySystem == null ? string.Empty : $"[{faultySystem.GetType().Name}]", innerException)
        {
            this.reportData = reportData;
            FaultySystem = faultySystem;
            Unhandled = unhandled;
        }

        public override string Message => messagePrefix + base.Message;

        string IManagedEcsException.MessagePrefix
        {
            set => messagePrefix = value;
        }

        public ref readonly ReportData ReportData => ref reportData;
    }
}
