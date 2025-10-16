using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.UIToolkit.Elements
{
    /// <summary>
	/// Draws a vertical or horizontal gradient between two colors.
	/// </summary>
	[UxmlElement]
	public partial class GradientElement : VisualElement
	{
		private static readonly ushort[] Indices = {0, 1, 2, 2, 3, 0};
		private static readonly Vertex[] Vertices = new Vertex[4];

		private static readonly CustomStyleProperty<Color> GradientFrom = new("--gradient-from");
		private static readonly CustomStyleProperty<Color> GradientTo = new("--gradient-to");

        [UxmlAttribute]
		public bool Vertical { get; set; }

		private Color startColor = Color.black;
		private Color endColor = Color.white;

		public GradientElement()
		{
			generateVisualContent += GenerateVisualContent;
			RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
		}

		private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
		{
			customStyle.TryGetValue(GradientFrom, out startColor);
			customStyle.TryGetValue(GradientTo, out endColor);
		}

		private void GenerateVisualContent(MeshGenerationContext mgc)
		{
			var rect = contentRect;

			Vertices[0].tint = startColor;
			Vertices[1].tint = Vertical ? endColor : startColor;
			Vertices[2].tint = endColor;
			Vertices[3].tint = Vertical ? startColor : endColor;

			float left = 0f;
			float right = rect.width;
			float top = 0f;
			float bottom = rect.height;

			Vertices[0].position = new Vector3(left, bottom, Vertex.nearZ);
			Vertices[1].position = new Vector3(left, top, Vertex.nearZ);
			Vertices[2].position = new Vector3(right, top, Vertex.nearZ);
			Vertices[3].position = new Vector3(right, bottom, Vertex.nearZ);

			var mwd = mgc.Allocate(Vertices.Length, Indices.Length);
			mwd.SetAllVertices(Vertices);
			mwd.SetAllIndices(Indices);
		}
	}
}
