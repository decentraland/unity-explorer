using Arch.System;
using System;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Exception happened in the ECS system
    /// </summary>
    public class EcsSystemException : Exception, IDecentralandException
    {
        /// <summary>
        ///     Can be used for stability analytics
        /// </summary>
        public readonly ISystem<float> FaultySystem;

        /// <summary>
        ///     Indicates that the exception was intercepted by a higher level abstraction and not handled by the user code itself
        /// </summary>
        public readonly bool Unhandled;

        internal ReportData reportData;

        private string messagePrefix;

        public override string Message => messagePrefix + base.Message;

        public ref readonly ReportData ReportData => ref reportData;

        string IDecentralandException.MessagePrefix
        {
            set => messagePrefix = value;
        }

        public EcsSystemException(ISystem<float> faultySystem, Exception innerException, ReportData reportData, bool unhandled = true)
            : base(faultySystem == null ? string.Empty : $"[{faultySystem.GetType().Name}]", innerException)
        {
            this.reportData = reportData;
            FaultySystem = faultySystem;
            Unhandled = unhandled;
        }
    }
}
