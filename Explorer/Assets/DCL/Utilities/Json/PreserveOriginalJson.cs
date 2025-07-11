using Newtonsoft.Json;

namespace DCL.Utilities.Json
{
    [JsonConverter(typeof(OriginalJsonContainerConverter))]
    public abstract class PreserveOriginalJson : IPreserveOriginalJson
    {
        [JsonIgnore]
        public string OriginalJson { get; set; }
    }
}
