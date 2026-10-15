using Arch.Core;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Special;
using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using System.Collections.Generic;

namespace DCL.McpServer.Utils
{
    /// <summary>
    ///     Short description of a scene entity for tool output, so an agent can tell entities apart without a
    ///     get_entity_details call per id.
    /// </summary>
    public static class SceneEntityLabel
    {
        private const int MAX_TEXT_LENGTH = 24;

        /// <summary>
        ///     The scene container is created with a default <see cref="CRDT.CRDTEntity" />, so it carries the scene root's CRDT id 0
        ///     without being a CRDT entity; only the real root has <see cref="SceneRootComponent" />.
        /// </summary>
        public static bool IsSceneContainer(World world, Entity entity, int crdtEntityId) =>
            crdtEntityId == SpecialEntitiesID.SCENE_ROOT_ENTITY && !world.Has<SceneRootComponent>(entity);

        /// <summary>
        ///     Names the SDK reserved entities by CRDT id; null for any other id.
        /// </summary>
        public static string? Reserved(int crdtEntityId) =>
            crdtEntityId switch
            {
                SpecialEntitiesID.SCENE_ROOT_ENTITY => "scene root",
                SpecialEntitiesID.PLAYER_ENTITY => "player",
                SpecialEntitiesID.CAMERA_ENTITY => "camera",
                _ => null,
            };

        /// <summary>
        ///     What the entity shows: gltf src, primitive mesh, text, hidden, parent CRDT id. Null when none of them applies.
        ///     <paramref name="parts" /> is scratch space, cleared on every call.
        /// </summary>
        public static string? Describe(World world, Entity entity, List<string> parts)
        {
            parts.Clear();

            if (world.TryGet(entity, out PBGltfContainer? gltf) && gltf != null)
                parts.Add($"gltf {gltf.Src}");

            if (world.TryGet(entity, out PBMeshRenderer? mesh) && mesh != null)
                parts.Add($"mesh {mesh.MeshCase}");

            if (world.TryGet(entity, out PBTextShape? textShape) && textShape != null)
            {
                string text = textShape.Text.Length > MAX_TEXT_LENGTH ? textShape.Text.Substring(0, MAX_TEXT_LENGTH) + "…" : textShape.Text;
                parts.Add($"text \"{text}\"");
            }

            if (world.TryGet(entity, out PBVisibilityComponent? visibility) && visibility is { HasVisible: true, Visible: false })
                parts.Add("hidden");

            if (world.TryGet(entity, out SDKTransform? transform) && transform != null && transform.ParentId.Id != SpecialEntitiesID.SCENE_ROOT_ENTITY)
                parts.Add($"parent {transform.ParentId.Id}");

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }
    }
}
