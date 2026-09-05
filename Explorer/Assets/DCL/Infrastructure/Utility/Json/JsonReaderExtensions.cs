using Newtonsoft.Json;

namespace Utility.Json
{
    public static class JsonReaderExtensions
    {
        public static float? ReadAsFloat(this JsonReader reader)
        {
            double? @double = reader.ReadAsDouble();

            if (@double == null)
                return null;

            return (float)@double.Value;
        }
    }
}
