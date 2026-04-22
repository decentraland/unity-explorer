namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    /// Immutable domain object describing a video currently being played:
    /// which stream it is and where it comes from (user, presentation bot, or current-stream fallback).
    /// Callers read sourcing via <see cref="IsPresentationBot"/> / <see cref="Identity"/> rather than
    /// probing raw identity strings.
    /// </summary>
    public readonly struct VideoEntity
    {
        public readonly LivekitAddress Source;

        private VideoEntity(LivekitAddress source)
        {
            this.Source = source;
        }

        public bool IsPresentationBot => Source.IsPresentationBotStream(out _);

        public string? Identity => Source.Match<string?>(
            onUserStream: static userStream => userStream.Identity,
            onPresentationBotStream: static bot => bot.Identity,
            onCurrentStream: static () => null
        );

        public static VideoEntity FromUser(UserStream userStream) =>
            new (LivekitAddress.FromUserStream(userStream));

        public static VideoEntity FromPresentationBot(PresentationBotStream bot) =>
            new (LivekitAddress.FromPresentationBotStream(bot));

        public static VideoEntity FromIdentity(string identity, string sid) =>
            identity.IsPresentationBotIdentity()
                ? FromPresentationBot(new PresentationBotStream(identity, sid))
                : FromUser(new UserStream(identity, sid));
    }
}
