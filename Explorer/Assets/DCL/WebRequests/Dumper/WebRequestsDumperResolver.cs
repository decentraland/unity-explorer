using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DCL.WebRequests.Dumper
{
#pragma warning disable CS8618
    public class WebRequestsDumperResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            var props =
                type.GetFields(FLAGS)
                    .Select(f => base.CreateProperty(f, memberSerialization))
                    .Concat(type.GetProperties(FLAGS).Where(p => p.GetSetMethod(true) != null).Select(p => base.CreateProperty(p, memberSerialization)))
                    .ToList();

            foreach (JsonProperty? p in props)
            {
                p.Readable = true;
                p.Writable = true; // allows setting private fields on deserialize
            }

            return props;
        }
    }
}
