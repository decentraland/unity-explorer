namespace DCL.Diagnostics.Sentry
{
    /// <summary>
    ///     User-authored content of a single Sentry User Feedback submission.
    /// </summary>
    public readonly struct SentryUserFeedbackReport
    {
        public readonly string Message;
        public readonly string? ContactEmail;
        public readonly string? UserName;

        /// <summary>
        ///     Encoded image bytes provided by the user. A feedback envelope carries a single attachment,
        ///     so this image takes that slot and the log travels on a linked event.
        /// </summary>
        public readonly byte[]? Image;

        /// <summary>
        ///     Mime type of <see cref="Image" />, e.g. "image/jpeg". Ignored when <see cref="Image" /> is null.
        /// </summary>
        public readonly string? ImageContentType;

        public readonly bool AttachLog;

        public SentryUserFeedbackReport(string message, string? contactEmail, string? userName, byte[]? image, string? imageContentType, bool attachLog)
        {
            Message = message;
            ContactEmail = contactEmail;
            UserName = userName;
            Image = image;
            ImageContentType = imageContentType;
            AttachLog = attachLog;
        }
    }
}
