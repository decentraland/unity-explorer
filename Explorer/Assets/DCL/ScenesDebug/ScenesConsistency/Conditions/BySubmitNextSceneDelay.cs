using Cysharp.Threading.Tasks;
using DCL.Chat;
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

            void OnSubmit(string _)
            {
                ready = true;
            }

            ChatView resource = await chatViewResource.ResourceAsync();
            resource.InputField.onSubmit!.AddListener(OnSubmit);
            await UniTask.WaitUntil(() => ready);
            resource.InputField.onSubmit.RemoveListener(OnSubmit);
        }
    }
}
