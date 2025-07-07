using System;
using UnityEngine;

namespace ECS.StreamableLoading.Common
{
    public class StreamableLoadingException : Exception
    {
        /// <summary>
        ///     Severity the exception should be logged with.
        ///     Allows to demote predictable exceptions to a lower log level.
        /// </summary>
        public readonly LogType Severity;

        public StreamableLoadingException(LogType severity, string message, Exception innerException) : base(message, innerException)
        {
            Severity = severity;
        }

        public StreamableLoadingException(LogType severity, string message) : base(message)
        {
            Severity = severity;
        }
    }
}
