using Arch.Core;
using CRDT;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput.UiSimulation;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Lists the scene entities whose renderers fall inside the main camera's frustum, with the screen rectangle
    ///     their bounds project to — frustum and bounds only, so nothing here says whether something else is in front.
    /// </summary>
    public class GetVisibleEntitiesTool : McpTool
    {
        private const int DEFAULT_LIMIT = 25;
        private const int MAX_LIMIT = 200;
        private const int FRUSTUM_PLANE_COUNT = 6;

        private static readonly QueryDescription PRIMITIVES = new QueryDescription().WithAll<PrimitiveMeshRendererComponent>().WithNone<DeleteEntityIntention>();
        private static readonly QueryDescription GLTF_CONTAINERS = new QueryDescription().WithAll<GltfContainerComponent>().WithNone<DeleteEntityIntention>();

        private readonly IScenesCache scenesCache;
        private readonly World globalWorld;
        private readonly Entity playerEntity;

        // Scratch space for one call; the tool runs on the main thread and never concurrently with itself.
        private readonly Plane[] frustumPlanes = new Plane[FRUSTUM_PLANE_COUNT];
        private readonly List<VisibleEntry> candidates = new ();
        private readonly List<string> labelParts = new (5);

        public override string Name => "get_visible_entities";

        public override string Description =>
            "List the scene entities in the camera's view right now, sorted by how much of the screen their renderer bounds "
            + "cover: a one-call check that a thing is in frame (or that something fills it) without reading a screenshot. "
            + "Occlusion is NOT evaluated — frustum and bounds only, so an entity behind a wall is still listed, and a bounds "
            + "box is larger than the mesh it wraps. screenRect is in the client's full-resolution screen pixels, origin "
            + "top-left, with the screen size as screen — normalize against screen, never against a downscaled screenshot; "
            + "center is the normalized point ready for ui_drag (click_at rays into the world and cannot address UI). Avatars "
            + "are not included; only primitive meshes and loaded GLTF containers of the current scene.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Integer("limit", "Maximum entities to return, largest coverage first. Default 25, max 200.")
                  .Number("minCoverage", "Smallest screen-area fraction (0..1) an entity must cover to be listed. Default 0.");

        public override JObject OutputSchema =>
            McpJsonSchema.Object()
                          .Object("screen", McpJsonSchema.Object().Integer("width").Integer("height"), "The screen size every screenRect is in.")
                          .Object("cameraPosition", JObjectExtensions.VectorSchema())
                          .Object("cameraRotationEuler", JObjectExtensions.VectorSchema())
                          .Integer("candidates", "Scene entities with a renderer to test.")
                          .Integer("inFrustum", "Candidates whose bounds intersect the camera frustum.")
                          .Integer("returned")
                          .Boolean("truncated")
                          .ObjectArray("entities", McpJsonSchema.Object()
                                                                 .Integer("id", "Engine entity id, for get_entity_details.")
                                                                 .Integer("crdtEntity", "CRDT id the scene code uses; null for engine-only entities.", nullable: true)
                                                                 .String("label", "What the entity shows; null when nothing describes it.", nullable: true)
                                                                 .Object("screenRect", McpJsonSchema.Object().Number("x").Number("y").Number("width").Number("height"),
                                                                      "Screen pixels, origin top-left, clamped to the screen.")
                                                                 .Object("center", McpJsonSchema.Object().Number("x").Number("y"), "Normalized (0..1) center of screenRect.")
                                                                 .Number("coverage", "Fraction of the screen area screenRect covers.")
                                                                 .Number("distance", "Meters from the player to the bounds center; null without a player transform.", nullable: true)
                                                                 .Number("distanceToCamera", "Meters from the camera to the bounds center.")
                                                                 .Integer("renderers", "Renderers whose bounds were merged."),
                              "In-frustum entities sorted by coverage, largest first.")
                          .Build();

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public GetVisibleEntitiesTool(IScenesCache scenesCache, World globalWorld, Entity playerEntity)
        {
            this.scenesCache = scenesCache;
            this.globalWorld = globalWorld;
            this.playerEntity = playerEntity;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            int limit = Mathf.Clamp(arguments.GetInt("limit", DEFAULT_LIMIT), 1, MAX_LIMIT);
            float minCoverage = Mathf.Clamp01(arguments.GetFloat("minCoverage", 0f));

            ISceneFacade? scene = scenesCache.CurrentScene.Value;

            if (scene == null)
                return UniTask.FromResult(McpToolResult.Error("No scene world found at the current parcel."));

            SingleInstanceEntity cameraEntity = globalWorld.CacheCamera();
            Camera camera = cameraEntity.GetCameraComponent(globalWorld).Camera;

            if (camera == null)
                return UniTask.FromResult(McpToolResult.Error("The camera entity has no Unity camera yet."));

            Vector3? playerPosition = globalWorld.TryGet(playerEntity, out CharacterTransform characterTransform) ? characterTransform.Position : null;
            Vector3 cameraPosition = camera.transform.position;

            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
            candidates.Clear();

            World sceneWorld = scene.EcsExecutor.World;
            var screenSize = new Vector2(Screen.width, Screen.height);
            var candidateCount = 0;
            var inFrustum = 0;

            sceneWorld.Query(in PRIMITIVES, (Entity entity, ref PrimitiveMeshRendererComponent component) =>
            {
                MeshRenderer renderer = component.MeshRenderer;

                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    return;

                candidateCount++;
                Consider(sceneWorld, entity, camera, renderer.bounds, rendererCount: 1, in screenSize, cameraPosition, playerPosition, ref inFrustum);
            });

            sceneWorld.Query(in GLTF_CONTAINERS, (Entity entity, ref GltfContainerComponent component) =>
            {
                if (component.State != LoadingState.Finished)
                    return;

                StreamableLoadingResult<GltfContainerAsset>? result = component.Promise.Result;

                if (result is not { Succeeded: true })
                    return;

                GltfContainerAsset? asset = result.Value.Asset;

                if (asset == null || !TryMergeBounds(asset.Renderers, out Bounds bounds, out int rendererCount))
                    return;

                candidateCount++;
                Consider(sceneWorld, entity, camera, bounds, rendererCount, in screenSize, cameraPosition, playerPosition, ref inFrustum);
            });

            candidates.Sort(static (a, b) => b.Coverage.CompareTo(a.Coverage));

            var matched = 0;

            for (var i = 0; i < candidates.Count; i++)
                if (candidates[i].Coverage >= minCoverage)
                    matched++;

            int returned = Mathf.Min(limit, matched);
            bool truncated = returned < matched;

            var entries = new JArray();
            var output = new StringBuilder();
            output.Append("candidates=").Append(candidateCount).Append(" inFrustum=").Append(inFrustum).Append(" returned=").Append(returned).AppendLine();

            var written = 0;

            for (var i = 0; i < candidates.Count && written < returned; i++)
            {
                VisibleEntry entry = candidates[i];

                if (entry.Coverage < minCoverage)
                    continue;

                written++;

                entries.Add(new JObject
                {
                    ["id"] = entry.Id,
                    ["crdtEntity"] = entry.CrdtEntity.HasValue ? new JValue(entry.CrdtEntity.Value) : JValue.CreateNull(),
                    ["label"] = entry.Label != null ? new JValue(entry.Label) : JValue.CreateNull(),
                    ["screenRect"] = UiDiscovery.RectJson(entry.ScreenRect),
                    ["center"] = UiDiscovery.CenterJson(entry.ScreenRect),
                    ["coverage"] = Math.Round(entry.Coverage, 4),
                    ["distance"] = entry.Distance.HasValue ? new JValue(Math.Round(entry.Distance.Value, 2)) : JValue.CreateNull(),
                    ["distanceToCamera"] = Math.Round(entry.DistanceToCamera, 2),
                    ["renderers"] = entry.Renderers,
                });

                output.Append(entry.Id);

                if (entry.CrdtEntity.HasValue)
                    output.Append(" crdt=").Append(entry.CrdtEntity.Value);

                output.Append(' ').Append(Mathf.RoundToInt(entry.Coverage * 100f)).Append('%');

                if (entry.Label != null)
                    output.Append(" (").Append(entry.Label).Append(')');

                output.AppendLine();
            }

            if (truncated)
                output.Append(returned).Append(" of ").Append(matched).Append(" shown; raise limit (max ").Append(MAX_LIMIT).Append(") or raise minCoverage.");

            var structured = new JObject
            {
                ["screen"] = UiDiscovery.ScreenJson(),
                ["cameraPosition"] = cameraPosition.ToVector(),
                ["cameraRotationEuler"] = camera.transform.rotation.eulerAngles.ToVector(),
                ["candidates"] = candidateCount,
                ["inFrustum"] = inFrustum,
                ["returned"] = returned,
                ["truncated"] = truncated,
                ["entities"] = entries,
            };

            candidates.Clear();

            return UniTask.FromResult(McpToolResult.TextWithStructured(output.ToString(), structured));
        }

        private void Consider(World sceneWorld, Entity entity, Camera camera, Bounds bounds, int rendererCount, in Vector2 screenSize,
            Vector3 cameraPosition, Vector3? playerPosition, ref int inFrustum)
        {
            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds))
                return;

            inFrustum++;

            if (!ScreenBoundsProjection.TryProjectToViewport(camera, in bounds, out Rect viewport))
                return;

            float xMin = Mathf.Clamp01(viewport.xMin);
            float xMax = Mathf.Clamp01(viewport.xMax);
            float yMin = Mathf.Clamp01(viewport.yMin);
            float yMax = Mathf.Clamp01(viewport.yMax);

            if (xMax <= xMin || yMax <= yMin)
                return;

            // Viewport y grows upward; image y grows downward from the top edge.
            var screenRect = new Rect(xMin * screenSize.x, (1f - yMax) * screenSize.y, (xMax - xMin) * screenSize.x, (yMax - yMin) * screenSize.y);
            float coverage = (xMax - xMin) * (yMax - yMin);

            int? crdtEntity = sceneWorld.TryGet(entity, out CRDTEntity crdt) ? crdt.Id : null;
            string? label = SceneEntityLabel.Describe(sceneWorld, entity, labelParts);

            candidates.Add(new VisibleEntry(entity.Id, crdtEntity, label, screenRect, coverage,
                playerPosition.HasValue ? Vector3.Distance(playerPosition.Value, bounds.center) : null,
                Vector3.Distance(cameraPosition, bounds.center), rendererCount));
        }

        private static bool TryMergeBounds(List<Renderer> renderers, out Bounds bounds, out int count)
        {
            bounds = default(Bounds);
            count = 0;

            for (var i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (count == 0)
                    bounds = renderer.bounds;
                else
                    bounds.Encapsulate(renderer.bounds);

                count++;
            }

            return count > 0;
        }

        private readonly struct VisibleEntry
        {
            public readonly int Id;
            public readonly int? CrdtEntity;
            public readonly string? Label;
            public readonly Rect ScreenRect;
            public readonly float Coverage;
            public readonly float? Distance;
            public readonly float DistanceToCamera;
            public readonly int Renderers;

            public VisibleEntry(int id, int? crdtEntity, string? label, Rect screenRect, float coverage, float? distance, float distanceToCamera, int renderers)
            {
                Id = id;
                CrdtEntity = crdtEntity;
                Label = label;
                ScreenRect = screenRect;
                Coverage = coverage;
                Distance = distance;
                DistanceToCamera = distanceToCamera;
                Renderers = renderers;
            }
        }
    }
}
