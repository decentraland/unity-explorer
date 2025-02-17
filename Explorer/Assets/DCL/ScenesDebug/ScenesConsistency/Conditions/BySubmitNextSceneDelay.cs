using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.History;
using DCL.ScenesDebug.ScenesConsistency.DelayedResources;

namespace DCL.ScenesDebug.ScenesConsistency.Conditions
{
    public class BySubmitNextSceneDelay : INextSceneDelay
    {
        private readonly IDelayedResource<ChatView> chatViewResource;

        public BySubmitNextSceneDelay(IDelayedResource<ChatView> chatViewResource)
        {
            this.chatViewResource = chatViewResource;
        }

        public async UniTask WaitAsync()
        {
            var ready = false;

            void OnSubmit(ChatChannel _, string __, string ___)
            {
                ready = true;
            }

            ChatView resource = await chatViewResource.ResourceAsync();
            resource.InputSubmitted += OnSubmit;
            await UniTask.WaitUntil(() => ready);
            resource.InputSubmitted -= OnSubmit;
        }
    }
}
