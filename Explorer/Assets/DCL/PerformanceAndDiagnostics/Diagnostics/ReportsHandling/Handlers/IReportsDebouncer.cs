using DCL.Optimization.Pools;
using System;
using UnityEngine;

namespace DCL.Diagnostics
{
    public interface IReportsDebouncer
    {
        /// <summary>
        ///     Handlers the debouncer is applied to
        /// </summary>
        ReportHandler AppliedTo { get; }

        /// <summary>
        ///     Determine if the message should be skipped
        /// </summary>
        bool Debounce(object message, ReportData reportData, LogType log);

        bool Debounce(Exception exception, ReportData reportData, LogType log);
    }
}
