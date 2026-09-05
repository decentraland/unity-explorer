namespace DCL.Diagnostics
{
    public interface IReportsDebouncer
    {
        /// <summary>
        ///     Handlers the debouncer is applied to
        /// </summary>
        ReportHandler AppliedTo { get; }

        /// <summary>
        ///     Determine if the message should be skipped
        /// </summary>
        bool Debounce(ReportMessageFingerprint fingerprint);
    }
}
