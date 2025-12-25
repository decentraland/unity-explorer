using Newtonsoft.Json.Linq;
using System;

namespace Utility.Json
{
    public static class JsonObjectBuilderExtensions
    {
        public static PooledJsonObject BuildPooled(this JsonObjectBuilder jsonObjectBuilder) =>
            new (jsonObjectBuilder);
    }

    public readonly struct PooledJsonObject : IDisposable
    {
        public readonly JObject Json;
        private readonly JsonObjectBuilder source;

        public PooledJsonObject(JsonObjectBuilder source)
        {
            this.source = source;
            Json = source.Build();
        }

        public void Dispose()
        {
            source.Release(Json);
        }
    }
}
