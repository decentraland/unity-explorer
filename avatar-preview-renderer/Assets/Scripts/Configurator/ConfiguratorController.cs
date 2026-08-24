using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using Loading;
using Runtime.Wearables;
using Services;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.VFX;
using Utils;
using Random = UnityEngine.Random;

namespace Configurator
{
    [DefaultExecutionOrder(10)]
    public class ConfiguratorController : MonoBehaviour
    {
        [SerializeField] private ConfiguratorUIPresenter uiPresenter;
        [SerializeField] private AvatarLoader avatarLoader;

        [SerializeField] private DragRotator dragRotator;

        [SerializeField] private GameObject platform;
        [SerializeField] private VisualEffect confirmationVFX;

        [SerializeField] private List<CategoryDefinition> faceCategories;
        [SerializeField] private List<CategoryDefinition> bodyCategories;

        [Header("Presets")] [SerializeField] private PresetDefinition[] avatarPresets;
        [SerializeField] private Color[] skinColorPresets;
        [SerializeField] private Color[] hairColorPresets;
        [SerializeField] private Color[] eyeColorPresets;

        private BodyShape _bodyShape;
        private readonly Dictionary<string, EntityDefinition> _selectedItems = new();
        private Color _skinColor;
        private Color _hairColor;
        private Color _eyeColor;
        private EntityDefinition _emoteToLoad;

        private void Start()
        {
            uiPresenter.CategoryChanged += OnCategoryChanged;
            uiPresenter.BodyShapeSelected += OnBodyShapeSelected;
            uiPresenter.WearableSelected += OnWearableSelected;
            uiPresenter.PresetSelected += OnPresetSelected;
            uiPresenter.SkinColorSelected += OnSkinColorSelected;
            uiPresenter.HairColorSelected += OnHairColorSelected;
            uiPresenter.EyeColorSelected += OnEyeColorSelected;
            uiPresenter.CharacterAreaDrag += dragRotator.OnDrag;
            uiPresenter.Confirmed += OnConfirmed;
            uiPresenter.JumpIn += OnJumpIn;
            uiPresenter.AvatarCustomizationStepChanged += OnAvatarCustomizationStepChanged;

            StartCoroutine(InitialLoad());
        }

        private void OnEyeColorSelected(Color color)
        {
            _eyeColor = color;

            StartCoroutine(ReloadPreview());
        }

        private void OnHairColorSelected(Color color)
        {
            _hairColor = color;

            StartCoroutine(ReloadPreview());
        }

        private void OnConfirmed(bool open)
        {
            if (open)
            {
                _emoteToLoad = EntityDefinition.FromEmbeddedEmote("character/Particles_Anim", true);
                StartCoroutine(ReloadPreview());
                
                confirmationVFX.Play();
                dragRotator.LookAtCamera(true);
            }
            else
            {
                _emoteToLoad = null;
                StartCoroutine(ReloadPreview());

                confirmationVFX.Stop();
            }
            
            platform.SetActive(!open);
        }

        private static void OnAvatarCustomizationStepChanged(int step)
        {
            JSBridge.NativeCalls.OnAvatarCustomizationStep(step);
        }

        private void OnJumpIn(bool skipped)
        {
            JSBridge.NativeCalls.OnCustomizationDone(JsonUtility.ToJson(new AvatarCustomizationConfig(
                _bodyShape == BodyShape.Male
                    ? WearablesConstants.BODY_SHAPE_MALE
                    : WearablesConstants.BODY_SHAPE_FEMALE,
                _eyeColor,
                _skinColor,
                _hairColor,
                _selectedItems.Values.Select(ed => ed.URN).ToList(),
                skipped
            )));
        }

        private void OnSkinColorSelected(Color color)
        {
            _skinColor = color;

            StartCoroutine(ReloadPreview());
        }

        private void OnWearableSelected(string category, EntityDefinition wearable)
        {
            if (wearable == null)
            {
                _selectedItems.Remove(category);
            }
            else
            {
                _selectedItems[category] = wearable;

                _emoteToLoad = GetEmote(category);
            }

            uiPresenter.ClearPresetSelection();

            StartCoroutine(ReloadPreview());
        }

        private void OnCategoryChanged(string category)
        {
            avatarLoader.TryHideCategory(WearableCategories.Categories.EYEWEAR,
                category is WearableCategories.Categories.EYES or WearableCategories.Categories.EYEBROWS);
        }

        private static EntityDefinition GetEmote(string category)
        {
            return EntityDefinition.FromEmbeddedEmote(category switch
            {
                "facial_hair" or "earring" or "hair" or "eyes" or "eyebrows" or "mouth" or "eyewear" =>
                    $"character/Accessories_v0{Random.Range(1, 4)}",
                "upper_body" => $"character/Outfit_Upper_v0{Random.Range(1, 4)}",
                "lower_body" => $"character/Outfit_Lower_v0{Random.Range(1, 4)}",
                "feet" => $"character/Outfit_Shoes_v0{Random.Range(1, 3)}",
                "hands_wear" => $"character/Outfit_Hand_v0{Random.Range(1, 3)}",
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            }, false);
        }

        private static EntityDefinition GetEmote(BodyShape bodyShape)
        {
            return EntityDefinition.FromEmbeddedEmote(bodyShape switch
            {
                BodyShape.Male => "character/Wave_Male",
                BodyShape.Female => "character/Wave_Female",
                _ => throw new ArgumentOutOfRangeException(nameof(bodyShape), bodyShape, null)
            }, false);
        }

        private void OnBodyShapeSelected(BodyShape bodyShape)
        {
            _bodyShape = bodyShape;
            uiPresenter.ClearPresetSelection();

            _emoteToLoad = GetEmote(bodyShape);

            StartCoroutine(ReloadPreview());
        }

        private void OnPresetSelected(PresetDefinition preset)
        {
            SetPreset(preset);

            StartCoroutine(ReloadPreview());
        }

        private void SetPreset(PresetDefinition preset)
        {
            _selectedItems.Clear();

            _bodyShape = preset.bodyShape;
            _skinColor = preset.skinColor;
            _hairColor = preset.hairColor;
            _eyeColor = preset.eyeColor;

            _emoteToLoad = GetEmote(_bodyShape);

            foreach (var urn in preset.urns)
            {
                var wearable = EntityService.GetCachedEntity(urn);
                _selectedItems[wearable.Category] = wearable;
            }

            uiPresenter.SetBodyShape(_bodyShape);
            uiPresenter.SetSelectedItems(_selectedItems);
            uiPresenter.SetColors(_skinColor, _hairColor, _eyeColor);
        }

        private async Awaitable ReloadPreview()
        {
            Debug.Log("Reloading preview...");
            await avatarLoader.LoadAvatar(_bodyShape, _selectedItems.Values, _emoteToLoad, Array.Empty<string>(),
                new AvatarColors(_eyeColor, _hairColor, _skinColor));
        }

        private async Awaitable InitialLoad()
        {
            Debug.Log("Initial loading...");

            var allUrns = faceCategories.Union(bodyCategories)
                .SelectMany(c => c.urns).Where(urn => !string.IsNullOrEmpty(urn))
                .Select(URNUtils.SanitizeURN)
                .Append(WearablesConstants.BODY_SHAPE_MALE.ToLowerInvariant())
                .Append(WearablesConstants.BODY_SHAPE_FEMALE.ToLowerInvariant())
                .ToArray();
            var collectionEntities = await EntityService.GetEntities(allUrns);

            var entityDict = collectionEntities
                .ToDictionary(ae => ae.URN, ae => ae);

            // Update category definitions
            foreach (var cd in faceCategories.Union(bodyCategories))
            {
                foreach (var urn in cd.urns)
                {
                    cd.Definitions.Add(string.IsNullOrEmpty(urn) ? null : entityDict[urn]);
                }
            }

            var randomPresetIndex = Random.Range(0, avatarPresets.Length);

            // Preload thumbnails
            RemoteTextureService.Instance.Pause(true);
            foreach (var preset in avatarPresets.Select(p => p.thumbnail))
            {
                RemoteTextureService.Instance.PreloadTexture(preset);
            }

            foreach (var ed in collectionEntities)
            {
                RemoteTextureService.Instance.PreloadTexture(ed.Thumbnail);
            }

            // Set data
            uiPresenter.SetColorPresets(skinColorPresets, hairColorPresets, eyeColorPresets);
            uiPresenter.SetCollection(faceCategories, bodyCategories);
            uiPresenter.SetAvatarPresets(avatarPresets, randomPresetIndex);
            RemoteTextureService.Instance.Pause(false);

            // Load initial preset
            SetPreset(avatarPresets[randomPresetIndex]);
            await ReloadPreview();
            
            // Preload VFX hack
            // TODO: Uncomment this if we put back the Jump In screen
            // confirmationVFX.Play();
            // await Awaitable.NextFrameAsync();
            // confirmationVFX.Stop();

            JSBridge.NativeCalls.OnLoadComplete();

            // Wait for the username
            Debug.Log("Waiting for username...");
            while (string.IsNullOrEmpty(AangConfiguration.Instance.Username))
            {
                await Awaitable.NextFrameAsync();
            }

            Debug.Log($"Username received: {AangConfiguration.Instance.Username}");

            uiPresenter.SetUsername(AangConfiguration.Instance.Username);

            platform.SetActive(true);
            uiPresenter.LoadCompleted();

            if (AangConfiguration.Instance.UseBrowserPreload)
            {
                EntityService.PreloadCachedEntityAssets();
                JSBridge.NativeCalls.PreloadURLs(
                    string.Join(',', Application.streamingAssetsPath + "/character/Accessories_v01.glb",
                        Application.streamingAssetsPath + "/character/Accessories_v02.glb",
                        Application.streamingAssetsPath + "/character/Accessories_v03.glb",
                        Application.streamingAssetsPath + "/character/Outfit_Lower_v01.glb",
                        Application.streamingAssetsPath + "/character/Outfit_Lower_v02.glb",
                        Application.streamingAssetsPath + "/character/Outfit_Lower_v03.glb",
                        Application.streamingAssetsPath + "/character/Outfit_Shoes_v01.glb",
                        Application.streamingAssetsPath + "/character/Outfit_Shoes_v02.glb",
                        Application.streamingAssetsPath + "/character/Outfit_Upper_v01.glb",
                        Application.streamingAssetsPath + "/character/Outfit_Upper_v02.glb",
                        Application.streamingAssetsPath + "/character/Outfit_Upper_v03.glb",
                        Application.streamingAssetsPath + "/character/Outfit_Hand_v01.glb",
                        Application.streamingAssetsPath + "/character/Outfit_Hand_v02.glb",
                        Application.streamingAssetsPath + "/character/Wave_Female.glb",
                        Application.streamingAssetsPath + "/character/Wave_Male.glb",
                        Application.streamingAssetsPath + "/character/Particles_Anim.glb"));
            }
        }
    }

    [Serializable]
    public class CategoryDefinition
    {
        public string id;
        public string title;

        [FormerlySerializedAs("defaultThumbnail")]
        public Texture2D emptyThumbnail;

        public string[] urns;

        public List<EntityDefinition> Definitions { get; private set; } = new(20);
    }

    [Serializable]
    public class PresetDefinition
    {
        public BodyShape bodyShape;
        public Color skinColor;
        public Color hairColor;
        public Color eyeColor;
        public string thumbnail;
        public string[] urns;
    }
}