using DCL.Diagnostics.ReportsHandling;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Debounce messages based on the <see cref="ExceptionFingerprint" /> or the default message comparison, and a time-like characteristic.
    /// </summary>
    public abstract class TimingBasedDebouncer<TTiming> : IReportsDebouncer, IEqualityComparer<ExceptionFingerprint> where TTiming: struct
    {
        protected readonly Dictionary<ExceptionFingerprint, TTiming> exceptions = new (50);
        protected readonly Dictionary<string, TTiming> messages = new (50);

        public abstract ReportHandler AppliedTo { get; }

        public IReadOnlyDictionary<ExceptionFingerprint, TTiming> Exceptions => exceptions;

        public bool Debounce(object message, ReportData reportData, LogType log)
        {
            if (message is not string key)
                return false; // No assumptions can be made about the generic object type

            return Debounce(messages, key);
        }

        public bool Debounce(Exception exception, ReportData reportData, LogType log) =>
            Debounce(exceptions, new ExceptionFingerprint(exception));

        protected abstract bool Debounce<TKey>(Dictionary<TKey, TTiming> dictionary, TKey key);

        public bool Equals(ExceptionFingerprint x, ExceptionFingerprint y) =>
            x.Equals(y);

        public int GetHashCode(ExceptionFingerprint obj) =>
            obj.GetHashCode();
    }
}
