using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    ///     Standalone reproduction probe for the paletted-PNG decode defect: LoadImage the file at
    ///     PNG_PROBE_SRC, re-encode the decoded pixels to PNG_PROBE_DST. Two runs producing different
    ///     bytes prove the decoder fills the texture with uninitialized memory.
    /// </summary>
    public static class PngProbe
    {
        public static void Run()
        {
            string src = Environment.GetEnvironmentVariable("PNG_PROBE_SRC");
            string dst = Environment.GetEnvironmentVariable("PNG_PROBE_DST");

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool ok = tex.LoadImage(File.ReadAllBytes(src));
            File.WriteAllBytes(dst, tex.EncodeToPNG());

            Debug.Log($"[PngProbe] ok={ok} {tex.width}x{tex.height} fmt={tex.format}");
            EditorApplication.Exit(0);
        }

        public static void RunPaletted()
        {
            string src = Environment.GetEnvironmentVariable("PNG_PROBE_SRC");
            string dst = Environment.GetEnvironmentVariable("PNG_PROBE_DST");

            byte[] data = File.ReadAllBytes(src);
            bool isPal = DCL.WebRequests.PalettedPng.IsPalettedPng(data);
            bool ok = DCL.WebRequests.PalettedPng.TryDecode(data, false, out Texture2D tex);
            if (ok) File.WriteAllBytes(dst, tex.EncodeToPNG());

            Debug.Log($"[PngProbe] paletted={isPal} decoded={ok} {(ok ? $"{tex.width}x{tex.height}" : "-")}");
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
