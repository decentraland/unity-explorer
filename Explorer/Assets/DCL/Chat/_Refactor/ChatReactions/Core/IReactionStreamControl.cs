namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    /// Minimal start/stop control over the situational <see cref="StreamReactionsEmitter"/>, exposed so a
    /// debug chat command can drive the reaction stream without depending on the emitter's full lifecycle API.
    /// </summary>
    public interface IReactionStreamControl
    {
        void Start(float emitRate, float sendBudget);

        void Stop();
    }
}
