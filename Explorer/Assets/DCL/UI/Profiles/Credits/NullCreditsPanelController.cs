namespace DCL.Credits
{
    /// <summary>
    ///     No-op implementation used when the USER_CREDITS feature is disabled.
    /// </summary>
    public class NullCreditsPanelController : ICreditsPanelController
    {
        public void Dispose() { }
    }
}
