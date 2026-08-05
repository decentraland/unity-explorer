using DCL.Profiling;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.UI.DebugMenu
{
    /// <summary>
    ///     Creator-facing "Scene Stats" panel of the scene debug menu showing the current scene's
    ///     content stats (triangles, entities, meshes, materials, textures, then geometries, colliders
    ///     and videos) against the documented scene limitations. The five capped metrics come first
    ///     with their per-parcel suggested limit; the three uncapped ones follow as plain counts.
    ///     Each row carries an info marker whose hover tooltip explains what the metric indicates.
    ///     Values are produced by <see cref="SceneContentStatsFormatter" />.
    /// </summary>
    public class MetricsPanelView : DebugPanelView
    {
        private const string USS_ROW = "metrics-panel__row";
        private const string USS_ROW_EVEN = "metrics-panel__row--even";
        private const string USS_ROW_LABEL = "metrics-panel__row-label";
        private const string USS_ROW_VALUE = "metrics-panel__row-value";
        private const string USS_ROW_INFO = "metrics-panel__row-info";
        private const string USS_TOOLTIP = "metrics-panel__tooltip";
        private const string USS_TOOLTIP_TEXT = "metrics-panel__tooltip-text";

        private const string INFO_GLYPH = "i";
        private const float TOOLTIP_GAP = 12f;

        // Capped metrics carry Decentraland's documented per-parcel scene limit; the value renders
        // white within budget and orange over it. Uncapped metrics render as plain informative counts.
        private const string TOOLTIP_TRIANGLES = "Total triangles across the scene's meshes. Shown against Decentraland's per-parcel scene limit — within budget it stays white, over budget it turns orange.";
        private const string TOOLTIP_ENTITIES = "Live entities in the scene, the count your scene code controls most directly. Shown against Decentraland's per-parcel scene limit.";
        private const string TOOLTIP_BODIES = "Mesh instances submitted to the renderer (draw objects) — 100 copies of one mesh count as 100 bodies. Shown against Decentraland's per-parcel scene limit.";
        private const string TOOLTIP_MATERIALS = "Unique materials in the scene. No limit shown: the SRP Batcher groups draws by shader variant, so many materials sharing few variants cost memory and texture budget, not frame time.";
        private const string TOOLTIP_TEXTURES = "Unique textures loaded by the scene's materials — a memory/VRAM budget. Shown against Decentraland's per-parcel scene limit.";
        private const string TOOLTIP_GEOMETRIES = "Unique meshes loaded. Reusing one mesh across many bodies keeps this low — a sign of efficient instancing. No documented limit.";
        private const string TOOLTIP_COLLIDERS = "Colliders in the scene (primitive + GLTF). No documented limit; high counts raise physics cost.";
        private const string TOOLTIP_VIDEOS = "Media players in the scene — one per video or audio stream, regardless of source. No documented limit.";

        private readonly VisualElement documentRoot;
        private readonly VisualElement tooltip;
        private readonly Label tooltipLabel;

        private readonly Label triangles;
        private readonly Label entities;
        private readonly Label bodies;
        private readonly Label materials;
        private readonly Label textures;
        private readonly Label geometries;
        private readonly Label colliders;
        private readonly Label videos;

        public MetricsPanelView(VisualElement root, Button sidebarButton, Action closeClicked) : base(root, sidebarButton, closeClicked)
        {
            // The panel clips its own overflow (rounded corners), so the tooltip lives on the document
            // root and floats in the empty space to the panel's left, where it is never clipped.
            documentRoot = root.parent;

            tooltip = new VisualElement { pickingMode = PickingMode.Ignore };
            tooltip.AddToClassList(USS_TOOLTIP);
            tooltip.style.display = DisplayStyle.None;

            tooltipLabel = new Label();
            tooltipLabel.AddToClassList(USS_TOOLTIP_TEXT);
            tooltip.Add(tooltipLabel);
            documentRoot.Add(tooltip);

            VisualElement rows = root.Q("MetricsRows");

            triangles = AddRow(rows, "TRIANGLES", TOOLTIP_TRIANGLES);
            entities = AddRow(rows, "ENTITIES", TOOLTIP_ENTITIES);
            bodies = AddRow(rows, "BODIES", TOOLTIP_BODIES);
            materials = AddRow(rows, "MATERIALS", TOOLTIP_MATERIALS);
            textures = AddRow(rows, "TEXTURES", TOOLTIP_TEXTURES);
            geometries = AddRow(rows, "GEOMETRIES", TOOLTIP_GEOMETRIES);
            colliders = AddRow(rows, "COLLIDERS", TOOLTIP_COLLIDERS);
            videos = AddRow(rows, "EXTERNAL VIDEOS/AUDIOS", TOOLTIP_VIDEOS);
        }

        public override void Toggle()
        {
            base.Toggle();

            if (!Visible)
                HideTooltip();
        }

        public void UpdateValues(in SceneContentStatsText text)
        {
            triangles.text = text.Triangles;
            entities.text = text.Entities;
            bodies.text = text.Bodies;
            materials.text = text.Materials;
            textures.text = text.Textures;
            geometries.text = text.Geometries;
            colliders.text = text.Colliders;
            videos.text = text.Videos;
        }

        private Label AddRow(VisualElement container, string title, string tooltipText)
        {
            var row = new VisualElement();
            row.AddToClassList(USS_ROW);

            if (container.childCount % 2 == 0)
                row.AddToClassList(USS_ROW_EVEN);

            var titleLabel = new Label(title);
            titleLabel.AddToClassList(USS_ROW_LABEL);
            row.Add(titleLabel);

            var info = new Label(INFO_GLYPH);
            info.AddToClassList(USS_ROW_INFO);
            info.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(row, tooltipText));
            info.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
            row.Add(info);

            var valueLabel = new Label(SceneContentStatsFormatter.EMPTY_VALUE);
            valueLabel.AddToClassList(USS_ROW_VALUE);
            row.Add(valueLabel);

            container.Add(row);
            return valueLabel;
        }

        private void ShowTooltip(VisualElement row, string text)
        {
            tooltipLabel.text = text;
            tooltip.style.display = DisplayStyle.Flex;

            Rect rowWorld = row.worldBound;
            Vector2 anchor = documentRoot.WorldToLocal(new Vector2(rowWorld.xMin, rowWorld.yMin));

            // Anchor the tooltip's right edge just left of the panel so it grows leftward into free space.
            tooltip.style.right = documentRoot.worldBound.width - anchor.x + TOOLTIP_GAP;
            tooltip.style.top = anchor.y;
        }

        private void HideTooltip() =>
            tooltip.style.display = DisplayStyle.None;
    }
}
