using DCL.Landscape.Jobs;
using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DCL.Landscape.Config.Editor
{
    [CustomEditor(typeof(NoiseData))]
    public class NoiseDataEditor : UnityEditor.Editor
    {
        private const float TEXTURE_MAX_SIZE = 450;
        private static readonly string[] TEXTURE_STRINGS = { "256", "512", "1024", "2048" };
        private static readonly int[] TEXTURE_RESOLUTIONS = { 256, 512, 1024, 2048 };
        private int currentResolutionIndex = 1;
        private int lastResolutionIndex = 1;
        private RenderTexture texture;
        private bool isInitialized;
        private bool isRendered;

        private NativeArray<float> noiseResults;
        private NativeArray<float2> octaveOffsets;
        private JobHandle handle;
        private Stopwatch timer;
        private ComputeShader computeShader;
        private ComputeBuffer resultBuffer;
        private static readonly int CS_RESULT = Shader.PropertyToID("ResultTexture");
        private static readonly int CS_NOISE_BUFFER = Shader.PropertyToID("NoiseBuffer");
        private static readonly int CS_WIDTH = Shader.PropertyToID("Width");

        private void OnEnable()
        {
            timer = new Stopwatch();
            computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/DCL/Landscape/Shaders/CS_NoiseTexture.compute");
        }

        private void OnDestroy()
        {
            DisposeNativeArrays();
        }

        private void DisposeNativeArrays()
        {
            noiseResults.Dispose();
            octaveOffsets.Dispose();
            resultBuffer?.Dispose();
        }

        public override void OnInspectorGUI()
        {
            bool newChanges = DrawDefaultInspector();

            GUILayout.Space(30);
            GUILayout.Label("Preview Size");
            currentResolutionIndex = GUILayout.Toolbar(currentResolutionIndex, TEXTURE_STRINGS);
            bool resolutionChanged = lastResolutionIndex != currentResolutionIndex;
            bool renderTexture = newChanges || !isInitialized || resolutionChanged;
            int textureSize = TEXTURE_RESOLUTIONS[currentResolutionIndex];

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

                    noiseResults.Dispose();
                    noiseResults = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
                    resultBuffer?.Dispose();
                    resultBuffer = new ComputeBuffer(textureSize * textureSize, sizeof(float));
                    computeShader.SetTexture(0, CS_RESULT, texture);
                }

                var data = serializedObject.targetObject as NoiseData;
                if (data == null) return;

                data.settings.ValidateValues();

                octaveOffsets.Dispose();
                octaveOffsets = new NativeArray<float2>(data.settings.octaves, Allocator.Persistent);
                float maxPossibleHeight = Noise.CalculateOctaves(data, ref octaveOffsets);

                var job = new NoiseJob
                {
                    Width = textureSize,
                    Height = textureSize,
                    NoiseSettings = data.settings,
                    OctaveOffsets = octaveOffsets,
                    Result = noiseResults,
                    MaxHeight = maxPossibleHeight,
                };

                timer.Start();
                handle = job.Schedule(textureSize * textureSize, 32);
                isRendered = false;
                isInitialized = true;
                lastResolutionIndex = currentResolutionIndex;
            }

            if (handle.IsCompleted)
            {
                handle.Complete();

                if (!isRendered)
                {
                    // Send this crap to the GPU
                    /*Color[] colourMap = new Color[textureSize * textureSize];
                    for (var y = 0; y < textureSize; y++)
                    for (var x = 0; x < textureSize; x++)
                    {
                        int index = (y * textureSize) + x;
                        colourMap[index] = Color.Lerp(Color.black, Color.white, noiseResults[index]);
                    }
                    texture.SetPixels(colourMap);
                    texture.Apply();*/
                    resultBuffer.SetData(noiseResults);
                    computeShader.SetBuffer(0, CS_NOISE_BUFFER, resultBuffer);
                    computeShader.SetFloat(CS_WIDTH, textureSize);
                    computeShader.Dispatch(0, textureSize / 8, textureSize / 8, 1);
                    isRendered = true;
                }
            }

            DrawNoise();
        }

        private void DrawNoise()
        {
            Rect controlRect = EditorGUILayout.GetControlRect(GUILayout.Height(200));
            float size = Mathf.Min(controlRect.width, TEXTURE_MAX_SIZE);
            var textureRect = new Rect(controlRect.center.x - (size * 0.5f), controlRect.y, size, size);
            textureRect.y += 20;
            EditorGUI.DrawPreviewTexture(textureRect, texture);
        }
    }
}
