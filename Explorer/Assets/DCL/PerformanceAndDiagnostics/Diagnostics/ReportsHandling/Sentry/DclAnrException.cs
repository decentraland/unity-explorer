using System;
using System.Collections.Generic;
using RichTypes;

namespace DCL.Diagnostics.Sentry
{
    public class DclApplicationNotRespondingException : Exception
    {
        public readonly string LoadingStage;

#if UNITY_STANDALONE_WIN
        public readonly IReadOnlyList<Result<DumpEntry>> DumpFileEntries;

        internal DclApplicationNotRespondingException(string message, string loadingStage, IReadOnlyList<Result<DumpEntry>> dumpFileEntries) : base(message)
        {
            this.LoadingStage = loadingStage;
            this.DumpFileEntries = dumpFileEntries;
        }
#else
        internal DclApplicationNotRespondingException(string message, string loadingStage) : base(message)
        {
            this.LoadingStage = loadingStage;
        }
#endif
    }
}