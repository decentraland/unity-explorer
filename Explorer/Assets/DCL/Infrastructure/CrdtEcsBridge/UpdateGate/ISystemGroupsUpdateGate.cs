using Arch.SystemGroups.Throttling;
using System;

namespace CrdtEcsBridge.UpdateGate
{
    /// <summary>
    ///     Enables throttling systems update when changes from the JS scenes are applied
    /// </summary>
    public interface ISystemGroupsUpdateGate : IUpdateBasedSystemGroupThrottler, IFixedUpdateBasedSystemGroupThrottler, IDisposable
    {
        /// <summary>
        ///     Open the gate to allow throttling systems update once
        /// </summary>
        void Open();
    }
}
