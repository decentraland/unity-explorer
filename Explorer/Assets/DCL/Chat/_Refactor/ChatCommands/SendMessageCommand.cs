using DCL.Audio;
using DCL.Chat.ChatServices;
using DCL.Chat.MessageBus;
using DCL.Settings.Settings;
using System;

namespace DCL.Chat.ChatCommands
{
    public struct SendMessageCommandPayload
    {
        public string Body { get; set; }
    }

    public class SendMessageCommand
    {

        private readonly CurrentChannelService currentChannelService;
        private readonly IChatMessagesBus chatMessageBus;
        private readonly AudioClipConfig sound;
        private readonly ChatSettingsAsset chatSettings;

        public SendMessageCommand(
            IChatMessagesBus chatMessageBus,
            CurrentChannelService currentChannelService,
            AudioClipConfig sound,
            ChatSettingsAsset chatSettings)
        {
            this.currentChannelService = currentChannelService;
            this.sound = sound;
            this.chatSettings = chatSettings;
            this.chatMessageBus = chatMessageBus;
        }

        public void Execute(SendMessageCommandPayload commandPayload)
        {
            if (string.IsNullOrWhiteSpace(commandPayload.Body)) return;

            //TODO: This logic needs to discriminate which notifications to play depending on the type of message (if private or not)
            //depending on user's settings for notifications.
            if (chatSettings.chatAudioSettings == ChatAudioSettings.ALL)
                UIAudioEventsBus.Instance.SendPlayAudioEvent(sound);

            chatMessageBus.SendWithUtcNowTimestamp(
                currentChannelService.CurrentChannel,
                commandPayload.Body,
                ChatMessageOrigin.CHAT);
        }
    }
}
