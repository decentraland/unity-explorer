using System.Collections.Generic;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Helper class for building JSON with fluent API.
    /// </summary>
    public struct JsonBufferBuilder
    {
        private BufferedStringUploadHandler handler;
        private bool firstElement;

        public JsonBufferBuilder(BufferedStringUploadHandler handler)
        {
            this.handler = handler;
            firstElement = true;
        }

        public JsonBufferBuilder BeginObject()
        {
            handler.WriteByte((byte)'{');
            firstElement = true;
            return this;
        }

        public JsonBufferBuilder EndObject()
        {
            handler.WriteByte((byte)'}');
            return this;
        }

        public JsonBufferBuilder BeginArray()
        {
            handler.WriteByte((byte)'[');
            firstElement = true;
            return this;
        }

        public JsonBufferBuilder EndArray()
        {
            handler.WriteByte((byte)']');
            return this;
        }

        private void WriteCommaIfNeeded()
        {
            if (!firstElement) { handler.WriteByte((byte)','); }

            firstElement = false;
        }

        public JsonBufferBuilder Key(string key)
        {
            WriteCommaIfNeeded();
            handler.WriteJsonString(key);
            handler.WriteByte((byte)':');
            firstElement = true; // Reset for value
            return this;
        }

        public JsonBufferBuilder Value(string value)
        {
            WriteCommaIfNeeded();
            handler.WriteJsonString(value);
            return this;
        }

        public JsonBufferBuilder Value(int value)
        {
            WriteCommaIfNeeded();
            handler.WriteInt(value);
            return this;
        }

        public JsonBufferBuilder Value(long value)
        {
            WriteCommaIfNeeded();
            handler.WriteLong(value);
            return this;
        }

        public JsonBufferBuilder Value(bool value)
        {
            WriteCommaIfNeeded();
            handler.WriteBool(value);
            return this;
        }

        public JsonBufferBuilder NullValue()
        {
            WriteCommaIfNeeded();
            handler.WriteNull();
            return this;
        }

        public JsonBufferBuilder StringArray(IEnumerable<string> values)
        {
            BeginArray();

            foreach (string? value in values) { Value(value); }

            EndArray();
            return this;
        }

        public JsonBufferBuilder IntArray(IEnumerable<int> values)
        {
            BeginArray();

            foreach (int value in values) { Value(value); }

            EndArray();
            return this;
        }
    }
}
