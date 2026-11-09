namespace UUAV.Compat
{
    // Mirrors AVPro's ErrorCode. UUAV surfaces errors only as a state, so the
    // backend collapses every failure onto LoadFailed; the consumer just checks
    // for != None.
    public enum ErrorCode
    {
        None = 0,
        LoadFailed = 100,
    }
}
