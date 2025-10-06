using System.Collections.Concurrent;

namespace DCL.MCP
{
    public static class MCPRequestsQueue
    {
        private static readonly ConcurrentQueue<MCPCreateTextShapeRequest> textShapeQueue = new ();

        public static void EnqueueTextShape(in MCPCreateTextShapeRequest request) =>
            textShapeQueue.Enqueue(request);

        public static bool TryDequeueTextShape(out MCPCreateTextShapeRequest request) =>
            textShapeQueue.TryDequeue(out request);
    }
}
