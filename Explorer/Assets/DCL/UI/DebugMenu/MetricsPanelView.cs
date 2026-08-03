using DCL.Profiling;
using System;
using UnityEngine.UIElements;

namespace DCL.UI.DebugMenu
{
    /// <summary>
    ///     Creator-facing panel of the scene debug menu showing the current scene's content stats
    ///     (entities, triangles, meshes, materials, textures, colliders, external content) against
    ///     the documented scene limitations. Values are produced by <see cref="SceneContentStatsFormatter" />.
    /// </summary>
    public class MetricsPanelView : DebugPanelView
    {
        private const string USS_ROW = "metrics-panel__row";
        private const string USS_ROW_LABEL = "metrics-panel__row-label";
        private const string USS_ROW_VALUE = "metrics-panel__row-value";

        private readonly Label entities;
        private readonly Label triangles;
        private readonly Label bodies;
        private readonly Label geometries;
        private readonly Label materials;
        private readonly Label textures;
        private readonly Label colliders;
        private readonly Label externalContent;

        public MetricsPanelView(VisualElement root, Button sidebarButton, Action closeClicked) : base(root, sidebarButton, closeClicked)
        {
            VisualElement rows = root.Q("MetricsRows");

            entities = AddRow(rows, "Entities");
            triangles = AddRow(rows, "Triangles");
            bodies = AddRow(rows, "Meshes (bodies)");
            geometries = AddRow(rows, "Geometries");
            materials = AddRow(rows, "Materials");
            textures = AddRow(rows, "Textures");
            colliders = AddRow(rows, "Colliders");
            externalContent = AddRow(rows, "External content");
        }

        public void UpdateValues(in SceneContentStatsText text)
        {
            entities.text = text.Entities;
            triangles.text = text.Triangles;
            bodies.text = text.Bodies;
            geometries.text = text.Geometries;
            materials.text = text.Materials;
            textures.text = text.Textures;
            colliders.text = text.Colliders;
            externalContent.text = text.ExternalContent;
        }

        private static Label AddRow(VisualElement container, string title)
        {
            var row = new VisualElement();
            row.AddToClassList(USS_ROW);

            var titleLabel = new Label(title);
            titleLabel.AddToClassList(USS_ROW_LABEL);
            row.Add(titleLabel);

            var valueLabel = new Label(SceneContentStatsFormatter.EMPTY_VALUE);
            valueLabel.AddToClassList(USS_ROW_VALUE);
            row.Add(valueLabel);

            container.Add(row);
            return valueLabel;
        }
    }
}
