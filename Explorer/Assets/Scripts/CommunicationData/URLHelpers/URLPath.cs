namespace CommunicationData.URLHelpers
{
    /// <summary>
    ///     The last chunk of URL address, there could be only one in URL
    ///     <para>
    ///         <b>parts-url</b> in https://blog.hubspot.com/marketing/parts-url
    ///     </para>
    /// </summary>
    public struct URLPath
    {
        public readonly string Value;

        public URLPath(string value)
        {
            Value = value;
        }

        public static URLPath FromString(string url) =>
            new (url);
    }
}
