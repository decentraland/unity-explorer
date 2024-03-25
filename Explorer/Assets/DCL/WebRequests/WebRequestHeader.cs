namespace DCL.WebRequests
{
    public readonly struct WebRequestHeader
    {
        public readonly string Name;
        public readonly string Value;

        public WebRequestHeader(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public override string ToString() =>
            $"WebRequestHeader({Name}: {Value})";
    }
}
