namespace DCL.Diagnostics
{
    /// <summary>
    ///     Receives the reports a <see cref="ReportSinkHandler" /> lets through. Called from whichever thread reported.
    /// </summary>
    public interface IReportSink
    {
        void Receive(in ReportLogEntry entry);
    }
}
