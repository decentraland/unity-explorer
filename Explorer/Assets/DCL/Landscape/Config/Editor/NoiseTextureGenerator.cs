using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;

namespace DCL.Landscape.Config.Editor
{
    public abstract class NoiseTextureGenerator : UnityEditor.Editor
    {
        private static int currentResolutionIndex = 1;
        private static int lastResolutionIndex = 1;
        private RenderTexture texture;
        private bool isInitialized;
        private bool isRendered;
        private JobHandle handle;
        private ComputeShader computeShader;
        private ComputeBuffer resultBuffer;

        private void OnEnable()
        {
            computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/DCL/Landscape/Shaders/CS_NoiseTexture.compute");
        }

        private void OnDestroy()
        {
            resultBuffer?.Dispose();
            DisposeNativeArrays();
        }

        protected abstract void DisposeNativeArrays();

        public override void OnInspectorGUI()
        {
            bool newChanges = DrawDefaultInspector();

            GUILayout.Space(30);
            GUILayout.Label("Zoom");
            currentResolutionIndex = GUILayout.Toolbar(currentResolutionIndex, NoiseEditorUtils.TEXTURE_STRINGS);
            bool resolutionChanged = lastResolutionIndex != currentResolutionIndex;
            bool renderTexture = newChanges || !isInitialized || resolutionChanged;
            int textureSize = NoiseEditorUtils.TEXTURE_RESOLUTIONS[currentResolutionIndex];

            if (renderTexture)
            {
                if (!handle.IsCompleted)
                    handle.Complete();

                if (texture == null || resolutionChanged)
                {
                    if (texture != null)
                        DestroyImmediate(texture);

                    texture = new RenderTexture(textureSize, textureSize, 32);
                    texture.enableRandomWrite = true;
                    texture.Create();

                    SetupNoiseArray(textureSize);
                    resultBuffer?.Dispose();
                    resultBuffer = new ComputeBuffer(textureSize * textureSize, sizeof(float));
                    computeShader.SetTexture(0, NoiseEditorUtils.CS_RESULT, texture);
                }

                handle = ScheduleJobs(textureSize);
                isRendered = false;
                isInitialized = true;
                lastResolutionIndex = currentResolutionIndex;
            }

            if (handle.IsCompleted)
            {
                handle.Complete();

                if (!isRendered)
                {
                    resultBuffer.SetData(GetResultNoise());
                    computeShader.SetBuffer(0, NoiseEditorUtils.CS_NOISE_BUFFER, resultBuffer);
                    computeShader.SetFloat(NoiseEditorUtils.CS_WIDTH, textureSize);
                    computeShader.Dispatch(0, textureSize / 8, textureSize / 8, 1);
                    isRendered = true;
                }
            }

            DrawNoise();
        }

        protected abstract NativeArray<float> GetResultNoise();

        protected abstract void SetupNoiseArray(int textureSize);

        protected abstract JobHandle ScheduleJobs(int textureSize);

        private void DrawNoise()
        {
            Rect controlRect = EditorGUILayout.GetControlRect(GUILayout.Height(NoiseEditorUtils.TEXTURE_MAX_SIZE + 30));
            float size = Mathf.Min(controlRect.width, NoiseEditorUtils.TEXTURE_MAX_SIZE);
            var textureRect = new Rect(controlRect.center.x - (size * 0.5f), controlRect.y, size, size);
            textureRect.y += 20;
            EditorGUI.DrawPreviewTexture(textureRect, texture);
        }
    }
}
