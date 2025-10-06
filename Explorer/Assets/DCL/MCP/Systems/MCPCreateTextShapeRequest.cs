namespace DCL.MCP
{
    public struct MCPColor3
    {
        public float r, g, b;
    }

    public struct MCPColor4
    {
        public float r, g, b, a;
    }

    public struct MCPCreateTextShapeRequest
    {
        public string RequestId;

        // Transform
        public float X, Y, Z;
        public float SX, SY, SZ;
        public float Yaw, Pitch, Roll;
        public int ParentId;

        // Scene
        public string SceneId;

        // Text content & style
        public string Text;
        public float FontSize;
        public string Font;
        public bool FontAutoSize;
        public string TextAlign;
        public float Width, Height;
        public float PaddingTop, PaddingRight, PaddingBottom, PaddingLeft;
        public float LineSpacing;
        public int LineCount;
        public bool TextWrapping;
        public float ShadowBlur, ShadowOffsetX, ShadowOffsetY;
        public float OutlineWidth;
        public MCPColor3? ShadowColor, OutlineColor;
        public MCPColor4? TextColor;
    }
}
