using System.Threading;

namespace Utility.Multithreading
{
    public sealed class InterlockedFlag
    {
        private int isSet;

        public bool IsSet => isSet != 0;

        public bool Set() =>
            DCLInterlocked.Exchange(ref isSet, 1) == 0;

        public bool Reset() =>
            DCLInterlocked.Exchange(ref isSet, 0) != 0;

        public static implicit operator bool(InterlockedFlag flag) =>
            flag.IsSet;
    }
}
