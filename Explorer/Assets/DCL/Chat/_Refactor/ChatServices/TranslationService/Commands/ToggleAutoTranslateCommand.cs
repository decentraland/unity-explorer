using DCL.Translation.Events;
using DCL.Translation.Settings;
using Utility;

public class ToggleAutoTranslateCommand
{
    private readonly ITranslationSettings settings;
    private readonly IEventBus eventBus;

    public ToggleAutoTranslateCommand(ITranslationSettings settings, IEventBus eventBus)
    {
        this.settings = settings;
        this.eventBus = eventBus;
    }

    public void Execute(string conversationId)
    {
        bool currentStatus = settings.GetAutoTranslateForConversation(conversationId);
        bool newStatus = !currentStatus;
        settings.SetAutoTranslateForConversation(conversationId, newStatus);

        eventBus.Publish(new TranslationEvents.ConversationAutoTranslateToggled
        {
            ConversationId = conversationId, IsEnabled = newStatus
        });
    }
}