using System.IO;
using System.Runtime.InteropServices;

namespace CRDT.Protocol.Factory
{
    /// <summary>
    ///     Contains the size field denoting the number of bytes required for Serialization
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ProcessedCRDTMessage
    {
        public readonly CRDTMessage message;
        public readonly int CRDTMessageDataLength;

        public ProcessedCRDTMessage(CRDTMessage message, int crdtMessageDataLength)
        {
            this.message = message;
            CRDTMessageDataLength = crdtMessageDataLength;
        }

        public override string ToString() =>
            $"Message: {message} CRDTMessageDataLength: {CRDTMessageDataLength}";

        public void LogSelf(string fromPlace)
        {
            INTERNAL_LOG.Log($"from place {fromPlace}: {ToString()}");
        }

        private static readonly IInternalLog INTERNAL_LOG =
#if UNITY_EDITOR
            new InternalLog(
                new StreamWriter(
                    new FileStream(
                        Path.Combine(Directory.GetCurrentDirectory(), "Assets/Scripts/CRDT/Serializer/log.txt"),
                        FileMode.Create
                    )
                )
            );
#else
            new IInternalLog.Null();
#endif

        private interface IInternalLog
        {
            void Log(string message);

            public class Null : IInternalLog
            {
                public void Log(string message)
                {
                    //ignore
                }
            }
        }

        private class InternalLog : IInternalLog
        {
            private readonly StreamWriter writer;

            public InternalLog(StreamWriter writer)
            {
                this.writer = writer;
            }

            public void Log(string message)
            {
                writer.WriteLine(message);
            }
        }
    }
}
