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

        private static readonly InternalLog INTERNAL_LOG = new (
            new StreamWriter(
                new FileStream(
                    "/Users/nickkhalow/Projects/unity-explorer/Explorer/Assets/Scripts/CRDT/Serializer/log.txt",
                    FileMode.Create
                )
            )
        );

        private class InternalLog
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
