using CRDT.Protocol.Factory;
using DCL.Diagnostics;
using System;
using System.Buffers;

namespace CRDT.Protocol
{
    public class LogCRDTProtocol : ICRDTProtocol
    {
        private const string PREFIX = nameof(LogCRDTProtocol);
        private readonly ICRDTProtocol origin;
        private readonly Action<string> log;

        public LogCRDTProtocol(ICRDTProtocol origin) : this(origin, ReportHub.WithReport(ReportCategory.CRDT).Log) { }

        public LogCRDTProtocol(ICRDTProtocol origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public void Dispose()
        {
            log($"{PREFIX}: Dispose");
            origin.Dispose();
        }

        public int GetMessagesCount()
        {
            int result = origin.GetMessagesCount();
            log($"{PREFIX}: GetMessagesCount {result}");
            return result;
        }

        public CRDTReconciliationResult ProcessMessage(in CRDTMessage message)
        {
            log($"{PREFIX}: ProcessMessage EntityId {message.EntityId} ComponentId {message.ComponentId} Type {message.Type}");
            return origin.ProcessMessage(message);
        }

        public void EnforceLWWState(in CRDTMessage message)
        {
            log($"{PREFIX}: EnforceLWWState {message}");
            origin.EnforceLWWState(message);
        }

        public int CreateMessagesFromTheCurrentState(ProcessedCRDTMessage[] preallocatedArray)
        {
            int result = origin.CreateMessagesFromTheCurrentState(preallocatedArray);
            log($"{PREFIX}: CreateMessagesFromTheCurrentState {result}");
            return result;
        }

        public ProcessedCRDTMessage CreateAppendMessage(CRDTEntity entity, int componentId, int timestamp, in IMemoryOwner<byte> data)
        {
            var result = origin.CreateAppendMessage(entity, componentId, timestamp, data);
            log($"{PREFIX}: CreateAppendMessage {result}");
            return result;
        }

        public ProcessedCRDTMessage CreatePutMessage(CRDTEntity entity, int componentId, in IMemoryOwner<byte> data)
        {
            var result = origin.CreatePutMessage(entity, componentId, data);
            log($"{PREFIX}: CreatePutMessage {result}");
            return result;
        }

        public ProcessedCRDTMessage CreateDeleteMessage(CRDTEntity entity, int componentId)
        {
            var result = origin.CreateDeleteMessage(entity, componentId);
            log($"{PREFIX}: CreateDeleteMessage {result}");
            return result;
        }
    }
}
