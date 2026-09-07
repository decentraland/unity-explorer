using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace StylizedGrass
{
    /// <summary>
    ///     Bakes the world-space ground-colour map sampled by the StylizedGrass shader (and any
    ///     terrain material that tints against the same map). For every terrain in
    ///     <see cref="terrainObjects" /> and every splat-described ground in
    ///     <see cref="splatSources" /> it renders the splat-blended albedo, top-down, into the
    ///     sub-rectangle of a RenderTexture that the ground occupies within
    ///     <see cref="GrassColorMap.bounds" />, then reads the result back into the color map's
    ///     texture and publishes it to the <c>_ColorMap</c> / <c>_ColorMapBounds</c> shader globals.
    ///     Grass blades therefore pick up the underlying ground palette (dirt patches, paths, sand)
    ///     instead of rendering a uniform green.
    /// </summary>
    [DisallowMultipleComponent]
    public class GrassColorMapRenderer : MonoBehaviour
    {
        private const int PASS_SPLAT = 0;
        private const int PASS_RAW = 1;
        private const int PASS_ADD = 0;
        private const int PASS_MASK = 0;
        private const int MIN_RESOLUTION = 64;
        private const int MAX_RESOLUTION = 4096;
        private const int SPLATS_PER_MAP = GrassColorMapSplatSource.MAX_LAYERS;

        private static readonly Color NEUTRAL_GROUND = new (0.32f, 0.30f, 0.24f, 0f);
        private static readonly Vector4 UNTILED_ST = new (1f, 1f, 0f, 0f);

        private static readonly int CONTROL_ID = Shader.PropertyToID("_Control");
        private static readonly int CONTROL_ST_ID = Shader.PropertyToID("_Control_ST");
        private static readonly int CONTROL_TEXEL_SIZE_ID = Shader.PropertyToID("_Control_TexelSize");
        private static readonly int LAYER_MASK_ID = Shader.PropertyToID("_LayerMask");
        private static readonly int MAIN_TEX_ID = Shader.PropertyToID("_MainTex");
        private static readonly int TERRAIN_SIZE_ID = Shader.PropertyToID("_TerrainSize");

        private static readonly int[] SPLAT_ID =
        {
            Shader.PropertyToID("_Splat0"), Shader.PropertyToID("_Splat1"),
            Shader.PropertyToID("_Splat2"), Shader.PropertyToID("_Splat3"),
        };

        private static readonly int[] SPLAT_ST_ID =
        {
            Shader.PropertyToID("_Splat0_ST"), Shader.PropertyToID("_Splat1_ST"),
            Shader.PropertyToID("_Splat2_ST"), Shader.PropertyToID("_Splat3_ST"),
        };

        // Consumer- and prefab-bound surface.
        public GrassColorMap colorMap;
        public int resolution = 1024;
        public List<GameObject> terrainObjects = new ();

        [Tooltip("Grounds that exist as splat data only (no Terrain or Renderer), baked alongside the terrain objects.")]
        public List<GrassColorMapSplatSource> splatSources = new ();

        // Prefab-authored configuration (retained for round-trip). The fields with runtime meaning
        // are honoured by the bake below: cullingMask filters which objects are baked and
        // useOriginalMaterials selects the bake path. resIdx and textureDetail are the editor
        // resolution/quality dropdown indices whose resolved pixel size is carried by 'resolution',
        // and layerScaleSettings holds the per-terrain-layer grass-scale authoring values.
        public int resIdx;
        public int textureDetail;
        public List<float> layerScaleSettings = new ();
        [SerializeField] private LayerMask cullingMask = ~0;
        [SerializeField] private bool useOriginalMaterials;

        // Bake shaders, wired on GrassRenderer.prefab by GUID.
        [SerializeField] private Shader terrainAlbedoShader;
        [SerializeField] private Shader terrainAlbedoAddPassShader;
        [SerializeField] private Shader terrainSplatMaskShader;

        // The texture this component created and handed to the color map, tracked so it can be
        // released with the component instead of outliving it once per realm switch.
        private Texture2D bakedTexture;

        /// <summary>
        ///     Encapsulate every baked ground (Unity Terrains by their terrain-data size, splat
        ///     sources by their authored region, everything else by its renderer bounds) into
        ///     <see cref="GrassColorMap.bounds" />.
        /// </summary>
        public void RecalculateBounds()
        {
            if (colorMap == null)
                return;

            var any = false;
            Bounds combined = default;

            if (terrainObjects != null)
            {
                foreach (GameObject go in terrainObjects)
                {
                    if (go == null)
                        continue;

                    if ((cullingMask.value & (1 << go.layer)) == 0)
                        continue;

                    if (!TryResolveBounds(go, out Bounds b))
                        continue;

                    Encapsulate(ref combined, ref any, b);
                }
            }

            if (splatSources != null)
            {
                foreach (GrassColorMapSplatSource source in splatSources)
                {
                    if (source == null || !HasArea(source.bounds))
                        continue;

                    Encapsulate(ref combined, ref any, source.bounds);
                }
            }

            if (any)
                colorMap.bounds = combined;
        }

        /// <summary>
        ///     Bake the color map for the current <see cref="terrainObjects" /> and
        ///     <see cref="splatSources" /> and publish it to the shader globals. Safe to call
        ///     repeatedly; re-baking releases the previous texture.
        /// </summary>
        public void Render()
        {
            if (colorMap == null)
                return;

            if (!HasArea(colorMap.bounds))
                RecalculateBounds();

            Bounds bounds = colorMap.bounds;
            var boundsMin = new Vector2(bounds.min.x, bounds.min.z);
            var boundsSize = new Vector2(bounds.size.x, bounds.size.z);

            if (boundsSize.x <= 0f || boundsSize.y <= 0f)
            {
                // Nothing to encapsulate: publish white + current bounds so grass renders untinted
                // rather than sampling a stale map.
                colorMap.SetActive();
                return;
            }

            int res = Mathf.Clamp(resolution, MIN_RESOLUTION, MAX_RESOLUTION);

            var desc = new RenderTextureDescriptor(res, res, RenderTextureFormat.ARGB32, 0)
            {
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = true,
            };

            RenderTexture rt = RenderTexture.GetTemporary(desc);
            RenderTexture prevActive = RenderTexture.active;

            Material albedoMat = null;
            Material addMat = null;
            Material maskMat = null;

            try
            {
                bool useShaders = terrainAlbedoShader != null && !useOriginalMaterials;

                if (useShaders)
                {
                    albedoMat = new Material(terrainAlbedoShader);

                    if (terrainAlbedoAddPassShader != null)
                        addMat = new Material(terrainAlbedoAddPassShader);

                    if (terrainSplatMaskShader != null)
                        maskMat = new Material(terrainSplatMaskShader);

                    BakeWithCommandBuffer(rt, res, boundsMin, boundsSize, albedoMat, addMat, maskMat);
                }
                else
                {
                    // No bake shaders wired, or the caller asked to use the terrains' own materials:
                    // composite each object's main texture into its sub-rectangle instead.
                    BakeWithBlits(rt, res, boundsMin, boundsSize);
                }

                var readback = new Texture2D(res, res, TextureFormat.RGBA32, false, false)
                {
                    name = "GrassColorMap.baked",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };

                RenderTexture.active = rt;
                readback.ReadPixels(new Rect(0, 0, res, res), 0, 0, false);

                // Upload and drop the CPU copy: nothing reads the baked map back, and at 2048² the
                // system-memory mirror is 16 MB held for the lifetime of the realm.
                readback.Apply(false, true);

                if (colorMap.texture != null)
                    SafeDestroy(colorMap.texture);

                bakedTexture = readback;
                colorMap.texture = readback;
                colorMap.SetActive();
            }
            finally
            {
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                SafeDestroy(albedoMat);
                SafeDestroy(addMat);
                SafeDestroy(maskMat);
            }
        }

        private void OnEnable()
        {
            // The renderer sits under its terrain root, so re-showing a terrain (a realm switch back
            // to it) re-publishes the bake that terrain owns without baking again.
            if (colorMap != null && colorMap.texture != null)
                colorMap.SetActive();
        }

        private void OnDestroy()
        {
            if (bakedTexture == null)
                return;

            // The color map is a plain data asset with no lifecycle of its own, so the texture this
            // component allocated would otherwise survive every realm switch that replaces it.
            if (colorMap != null && colorMap.texture == bakedTexture)
                colorMap.texture = null;

            SafeDestroy(bakedTexture);
            bakedTexture = null;

            // Only the bake the globals still point at gets reset to white; a newer bake that has
            // already taken over must not be clobbered by a stale renderer's deferred destruction.
            if (colorMap != null && colorMap.IsActive)
                colorMap.SetActive();
        }

        private void BakeWithCommandBuffer(RenderTexture rt, int res, Vector2 boundsMin, Vector2 boundsSize,
            Material albedoMat, Material addMat, Material maskMat)
        {
            var cmd = new CommandBuffer { name = "GrassColorMap Bake" };

            try
            {
                cmd.SetRenderTarget(rt);
                cmd.ClearRenderTarget(true, true, NEUTRAL_GROUND);

                foreach (GameObject go in terrainObjects)
                {
                    if (go == null)
                        continue;

                    if ((cullingMask.value & (1 << go.layer)) == 0)
                        continue;

                    if (!TryGetViewport(go, boundsMin, boundsSize, res, out Rect viewport))
                        continue;

                    cmd.SetViewport(viewport);

                    Terrain terrain = go.GetComponent<Terrain>();

                    if (terrain != null && terrain.terrainData != null)
                        DrawTerrain(cmd, terrain, albedoMat, addMat, maskMat);
                    else
                        DrawRaw(cmd, go, albedoMat, maskMat);
                }

                foreach (GrassColorMapSplatSource source in splatSources)
                {
                    if (source == null || source.control == null)
                        continue;

                    if (!TryGetViewport(source.bounds, boundsMin, boundsSize, res, out Rect viewport))
                        continue;

                    cmd.SetViewport(viewport);
                    DrawSplatSource(cmd, source, albedoMat, maskMat);
                }

                Graphics.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                cmd.Release();
            }
        }

        private static void DrawTerrain(CommandBuffer cmd, Terrain terrain, Material albedoMat, Material addMat, Material maskMat)
        {
            TerrainData data = terrain.terrainData;
            Texture2D[] controls = data.alphamapTextures;

            if (controls == null || controls.Length == 0)
                return;

            int layerCount = data.terrainLayers != null ? data.terrainLayers.Length : 0;

            var baseProps = new MaterialPropertyBlock();
            BindSplatMap(baseProps, data, controls, 0);
            cmd.DrawProcedural(Matrix4x4.identity, albedoMat, PASS_SPLAT, MeshTopology.Triangles, 3, 1, baseProps);

            if (addMat != null)
            {
                for (var map = 1; map < controls.Length && map * SPLATS_PER_MAP < layerCount; map++)
                {
                    var addProps = new MaterialPropertyBlock();
                    BindSplatMap(addProps, data, controls, map);
                    cmd.DrawProcedural(Matrix4x4.identity, addMat, PASS_ADD, MeshTopology.Triangles, 3, 1, addProps);
                }
            }

            if (maskMat != null)
            {
                // Once per control map, additively: a terrain spreads its layer weights four layers
                // to a map, so ground painted entirely with layers 4+ carries no weight at all in
                // the first map and would read as a hole in the coverage.
                for (var map = 0; map < controls.Length; map++)
                {
                    var maskProps = new MaterialPropertyBlock();
                    maskProps.SetTexture(CONTROL_ID, controls[map]);
                    maskProps.SetVector(CONTROL_ST_ID, UNTILED_ST);
                    maskProps.SetVector(CONTROL_TEXEL_SIZE_ID, TexelSize(controls[map]));
                    maskProps.SetVector(LAYER_MASK_ID, LayerMask(layerCount - (map * SPLATS_PER_MAP)));
                    cmd.DrawProcedural(Matrix4x4.identity, maskMat, PASS_MASK, MeshTopology.Triangles, 3, 1, maskProps);
                }
            }
        }

        private static void BindSplatMap(MaterialPropertyBlock props, TerrainData data, Texture2D[] controls, int map)
        {
            props.SetTexture(CONTROL_ID, controls[map]);
            props.SetVector(CONTROL_ST_ID, UNTILED_ST);
            props.SetVector(CONTROL_TEXEL_SIZE_ID, TexelSize(controls[map]));
            props.SetVector(TERRAIN_SIZE_ID, new Vector4(data.size.x, data.size.z, 0f, 0f));

            int baseLayer = map * SPLATS_PER_MAP;

            for (var i = 0; i < SPLATS_PER_MAP; i++)
            {
                Texture diffuse = Texture2D.whiteTexture;
                Vector4 st = UNTILED_ST;

                int layerIndex = baseLayer + i;

                if (data.terrainLayers != null && layerIndex < data.terrainLayers.Length)
                {
                    TerrainLayer layer = data.terrainLayers[layerIndex];

                    if (layer != null)
                    {
                        if (layer.diffuseTexture != null)
                            diffuse = layer.diffuseTexture;

                        st = LayerST(layer.tileSize, layer.tileOffset);
                    }
                }

                props.SetTexture(SPLAT_ID[i], diffuse);
                props.SetVector(SPLAT_ST_ID[i], st);
            }
        }

        private static void DrawSplatSource(CommandBuffer cmd, GrassColorMapSplatSource source, Material albedoMat, Material maskMat)
        {
            Vector4 controlST = ControlST(source.bounds, source.controlTileSize, source.controlTileOffset);
            Vector4 controlTexelSize = TexelSize(source.control);
            int layerCount = source.LayerCount;

            var props = new MaterialPropertyBlock();
            props.SetTexture(CONTROL_ID, source.control);
            props.SetVector(CONTROL_ST_ID, controlST);
            props.SetVector(CONTROL_TEXEL_SIZE_ID, controlTexelSize);
            props.SetVector(TERRAIN_SIZE_ID, new Vector4(source.bounds.size.x, source.bounds.size.z, 0f, 0f));

            for (var i = 0; i < SPLATS_PER_MAP; i++)
            {
                // Channels past the source's layer count are bound black: whatever the control map
                // carries there (an opaque alpha, an unused channel) must add no colour.
                Texture diffuse = Texture2D.blackTexture;
                Vector4 st = UNTILED_ST;

                if (i < layerCount)
                {
                    GrassColorMapSplatSource.Layer layer = source.layers[i];

                    if (layer.diffuse != null)
                        diffuse = layer.diffuse;

                    st = LayerST(layer.tileSize, new Vector2(
                        layer.tileOffset.x + source.bounds.min.x,
                        layer.tileOffset.y + source.bounds.min.z));
                }

                props.SetTexture(SPLAT_ID[i], diffuse);
                props.SetVector(SPLAT_ST_ID[i], st);
            }

            cmd.DrawProcedural(Matrix4x4.identity, albedoMat, PASS_SPLAT, MeshTopology.Triangles, 3, 1, props);

            if (maskMat != null)
            {
                var maskProps = new MaterialPropertyBlock();
                maskProps.SetTexture(CONTROL_ID, source.control);
                maskProps.SetVector(CONTROL_ST_ID, controlST);
                maskProps.SetVector(CONTROL_TEXEL_SIZE_ID, controlTexelSize);
                maskProps.SetVector(LAYER_MASK_ID, LayerMask(layerCount));
                cmd.DrawProcedural(Matrix4x4.identity, maskMat, PASS_MASK, MeshTopology.Triangles, 3, 1, maskProps);
            }
        }

        private static void DrawRaw(CommandBuffer cmd, GameObject go, Material albedoMat, Material maskMat)
        {
            Texture main = ResolveMainTexture(go);

            if (main == null)
                return;

            var props = new MaterialPropertyBlock();
            props.SetTexture(MAIN_TEX_ID, main);
            cmd.DrawProcedural(Matrix4x4.identity, albedoMat, PASS_RAW, MeshTopology.Triangles, 3, 1, props);

            if (maskMat != null)
            {
                var maskProps = new MaterialPropertyBlock();
                maskProps.SetTexture(CONTROL_ID, Texture2D.whiteTexture);
                maskProps.SetVector(CONTROL_ST_ID, UNTILED_ST);
                maskProps.SetVector(CONTROL_TEXEL_SIZE_ID, TexelSize(Texture2D.whiteTexture));
                maskProps.SetVector(LAYER_MASK_ID, LayerMask(1));
                cmd.DrawProcedural(Matrix4x4.identity, maskMat, PASS_MASK, MeshTopology.Triangles, 3, 1, maskProps);
            }
        }

        private void BakeWithBlits(RenderTexture rt, int res, Vector2 boundsMin, Vector2 boundsSize)
        {
            Graphics.SetRenderTarget(rt);

            // Alpha 0 marks the whole map as uncovered. The grass shader multiplies its ground tint
            // by this coverage, so clearing it to 1 tints every blade inside the bounds box with
            // NEUTRAL_GROUND even where nothing was ever drawn.
            GL.Clear(true, true, new Color(NEUTRAL_GROUND.r, NEUTRAL_GROUND.g, NEUTRAL_GROUND.b, 0f));

            GL.PushMatrix();

            // Graphics.DrawTexture takes screen rects whose origin is the top-left corner, so the
            // pixel matrix has to run y downwards and each viewport rect - built from a +Z-up world
            // mapping - has to be flipped into that space.
            GL.LoadPixelMatrix(0f, res, res, 0f);

            try
            {
                foreach (GameObject go in terrainObjects)
                {
                    if (go == null)
                        continue;

                    if ((cullingMask.value & (1 << go.layer)) == 0)
                        continue;

                    if (!TryGetViewport(go, boundsMin, boundsSize, res, out Rect destPx))
                        continue;

                    Texture main = ResolveMainTexture(go);

                    if (main == null)
                        continue;

                    // Coverage comes from the drawn texture's own alpha, so only the rectangles an
                    // object actually painted end up marked as ground.
                    Graphics.DrawTexture(TopLeftRect(destPx, res), main);
                }

                foreach (GrassColorMapSplatSource source in splatSources)
                {
                    if (source == null || source.LayerCount == 0 || source.layers[0].diffuse == null)
                        continue;

                    if (!TryGetViewport(source.bounds, boundsMin, boundsSize, res, out Rect destPx))
                        continue;

                    // Without the splat shaders the source's first layer stands in for the blend,
                    // tiled across the region exactly as the shader path would sample it.
                    GrassColorMapSplatSource.Layer layer = source.layers[0];
                    Vector4 st = LayerST(layer.tileSize, layer.tileOffset);

                    var sourceRect = new Rect(
                        (source.bounds.min.x * st.x) + st.z,
                        (source.bounds.min.z * st.y) + st.w,
                        source.bounds.size.x * st.x,
                        source.bounds.size.z * st.y);

                    Graphics.DrawTexture(TopLeftRect(destPx, res), layer.diffuse, sourceRect, 0, 0, 0, 0);
                }
            }
            finally
            {
                GL.PopMatrix();
            }
        }

        private static Rect TopLeftRect(Rect bottomLeftPx, int res) =>
            new (bottomLeftPx.x, res - bottomLeftPx.yMax, bottomLeftPx.width, bottomLeftPx.height);

        private static bool TryGetViewport(GameObject go, Vector2 boundsMin, Vector2 boundsSize, int res, out Rect viewport)
        {
            viewport = default;

            return TryResolveBounds(go, out Bounds b) && TryGetViewport(b, boundsMin, boundsSize, res, out viewport);
        }

        /// <summary>
        ///     Pixel rectangle a world-space region occupies inside a <paramref name="res" />² map
        ///     that spans <paramref name="boundsMin" /> / <paramref name="boundsSize" /> in XZ.
        ///     Regions with no XZ area have no viewport.
        /// </summary>
        internal static bool TryGetViewport(Bounds b, Vector2 boundsMin, Vector2 boundsSize, int res, out Rect viewport)
        {
            viewport = default;

            if (!HasArea(b) || boundsSize.x <= 0f || boundsSize.y <= 0f)
                return false;

            float u0 = (b.min.x - boundsMin.x) / boundsSize.x;
            float v0 = (b.min.z - boundsMin.y) / boundsSize.y;
            float du = b.size.x / boundsSize.x;
            float dv = b.size.z / boundsSize.y;

            viewport = new Rect(u0 * res, v0 * res, du * res, dv * res);
            return true;
        }

        internal static bool TryResolveBounds(GameObject go, out Bounds bounds)
        {
            Terrain terrain = go.GetComponent<Terrain>();

            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 size = terrain.terrainData.size;
                bounds = new Bounds(go.transform.position + (size * 0.5f), size);
                return true;
            }

            Renderer renderer = go.GetComponentInChildren<Renderer>();

            if (renderer != null)
            {
                bounds = renderer.bounds;
                return true;
            }

            bounds = default;
            return false;
        }

        /// <summary>
        ///     Unity's terrain-layer scale/offset packing: a layer texture repeats every
        ///     <paramref name="tileSize" /> metres, shifted by <paramref name="tileOffset" /> metres,
        ///     so <c>uv = worldXZ * ST.xy + ST.zw</c>. Non-positive tile sizes fall back to one metre.
        /// </summary>
        internal static Vector4 LayerST(Vector2 tileSize, Vector2 tileOffset)
        {
            float tileX = tileSize.x > 0f ? tileSize.x : 1f;
            float tileY = tileSize.y > 0f ? tileSize.y : 1f;
            return new Vector4(1f / tileX, 1f / tileY, tileOffset.x / tileX, tileOffset.y / tileY);
        }

        /// <summary>
        ///     Scale/offset that maps the 0..1 UV of a region's viewport onto a control map that
        ///     repeats every <paramref name="tileSize" /> metres shifted by
        ///     <paramref name="tileOffset" />. A control map sized and placed to span the region
        ///     exactly once resolves to (1, 1, 0, 0).
        /// </summary>
        internal static Vector4 ControlST(Bounds region, Vector2 tileSize, Vector2 tileOffset)
        {
            float tileX = tileSize.x > 0f ? tileSize.x : 1f;
            float tileY = tileSize.y > 0f ? tileSize.y : 1f;

            return new Vector4(
                region.size.x / tileX,
                region.size.z / tileY,
                (region.min.x + tileOffset.x) / tileX,
                (region.min.z + tileOffset.y) / tileY);
        }

        /// <summary>
        ///     Control channels that carry a layer: 1 for the first <paramref name="layerCount" />
        ///     of the four channels, 0 for the rest. Coverage is the weight summed over these only.
        /// </summary>
        internal static Vector4 LayerMask(int layerCount)
        {
            int n = Mathf.Clamp(layerCount, 0, SPLATS_PER_MAP);
            return new Vector4(n > 0 ? 1f : 0f, n > 1 ? 1f : 0f, n > 2 ? 1f : 0f, n > 3 ? 1f : 0f);
        }

        private static bool HasArea(Bounds b) =>
            b.size.x > 0f && b.size.z > 0f;

        private static void Encapsulate(ref Bounds combined, ref bool any, Bounds b)
        {
            if (!any)
            {
                combined = b;
                any = true;
            }
            else
                combined.Encapsulate(b);
        }

        private static Texture ResolveMainTexture(GameObject go)
        {
            Terrain terrain = go.GetComponent<Terrain>();

            if (terrain != null && terrain.materialTemplate != null && terrain.materialTemplate.mainTexture != null)
                return terrain.materialTemplate.mainTexture;

            Renderer renderer = go.GetComponentInChildren<Renderer>();

            if (renderer != null && renderer.sharedMaterial != null)
                return renderer.sharedMaterial.mainTexture;

            return null;
        }

        // Matches Unity's _TexelSize convention: (1/width, 1/height, width, height).
        private static Vector4 TexelSize(Texture texture) =>
            new (1f / texture.width, 1f / texture.height, texture.width, texture.height);

        private static void SafeDestroy(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
