using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.History;
using DCL.ScenesDebug.ScenesConsistency.DelayedResources;

namespace DCL.ScenesDebug.ScenesConsistency.Conditions
{
    public class BySubmitNextSceneDelay : INextSceneDelay
    {
        private readonly IDelayedResource<ChatPanelView> chatViewResource;

        public BySubmitNextSceneDelay(IDelayedResource<ChatPanelView> chatViewResource)
        {
            this.chatViewResource = chatViewResource;
        }

        public async UniTask WaitAsync()
        {
            var ready = false;

            void OnSubmit()
            {
                ready = true;
            }

            ChatPanelView resource = await chatViewResource.ResourceAsync();
            resource.InputView.DebugOnSubmit += OnSubmit;
            await UniTask.WaitUntil(() => ready);
            resource.InputView.DebugOnSubmit -= OnSubmit;
        }
    }
}
