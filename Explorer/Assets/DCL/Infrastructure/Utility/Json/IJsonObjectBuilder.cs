using Segment.Serialization;
using System;

namespace Utility.Json
{
    public interface IJsonObjectBuilder
    {
        void Set(string key, string value);

        void Set(string key, float value);

        void Set(string key, int value);

        /// <summary>
        ///     Builds Json Object and clears internal keys. This operation is not idempotent
        /// </summary>
        JsonObject Build();

        void Release(JsonObject jsonObject);
    }

    public static class JsonObjectBuilderExtensions
    {
        public static PooledJsonObject BuildPooled(this IJsonObjectBuilder jsonObjectBuilder) =>
            new (jsonObjectBuilder);
    }

    public readonly struct PooledJsonObject : IDisposable
    {
        public readonly JsonObject Json;
        private readonly IJsonObjectBuilder source;

        public PooledJsonObject(IJsonObjectBuilder source)
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
