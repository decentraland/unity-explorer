using DCL.Diagnostics;
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

        public override string Message => messagePrefix + base.Message;

        public ref readonly ReportData ReportData => ref reportData;

        string IManagedEcsException.MessagePrefix
        {
            set => messagePrefix = value;
        }

        internal SceneExecutionException(IEnumerable<Exception> innerExceptions, ReportData reportData) : base(innerExceptions)
        {
            this.reportData = reportData;
        }
    }
}
