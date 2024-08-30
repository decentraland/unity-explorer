using Segment.Serialization;

namespace Utility.Json
{
    public interface IJsonObjectBuilder
    {
        void Clear();

        void Set(string key, string value);

        void Set(string key, float value);

        void Set(string key, int value);

        JsonObject Build();

        void Release(JsonObject jsonObject);

        void DisposeCacheIfNeeded();
    }
}
