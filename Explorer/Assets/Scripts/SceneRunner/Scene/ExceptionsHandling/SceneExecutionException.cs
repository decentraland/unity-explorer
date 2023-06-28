using Diagnostics.ReportsHandling;
using System;
using System.Collections.Generic;

namespace SceneRunner.Scene.ExceptionsHandling
{
    /// <summary>
    ///     Exception that suspended the scene
    /// </summary>
    public class SceneExecutionException : AggregateException, IManagedEcsException
    {
        private readonly ReportData reportData;
        private string messagePrefix;

        internal SceneExecutionException(Exception[] innerExceptions, ReportData reportData) : base(innerExceptions)
        {
            this.reportData = reportData;
        }

        internal SceneExecutionException(IEnumerable<Exception> innerExceptions, ReportData reportData) : base(innerExceptions)
        {
            this.reportData = reportData;
        }

        public override string Message => messagePrefix + base.Message;

        string IManagedEcsException.MessagePrefix
        {
            set => messagePrefix = value;
        }

        public ref readonly ReportData ReportData => ref reportData;
    }
}
