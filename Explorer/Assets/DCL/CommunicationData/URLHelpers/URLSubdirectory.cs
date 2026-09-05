namespace CommunicationData.URLHelpers
{
    /// <summary>
    ///     URL can contain multiple subdirectories
    ///     <b>marketing</b> in https://blog.hubspot.com/marketing/parts-url
    /// </summary>
    public readonly struct URLSubdirectory
    {
        public static readonly URLSubdirectory EMPTY = new (string.Empty);

        public readonly string Value;

        public bool IsEmpty() =>
            string.IsNullOrEmpty(Value);

        private URLSubdirectory(string value)
        {
            Value = value;
        }

        public static URLSubdirectory FromString(string url) =>
            new (url);
    }
}
