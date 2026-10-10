namespace DCL.Diagnostics
{
    public interface IDecentralandException
    {
        ref readonly ReportData ReportData { get; }
        /// <summary>
        ///     A hack to prepend an exception message while the exception is logged through the native means
        /// </summary>
        internal string MessagePrefix { set; }
    }
}
