namespace Diagnostics.ReportsHandling
{
    public interface IManagedEcsException
    {
        /// <summary>
        ///     A hack to prepend an exception message while the exception is logged through the native means
        /// </summary>
        internal string MessagePrefix { set; }

        ref readonly ReportData ReportData { get; }
    }
}
