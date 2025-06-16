namespace DCL.WebRequests
{
    public readonly struct PartialDownloadArguments
    {
        /// <summary>
        ///     Stream that was previously created on downloading of the first chunk of the given file
        /// </summary>
        public readonly PartialDownloadStream? Stream;

        public PartialDownloadArguments(PartialDownloadStream? stream)
        {
            Stream = stream;
        }
    }
}
