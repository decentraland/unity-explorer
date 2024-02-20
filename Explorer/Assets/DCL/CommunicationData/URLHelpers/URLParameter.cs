namespace CommunicationData.URLHelpers
{
    public readonly struct URLParameter
    {
        public readonly string Name;
        public readonly string Value;

        public URLParameter(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public static implicit operator URLParameter((string name, string value) tuple) =>
            new (tuple.name, tuple.value);
    }
}
