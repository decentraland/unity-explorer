using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DCL.UIToolkit.Editor
{
    [SuppressMessage("Domain reload", "UDR0001:Domain Reload Analyzer")]
    public class UISpriteImporter : AssetPostprocessor
    {
        private const string SPRITES_PATH = "Assets/DCL/UIToolkit/Sprites";
        private const string STYLES_FOLDER = "Assets/DCL/UIToolkit/Styles/";

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool deletedSprite = deletedAssets.Any(deleted => deleted.StartsWith(SPRITES_PATH));

            if (deletedSprite)
                EditorApplication.delayCall += GenerateSpriteUss;
        }

        // TODO: If we ever use SVGs

        // private void OnPreprocessAsset()
        // {
        //     if (!assetImporter.assetPath.Contains(SPRITES_PATH)) return;
        //
        //     var svg = assetImporter as SVGImporter;
        //     if (svg == null) return;
        //     svg.SvgType = SVGType.UIToolkit;
        // }

        public void OnPreprocessTexture()
        {
            if (!assetImporter.assetPath.Contains(SPRITES_PATH)) return;
            var textureImporter = (TextureImporter)assetImporter;
            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.spriteImportMode = SpriteImportMode.Single;
        }

        private void OnPostprocessSprites(Texture2D texture, Sprite[] sprites)
        {
            if (!assetImporter.assetPath.Contains(SPRITES_PATH)) return;
            if (!assetImporter.importSettingsMissing) return; // No meta file, meaning its a new file
            EditorApplication.delayCall += GenerateSpriteUss;
        }

        [MenuItem("Decentraland/UI/Generate Sprite USS")]
        public static void GenerateSpriteUss()
        {
            foreach (var grouping in GetSpritesGroupedByDirectory())
            {
                Debug.Log($"Generating USS: {grouping.Key}");
                string uss = GenerateSpriteUss(grouping);
                Debug.Log(uss);

                string stylePathRelative = STYLES_FOLDER + $"Sprites-{GetCleanAtlasName(grouping.Key, false)}.uss";
                string stylePathAbsolute = Application.dataPath.Replace("Assets", "") + stylePathRelative;

                Debug.Log($"Writing USS: {grouping.Key} to file '{stylePathRelative}'");

                File.WriteAllText(stylePathAbsolute, uss);
                AssetDatabase.ImportAsset(stylePathRelative);
                Debug.Log($"USS processed: {grouping.Key}");
            }

            EditorUtility.UnloadUnusedAssetsImmediate();
            Debug.Log("Sprite USS generation finished.");
        }

        private static IEnumerable<IGrouping<string, string>> GetSpritesGroupedByDirectory(bool log = true)
        {
            return AssetDatabase.GetAllAssetPaths()
                                .OrderBy(s => s)
                                .Where(path =>
                                     path.StartsWith(SPRITES_PATH) && !Directory.Exists(path) &&
                                     AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Texture2D))
                                .Select(s =>
                                 {
                                     if (log)
                                         Debug.Log(
                                             $"Path: {s} type: {AssetDatabase.GetMainAssetTypeAtPath(s) == typeof(Texture2D)}");

                                     return s;
                                 })
                                .GroupBy(str => str.Split('/')[4]);
        }

        private static string GenerateSpriteUss(IGrouping<string, string> arg)
        {
            var sb = new StringBuilder();

            var names = new HashSet<string>();

            // Generate variables
            sb.AppendLine("/* AUTO GENERATED */");
            sb.AppendLine(":root {");

            foreach (string path in arg)
            {
                string name = GenerateSpriteVar(arg.Key, path, true);

                if (!names.Add(name))
                    throw new NotSupportedException($"Found duplicate sprite name in {arg.Key}: {name}");

                sb.AppendLine("    " + GenerateSpriteVar(arg.Key, path, true));
            }

            sb.AppendLine("}");
            sb.AppendLine();

            // Generate classes
            foreach (string path in arg)
            {
                string filename = Path.GetFileNameWithoutExtension(path);

                // Pressed versions get the :active pseudo class
                if (filename.EndsWith("-pressed"))
                {
                    sb.AppendLine(
                        $".sprite-{GetCleanAtlasName(arg.Key)}__{filename.Replace("-pressed", "")}:active {{");

                    sb.AppendLine($"    background-image: var({GenerateSpriteVar(arg.Key, path, false)});");
                }
                else
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                    if (sprite == null)
                        throw new NotSupportedException($"Found a file that isn't a sprite: {path}");

                    sb.AppendLine($".sprite-{GetCleanAtlasName(arg.Key)}__{Path.GetFileNameWithoutExtension(path)} {{");
                    sb.AppendLine($"    background-image: var({GenerateSpriteVar(arg.Key, path, false)});");
                    sb.AppendLine($"    width: {sprite.texture.width / 2f}px;");
                    sb.AppendLine($"    height: {sprite.texture.height / 2f}px;");
                }

                sb.AppendLine("}");

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string GenerateSpriteVar(string atlas, string path, bool full) =>
            $"--sprite-{GetCleanAtlasName(atlas)}__{Path.GetFileNameWithoutExtension(path)}" +
            (full ? $": url('/{path}');" : "");

        private static string GetCleanAtlasName(string atlas, bool lowercase = true)
        {
            if (lowercase)
                atlas = atlas.ToLowerInvariant();

            while (atlas.Contains(" "))
                atlas = atlas.Replace(" ", string.Empty);

            return atlas;
        }
    }
}
