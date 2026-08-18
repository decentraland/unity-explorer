namespace DCL.Diagnostics.Sentry
{
    /// <summary>User-authored content of a single Sentry User Feedback submission.</summary>
    public readonly struct SentryUserFeedbackReport
    {
        public readonly string Message;
        public readonly string? ContactEmail;
        public readonly string? UserName;
        public readonly byte[]? Image;
        public readonly string? ImageContentType;

        public SentryUserFeedbackReport(string message, string? contactEmail, string? userName, byte[]? image, string? imageContentType)
        {
            Message = message;
            ContactEmail = contactEmail;
            UserName = userName;
            Image = image;
            ImageContentType = imageContentType;
        }
    }
}
