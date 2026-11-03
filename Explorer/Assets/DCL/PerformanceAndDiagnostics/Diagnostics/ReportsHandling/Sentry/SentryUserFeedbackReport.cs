namespace DCL.Diagnostics.Sentry
{
    /// <summary>User-authored content of a single Sentry User Feedback submission.</summary>
    public readonly struct SentryUserFeedbackReport
    {
        public readonly string Message;
        public readonly string? ContactEmail;
        public readonly string? UserName;

        public SentryUserFeedbackReport(string message, string? contactEmail, string? userName)
        {
            Message = message;
            ContactEmail = contactEmail;
            UserName = userName;
        }
    }
}
