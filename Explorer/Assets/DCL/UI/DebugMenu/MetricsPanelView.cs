using DCL.Profiling;
using System;
using System.Globalization;
using UnityEngine.UIElements;

namespace DCL.UI.DebugMenu
{
    /// <summary>
    ///     Creator-facing "Scene Stats" panel of the scene debug menu showing the current scene's
    ///     content stats (triangles, entities, meshes, materials, textures, then geometries, colliders
    ///     and external content) against the documented scene limitations. The five capped metrics come
    ///     first with their per-parcel suggested limit; the three uncapped ones follow as plain counts.
    ///     Values are produced by <see cref="SceneContentStatsFormatter" />.
    /// </summary>
    public class MetricsPanelView : DebugPanelView
    {
        private const int SQUARE_METERS_PER_PARCEL = 256;

        private const string USS_ROW = "metrics-panel__row";
        private const string USS_ROW_LAST = "metrics-panel__row--last";
        private const string USS_ROW_LABEL = "metrics-panel__row-label";
        private const string USS_ROW_VALUE = "metrics-panel__row-value";

        private readonly Label parcelSummary;

        private readonly Label triangles;
        private readonly Label entities;
        private readonly Label bodies;
        private readonly Label materials;
        private readonly Label textures;
        private readonly Label geometries;
        private readonly Label colliders;
        private readonly Label externalContent;

        public MetricsPanelView(VisualElement root, Button sidebarButton, Action closeClicked) : base(root, sidebarButton, closeClicked)
        {
            parcelSummary = root.Q<Label>("ParcelSummary");

            VisualElement rows = root.Q("MetricsRows");

            triangles = AddRow(rows, "TRIANGLES");
            entities = AddRow(rows, "ENTITIES");
            bodies = AddRow(rows, "BODIES");
            materials = AddRow(rows, "MATERIALS");
            textures = AddRow(rows, "TEXTURES");
            geometries = AddRow(rows, "GEOMETRIES");
            colliders = AddRow(rows, "COLLIDERS");
            externalContent = AddRow(rows, "EXTERNAL CONTENT");

            externalContent.parent.AddToClassList(USS_ROW_LAST);
        }

        public void SetSceneContext(int parcelCount)
        {
            string parcels = parcelCount == 1 ? "Parcel" : "Parcels";
            int squareMeters = parcelCount * SQUARE_METERS_PER_PARCEL;
            parcelSummary.text = $"{parcelCount} {parcels} = {squareMeters.ToString("N0", CultureInfo.InvariantCulture)} m²";
        }

        public void ClearSceneContext()
        {
            parcelSummary.text = SceneContentStatsFormatter.EMPTY_VALUE;
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
