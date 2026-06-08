using System;
using System.Collections.Generic;
using RichTypes;

namespace DCL.Diagnostics.Sentry
{
    public class DclApplicationNotRespondingException : Exception
    {
#if UNITY_STANDALONE_WIN
        public readonly IReadOnlyList<Result<DumpEntry>> DumpFilePaths;

        internal DclApplicationNotRespondingException(string message, IReadOnlyList<Result<DumpEntry>> dumpFilePaths) : base(message)
        {
            this.DumpFilePaths = dumpFilePaths;
        }
#else
        internal DclApplicationNotRespondingException(string message) : base(message) { }
#endif
    }
}
