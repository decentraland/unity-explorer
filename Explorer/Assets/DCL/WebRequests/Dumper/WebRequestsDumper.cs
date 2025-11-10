using CodeLess.Attributes;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace DCL.WebRequests.Dumper
{
    public class WebRequestsDumperResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            var props =
                type.GetFields(FLAGS)
                    .Select(f => base.CreateProperty(f, memberSerialization))
                    .Concat(type.GetProperties(FLAGS).Select(p => base.CreateProperty(p, memberSerialization)))
                    .ToList();

            foreach (JsonProperty? p in props)
            {
                p.Readable = true;
                p.Writable = true; // allows setting private fields on deserialize
            }

            return props;
        }
    }

    [Singleton(SingletonGenerationBehavior.ALLOW_IMPLICIT_CONSTRUCTION)]
    public partial class WebRequestsDumper
    {
        public bool Enabled { get; set; }

        public string Filter { get; set; }
    }

    public class WebRequestDump
    {
        [Serializable]
        public class Entry
        {
            public string webRequestType;
            public string argsType;
            public Envelope envelope;
        }

        [Serializable]
        public class Envelope
        {
            public readonly Type RequestType;
            public readonly CommonArguments CommonArguments;
            public readonly Type ArgsType;
            public readonly object Args;
            public readonly WebRequestHeadersInfo HeadersInfo;

            // Sign is not supported

            [JsonConstructor]
            internal Envelope(Type requestType, CommonArguments commonArguments, Type argsType, object args, WebRequestHeadersInfo headersInfo)
            {
                CommonArguments = commonArguments;
                ArgsType = argsType;
                Args = args;
                HeadersInfo = headersInfo;
                RequestType = requestType;
            }

            public UniTask<WebRequestUtils.NoResult> RecreateWithNoOp() { }
        }
    }
}
