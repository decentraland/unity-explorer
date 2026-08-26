using System;
using System.Linq;
using System.Threading.Tasks;
using Data;
using DCL.GLTFast.Wrappers;
using GLTFast;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace Loading
{
    public static class GLTFLoader
    {
        public static async Task<LoadedModel> LoadModel(BodyShape bodyShape, EntityDefinition entityDefinition,
            Transform parent)
        {
            var representation = entityDefinition[bodyShape];

            var importer = new GltfImport(
                downloadProvider: new BinaryDownloadProvider(representation.Files),
                materialGenerator: new ToonMaterialGenerator()
            );

            var importSettings = new ImportSettings
            {
                NodeNameMethod = NameImportMethod.OriginalUnique,
                AnisotropicFilterLevel = 0,
                GenerateMipMaps = false,
                AnimationMethod = AnimationMethod.None,
            };

            var file = representation.Files[representation.MainFile];

            Debug.Log($"Loading GLB: {representation.MainFile} - {file}");

            var success = await importer.Load(file, importSettings);

            if (success)
            {
                var root = new GameObject(entityDefinition.Category);
                root.SetActive(false);
                root.transform.SetParent(parent, false);

                var instantiator = new GameObjectInstantiator(importer, root.transform);
                await importer.InstantiateSceneAsync(instantiator);

                Sanitize(root.transform);

                Debug.Log($"GLB loaded: {representation.MainFile}");

                return new LoadedModel(entityDefinition, root, importer);
            }

            throw new Exception($"Failed to load GLB: {representation.MainFile}");
        }

        public static async Task<LoadedFacialFeature> LoadFacialFeature(BodyShape bodyShape,
            EntityDefinition entityDefinition)
        {
            var rep = entityDefinition[bodyShape];

            var mainTexture = await LoadTexture(rep.Files[rep.MainFile]);
            if (!mainTexture) throw new Exception($"Failed to load texture {rep.Files[rep.MainFile]}");

            var maskTexture = rep.Files.Count == 2
                ? await LoadTexture(rep.Files[rep.Files.Keys.First(x => x != rep.MainFile)])
                : null;

            return new LoadedFacialFeature(entityDefinition, mainTexture, maskTexture);

            async Awaitable<Texture2D> LoadTexture(string file)
            {
                using var webRequest = UnityWebRequestTexture.GetTexture(file, true);

                await webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to load texture {file}: {webRequest.error}");
                    return null;
                }

                return DownloadHandlerTexture.GetContent(webRequest);
            }
        }

        public static async Awaitable<LoadedEmote> LoadEmote(BodyShape bodyShape, EntityDefinition entityDefinition, Transform propParent)
        {
            var rep = entityDefinition[bodyShape];

            var importer = new GltfImport(
                materialGenerator: new DecentralandMaterialGenerator("DCL/Scene", true),
                downloadProvider: new BinaryDownloadProvider(rep.Files)
            );

            var importSettings = new ImportSettings
            {
                NodeNameMethod = NameImportMethod.OriginalUnique,
                AnisotropicFilterLevel = 0,
                AnimationMethod = AnimationMethod.Legacy,
                GenerateMipMaps = false,
            };

            var file = rep.Files[rep.MainFile];

            Debug.Log($"Loading Emote: {rep.MainFile} - {file}");

            var success = await importer.Load(file, importSettings);

            AudioClip audioClip = null;

            // TODO: Clean this up cmon
            var audioFile = rep.Files.FirstOrDefault(kvp =>
                kvp.Key.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));
            if (audioFile.Key != null)
            {
                Debug.Log($"Loading audio clip: {audioFile.Key} - {audioFile.Value}");

                var audioType = AudioType.UNKNOWN;


                if (audioFile.Key.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    audioType = AudioType.MPEG;

                if (audioFile.Key.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    audioType = AudioType.WAV;

                if (audioFile.Key.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                    audioType = AudioType.OGGVORBIS;


                using var www = UnityWebRequestMultimedia.GetAudioClip(audioFile.Value, audioType);
                await www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError)
                {
                    Debug.Log(www.error);
                }
                else
                {
                    audioClip = DownloadHandlerAudioClip.GetContent(www);
                }
            }

            if (success)
            {
                var clips = importer.GetAnimationClips();
                var looping = (entityDefinition.Flags & EntityFlags.Looping) != 0;
                Debug.Log($"Loaded emote: {rep.MainFile} with clips: {clips.Length}");
                
                var (avatarClip, propClip) = ExtractClips(clips);
                GameObject prop = null;
                Animation propAnim = null;
                
                avatarClip.wrapMode = looping ? WrapMode.Loop : WrapMode.Clamp;

                if (propClip != null)
                {
                    // Avatar animation controls prop clip so we don't auto loop it
                    propClip.wrapMode = WrapMode.Clamp;

                    // We have a prop we need to deal with
                    Debug.Log("Lading emote prop");
                    prop = new GameObject("emote");
                    prop.SetActive(false);
                    prop.transform.SetParent(propParent, false);
                    propAnim = prop.AddComponent<Animation>();
                    propClip.name = entityDefinition.URN;
                    propAnim.AddClip(propClip, entityDefinition.URN);
                    propAnim.clip = propClip;

                    await importer.InstantiateMainSceneAsync(prop.transform);

                    Sanitize(prop.transform);
                }

                return new LoadedEmote(entityDefinition, avatarClip, audioClip, prop, propAnim, importer);
            }

            throw new NotSupportedException($"Failed to load emote: {rep.MainFile}");
        }

        private static (AnimationClip avatarCliP, AnimationClip propClip) ExtractClips(AnimationClip[] clips)
        {
            // Note, some GLB's just don't have an animation that ends with _Avatar, because of course they bloody don't.
            // Even though conventions say they should: https://docs.decentraland.org/creator/emotes/props-and-sounds/#naming-conventions
            // Like this one: urn:decentraland:matic:collections-v2:0xb5e24ada4096b86ce3cf7af5119f19ed6089a80b:0
            // Conventions are often not followed, so we try to figure out which clip is which
            
            // Conventions (if it's just one clip, it's the avatar clip)
            var avatarClip = clips.Length == 1 ? clips[0] : clips.FirstOrDefault(c => c.name.EndsWith("_Avatar", StringComparison.OrdinalIgnoreCase));
            var propClip = clips.Length == 1 ?
                null :
                clips.FirstOrDefault(c => c.name.EndsWith("_Prop", StringComparison.OrdinalIgnoreCase));

            // If the prop clip was found but the avatar clip was not, we can infer that the avatar clip is
            // the one that isn't the prop clip
            if (avatarClip == null && propClip != null)
            {
                avatarClip = clips.FirstOrDefault(c => c != propClip);
            }
            
            // If the avatar clip was found, but the prop clip wasn't, we can infer that the prop clip is
            // the one that isn't the avatar clip
            if (avatarClip != null && propClip == null && clips.Length == 2)
            {
                propClip = clips.FirstOrDefault(c => c != avatarClip);
            }

            // Last resort: no clip follows the _Avatar/_Prop convention at all (e.g. emotes exported
            // with default Blender clip names like "Base Layer" / "Base Layer.001"). Fall back to
            // positional assignment - first clip is the avatar, second (if exactly two) is the prop -
            // rather than throwing and taking down the whole avatar load.
            if (avatarClip == null && clips.Length >= 1)
            {
                avatarClip = clips[0];
                if (propClip == null && clips.Length == 2)
                    propClip = clips[1];
            }

            if (avatarClip == null) throw new InvalidOperationException("Failed to determine animation clip assignment from names: " + clips.Select(c => c.name).Aggregate((a, b) => a + ", " + b));
            
            return (avatarClip, propClip);
        } 
        
        /// <summary>
        /// Examples of urns that need this:
        ///
        /// urn:decentraland:matic:collections-v2:0xee8ae4c668edd43b34b98934d6d2ff82e41e6488:0
        /// urn:decentraland:matic:collections-v2:0xee8ae4c668edd43b34b98934d6d2ff82e41e6488:5
        /// </summary>
        private static void Sanitize(Transform root)
        {
            // If the wearable has 2 root objects they get placed under a Scene object, and we don't want that
            if (root.childCount == 1 && root.GetChild(0).name == "Scene")
            {
                var sceneChild = root.GetChild(0);

                while (sceneChild.childCount > 0)
                {
                    var child = sceneChild.GetChild(0);
                    child.SetParent(root, true);
                }

                // DestroyImmediate is required outside play mode (editor previews); Destroy would error
                if (Application.isPlaying)
                    Object.Destroy(sceneChild.gameObject);
                else
                    Object.DestroyImmediate(sceneChild.gameObject);
            }

        }
    }
}
