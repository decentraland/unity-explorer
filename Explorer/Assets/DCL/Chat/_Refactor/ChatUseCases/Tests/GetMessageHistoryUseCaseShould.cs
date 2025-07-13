using NUnit.Framework;
using NSubstitute;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DCL.Chat.ChatUseCases;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.UI.InputFieldFormatting;

public class GetMessageHistoryUseCaseShould
{
    private GetMessageHistoryUseCase useCase;
    private IChatHistory mockChatHistory;
    private ChatHistoryStorage mockChatHistoryStorage;
    private CreateMessageViewModelUseCase mockCreateViewModelUseCase;

    [SetUp]
    public void SetUp()
    {
        mockChatHistory = Substitute.For<IChatHistory>();
        
        mockChatHistoryStorage = Substitute.For<ChatHistoryStorage>(null, null, null); 
        
        var mockFormatter = Substitute.For<ITextFormatter>();
        mockCreateViewModelUseCase = Substitute.For<CreateMessageViewModelUseCase>(mockFormatter);

        useCase = new GetMessageHistoryUseCase(
            mockChatHistory,
            mockChatHistoryStorage,
            mockCreateViewModelUseCase
        );
    }

    [Test]
    public async Task InsertSeparatorAtCorrectPosition_WhenUnreadMessagesExist()
    {
        var messages = new List<ChatMessage>();
        for (int i = 0; i < 10; i++)
        {
            messages.Add(new ChatMessage($"Message {i}",
                "sender",
                "0x123",
                true,
                "wallet",
                false));
        }

        var fakeChannel = new ChatChannel(ChatChannel.ChatChannelType.USER, "test-channel");
        foreach (var msg in messages) fakeChannel.AddMessage(msg);
        fakeChannel.ReadMessages = 5;

        ChatChannel? outChannel = fakeChannel;
        mockChatHistory.Channels.TryGetValue(Arg.Any<ChatChannel.ChannelId>(), out outChannel)
                       .Returns(true);

        mockCreateViewModelUseCase.Execute(Arg.Any<ChatMessage>())
                                  .Returns(callInfo =>
                                  {
                                      var msg = (ChatMessage)callInfo[0];
                                      return new ChatMessageViewModel { Message = msg.Message, IsSeparator = msg.IsSeparator };
                                  });

        var result = await useCase.ExecuteAsync(new ChatChannel.ChannelId("test-channel"), CancellationToken.None);

        Assert.AreEqual(11, result.Messages.Count);
        Assert.IsTrue(result.Messages[5].IsSeparator, "The separator was not at the expected index 5.");
        Assert.IsFalse(result.Messages[4].IsSeparator);
        Assert.IsFalse(result.Messages[6].IsSeparator);
    }
}