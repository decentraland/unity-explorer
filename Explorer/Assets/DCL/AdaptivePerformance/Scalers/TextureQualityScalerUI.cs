// using UnityEngine;
// using UnityEngine.AdaptivePerformance;
//
// // This attribute allows you to create an asset from this script
// // via Assets > Create > Scriptable Objects > Texture Quality Scaler UI.
// [CreateAssetMenu(fileName = "TextureQualityScaler_UI", menuName = "Scriptable Objects/Texture Quality Scaler UI")]
// public class TextureQualityScalerUI : AdaptivePerformanceScaler
// {
//     private int m_DefaultTextureLimit;
//
//     protected override void OnEnabled()
//     {
//         // Store the original texture quality setting when the scaler is enabled.
//         m_DefaultTextureLimit = QualitySettings.globalTextureMipmapLimit;
//     }
//
//     protected override void OnDisabled()
//     {
//         // Restore the original setting when the scaler is disabled.
//         QualitySettings.globalTextureMipmapLimit = m_DefaultTextureLimit;
//     }
//
//     // This method is called when the performance level changes.
//     protected override void OnLevel()
//     {
//         // The base class calculates the new `Scale` value based on the current performance level.
//         // Apply the new scale to the global mipmap limit.
//         // A higher scale (better quality) maps to a lower mipmap limit (better quality).
//         if (ScaleChanged())
//         {
//             // Adjust the global texture mipmap limit based on the new scale.
//             Debug.Log($"TextureQualityScalerUI new scale: {Scale}");
//             QualitySettings.globalTextureMipmapLimit = (int)MaxBound - ((int)(MaxBound * Scale));
//         }
//     }
// }
