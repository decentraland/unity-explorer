using System;
using System.Collections.Generic;
using RichTypes;

namespace DCL.Diagnostics.Sentry
{
    public class DclApplicationNotRespondingException : Exception
    {
        public readonly string LoadingStage;
        public readonly int SessionAgeSeconds;

#if UNITY_STANDALONE_WIN
        public readonly IReadOnlyList<Result<DumpEntry>> DumpFileEntries;

        internal DclApplicationNotRespondingException(string message, string loadingStage, int sessionAgeSeconds, IReadOnlyList<Result<DumpEntry>> dumpFileEntries) : base(message)
        {
            this.LoadingStage = loadingStage;
            this.SessionAgeSeconds = sessionAgeSeconds;
            this.DumpFileEntries = dumpFileEntries;
        }
#else
        internal DclApplicationNotRespondingException(string message, string loadingStage, int sessionAgeSeconds) : base(message)
        {
            this.LoadingStage = loadingStage;
            this.SessionAgeSeconds = sessionAgeSeconds;
        }
#endif
    }
}
