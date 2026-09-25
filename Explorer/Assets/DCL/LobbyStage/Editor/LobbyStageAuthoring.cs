using Cinemachine;
using DCL.CharacterPreview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Lobby.Editor
{
    /// <summary>
    ///     Editor-only stage authoring: opens an unsaved scene with the real character preview rig, the stage prefab and an
    ///     avatar-sized mannequin, framed by the Lobby camera preset, so presets, props and lighting can be tuned without running
    ///     the application. While that scene is open, the preview camera is kept on the preset every editor tick, in edit mode
    ///     and in Play mode, and the stage fits itself to it as it renders.
    /// </summary>
    public static class LobbyStageAuthoring
    {
        private const string CONTAINER_PREFAB = "Assets/DCL/Character/CharacterPreview/Assets/CharacterPreviewContainer.prefab";
        private const string LOBBY_CAMERA_SETTINGS = "Assets/DCL/Character/CharacterPreview/Assets/CharacterPreviewCameraSettings_Lobby.asset";
        private const string STAGE_PREFAB = "Assets/DCL/LobbyStage/Prefabs/LobbyStage.prefab";
        private const string BASE_BODY_MESH = "Assets/DCL/AvatarRendering/AvatarShape/Assets/Avatar_Male_Mesh.asset";

        private const string CONTAINER_NAME = "CharacterPreviewContainer (authoring)";
        private const string MANNEQUIN_NAME = "AvatarMannequin (base body, bind pose)";
        private const int PREVIEW_LAYER = 31;
        private const float FAR_CLIP = 100f;

        // The runtime avatar spawns under the container's AvatarParent, one unit below the container origin
        private const float FEET_Y = -1f;

        // The camera preset fields are internal to the character preview assembly, so they are read through serialization
        private const string PRESETS_PATH = "<cameraSettings>k__BackingField.<cameraPositions>k__BackingField";
        private const string VERTICAL_POSITION_PATH = "<verticalPosition>k__BackingField";
        private const string FOV_PATH = "<cameraFieldOfView>k__BackingField";

        private static Camera? previewCamera;
        private static CinemachineFreeLook? freeLook;
        private static Transform? cameraTarget;
        private static LobbyStage? stage;
        private static Vector3 presetVerticalPosition;
        private static float presetFieldOfView;

        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorApplication.hierarchyChanged += Resolve;
            EditorApplication.update += Tick;
            Resolve();
        }

        [MenuItem("Decentraland/Lobby Stage/Open Authoring Scene")]
        private static void OpenAuthoringScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var containerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CONTAINER_PREFAB);
            var settings = AssetDatabase.LoadAssetAtPath<CharacterPreviewSettingsSO>(LOBBY_CAMERA_SETTINGS);
            var stagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(STAGE_PREFAB);
            var baseBodyMesh = AssetDatabase.LoadAssetAtPath<Mesh>(BASE_BODY_MESH);

            if (containerPrefab == null || settings == null || stagePrefab == null || baseBodyMesh == null)
            {
                Debug.LogError($"Lobby stage authoring: missing asset. Container: {containerPrefab != null}, settings: {settings != null}, stage: {stagePrefab != null}, body mesh: {baseBodyMesh != null}");
                return;
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var container = (GameObject)PrefabUtility.InstantiatePrefab(containerPrefab);
            container.name = CONTAINER_NAME;

            // The prefab camera targets a RenderTexture asset whose format is unsupported on Windows; the Game view is the target here
            container.GetComponentInChildren<Camera>(true).targetTexture = null;

            var avatarContainer = container.GetComponent<CharacterPreviewAvatarContainer>();

            // Mirrors the lobby: the stage provides the platform and the light
            avatarContainer.SetPreviewPlatformActive(false);
            avatarContainer.SetLightActive(false);

            // Its Update eases the FOV towards a target only the runtime flow sets; disabled, the lens stays where Tick puts it
            avatarContainer.enabled = false;

            var stageObject = (GameObject)PrefabUtility.InstantiatePrefab(stagePrefab);

            if (!stageObject.TryGetComponent(out LobbyStage stageInstance))
            {
                Debug.LogError("Lobby stage authoring: the stage prefab root has no LobbyStage component");
                return;
            }

            // Drawn without skinning the base body shows its bind pose: a grey mannequin with the real proportions, feet at the origin
            var mannequin = new GameObject(MANNEQUIN_NAME);
            mannequin.layer = PREVIEW_LAYER;
            mannequin.transform.position = new Vector3(0f, FEET_Y, 0f);
            mannequin.AddComponent<MeshFilter>().sharedMesh = baseBodyMesh;
            mannequin.AddComponent<MeshRenderer>().sharedMaterial = GraphicsSettings.currentRenderPipeline.defaultMaterial;

            Resolve();
            Selection.activeGameObject = stageInstance.gameObject;
        }

        // Re-resolved on every hierarchy change so the scene survives domain reloads and Play mode transitions
        private static void Resolve()
        {
            previewCamera = null;

            GameObject? container = GameObject.Find(CONTAINER_NAME);
            if (container == null) return;

            var settings = AssetDatabase.LoadAssetAtPath<CharacterPreviewSettingsSO>(LOBBY_CAMERA_SETTINGS);
            stage = Object.FindAnyObjectByType<LobbyStage>();
            freeLook = container.GetComponentInChildren<CinemachineFreeLook>(true);
            if (settings == null || stage == null || freeLook == null) return;

            cameraTarget = freeLook.LookAt;

            var serialized = new SerializedObject(settings);
            SerializedProperty preset = serialized.FindProperty(PRESETS_PATH).GetArrayElementAtIndex(0);
            presetVerticalPosition = preset.FindPropertyRelative(VERTICAL_POSITION_PATH).vector3Value;
            presetFieldOfView = preset.FindPropertyRelative(FOV_PATH).floatValue;

            previewCamera = container.GetComponentInChildren<Camera>(true);
            stage.Track(previewCamera);
        }

        private static void Tick()
        {
            if (previewCamera == null || freeLook == null || cameraTarget == null || stage == null) return;

            previewCamera.targetTexture = null;
            cameraTarget.localPosition = presetVerticalPosition;
            freeLook.m_Lens.FieldOfView = presetFieldOfView;
            freeLook.m_Lens.FarClipPlane = FAR_CLIP;
        }
    }
}
