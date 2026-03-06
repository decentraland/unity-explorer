using System;
using DCL.Chat.ChatReactions;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Chat.Reactions
{
    /// <summary>
    /// Thin MonoBehaviour driver for the situational reaction particle system.
    /// Physics tick runs in Update; rendering runs in URP's beginCameraRendering
    /// callback so the camera transform is guaranteed to be final (after Cinemachine).
    /// </summary>
    public sealed class SituationalReactionController : MonoBehaviour, ISituationalReactionService, IDisposable
    {
        [field: SerializeField] public RectTransform LaneRect { get; private set; } = null!;

        private ChatReactionSimulation? simulation;
        private Camera? cachedMainCamera;

        public void Initialize(ChatReactionsSituationalConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            simulation?.Dispose();
            simulation = new ChatReactionSimulation(config, LaneRect);
        }

        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            simulation?.Dispose();
            simulation = null;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        private void Update()
        {
            cachedMainCamera = Camera.main;
            simulation?.Tick(Time.unscaledDeltaTime);
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam != cachedMainCamera || cachedMainCamera == null) return;

            simulation?.Draw(cam);
        }

        public void TriggerUIReaction(int emojiIndex, int count) =>
            simulation?.TriggerUIReaction(emojiIndex, count);

        public void TriggerUIReactionFromRect(RectTransform sourceRect, int emojiIndex, int count) =>
            simulation?.TriggerUIReactionFromRect(sourceRect, emojiIndex, count);

        public void TriggerDefaultUIReaction() =>
            simulation?.TriggerDefaultUIReaction();

        public void TriggerDefaultUIReactionFromRect(RectTransform sourceRect) =>
            simulation?.TriggerDefaultUIReactionFromRect(sourceRect);

        public void BeginUIStream(RectTransform sourceRect) =>
            simulation?.BeginUIStream(sourceRect);

        public void EndUIStream() =>
            simulation?.EndUIStream();

        public void ToggleUIStream(RectTransform sourceRect) =>
            simulation?.ToggleUIStream(sourceRect);
    }
}
