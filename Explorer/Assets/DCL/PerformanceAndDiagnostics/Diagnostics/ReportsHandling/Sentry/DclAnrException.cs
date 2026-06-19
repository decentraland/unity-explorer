using System;
using System.Collections.Generic;
using RichTypes;

namespace DCL.Diagnostics.Sentry
{
    public class DclApplicationNotRespondingException : Exception
    {
#if UNITY_STANDALONE_WIN
        public readonly IReadOnlyList<Result<DumpEntry>> DumpFileEntries;

        internal DclApplicationNotRespondingException(string message, IReadOnlyList<Result<DumpEntry>> dumpFileEntries) : base(message)
        {
            this.DumpFileEntries = dumpFileEntries;
        }
#else
        internal DclApplicationNotRespondingException(string message) : base(message) { }
#endif
    }
}
