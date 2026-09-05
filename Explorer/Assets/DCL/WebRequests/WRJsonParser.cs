namespace DCL.WebRequests
{
    // ReSharper disable once InconsistentNaming
    public enum WRJsonParser
    {
        /// <summary>
        ///     Use Unity's built-in parser:  it's faster but can't parse arrays
        /// </summary>
        Unity,

        /// <summary>
        ///     Generally slower but can parse arrays
        /// </summary>
        Newtonsoft,

        /// <summary>
        ///     Special case when Unity parser leads to crashes in the Editor
        ///     but we still want to use it in the builds
        /// </summary>
        NewtonsoftInEditor,
    }
}
