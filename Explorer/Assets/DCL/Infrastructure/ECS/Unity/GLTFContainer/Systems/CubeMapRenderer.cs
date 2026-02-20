using UnityEngine;

public class CubeMapRenderer : MonoBehaviour
{
    private Camera renderCamera;
    private Material twoSidedMaterial;
    private RenderTexture[] faceTextures;

    private static readonly Vector3[] directions = new Vector3[]
    {
        Vector3.right,   // +X
        Vector3.left,    // -X
        Vector3.up,      // +Y
        Vector3.down,    // -Y
        Vector3.forward, // +Z
        Vector3.back     // -Z
    };

    private static readonly Vector3[] upVectors = new Vector3[]
    {
        Vector3.up,      // +X
        Vector3.up,      // -X
        Vector3.back,    // +Y
        Vector3.forward, // -Y
        Vector3.up,      // +Z
        Vector3.up       // -Z
    };

    public void Initialize(int resolution, Shader twoSidedShader)
    {
        if (twoSidedShader == null)
        {
            Debug.LogError("Two-sided shader is null! Cannot initialize CubeMapRenderer.");
            return;
        }

        // Create camera
        GameObject camObj = new GameObject("VoxelizationCamera");
        camObj.transform.SetParent(transform);
        camObj.hideFlags = HideFlags.HideAndDontSave;

        renderCamera = camObj.AddComponent<Camera>();
        renderCamera.enabled = false;
        renderCamera.clearFlags = CameraClearFlags.SolidColor;
        renderCamera.backgroundColor = Color.black;
        renderCamera.fieldOfView = 90f;
        renderCamera.aspect = 1f;
        renderCamera.nearClipPlane = 0.01f;
        renderCamera.farClipPlane = 1000f;
        renderCamera.orthographic = false;

        // Create material
        twoSidedMaterial = new Material(twoSidedShader);

        if (twoSidedMaterial == null)
        {
            Debug.LogError("Failed to create two-sided material!");
            return;
        }

        // Create render textures for each face
        faceTextures = new RenderTexture[6];
        for (int i = 0; i < 6; i++)
        {
            faceTextures[i] = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32);
            faceTextures[i].name = $"CubeFace_{i}";
        }

        Debug.Log("CubeMapRenderer initialized successfully");
    }

    public CubeMapResult RenderCubeMap(Vector3 position, float cellSize, GameObject[] objectsToRender)
    {
        // Add safety checks
        if (renderCamera == null)
        {
            Debug.LogError("RenderCamera is null! Was Initialize() called?");
            return new CubeMapResult(true);
        }

        if (twoSidedMaterial == null)
        {
            Debug.LogError("TwoSidedMaterial is null! Check shader assignment.");
            return new CubeMapResult(true);
        }

        if (objectsToRender == null || objectsToRender.Length == 0)
        {
            Debug.LogWarning("No objects to render in cube map");
            return new CubeMapResult(true);
        }

        CubeMapResult result = new CubeMapResult(true); // Initialize with constructor

        // Position camera at cell center, near plane at cell edge
        renderCamera.transform.position = position;
        renderCamera.nearClipPlane = cellSize * 0.5f;

        // Store original materials
        var originalMaterials = new System.Collections.Generic.Dictionary<Renderer, Material[]>();
        foreach (var obj in objectsToRender)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                originalMaterials[renderer] = renderer.sharedMaterials;

                // Set to two-sided material
                Material[] mats = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = twoSidedMaterial;
                renderer.sharedMaterials = mats;
            }
        }

        // Render each face
        for (int i = 0; i < 6; i++)
        {
            renderCamera.transform.rotation = Quaternion.LookRotation(directions[i], upVectors[i]);
            renderCamera.targetTexture = faceTextures[i];
            renderCamera.Render();

            // Count red and blue pixels
            result.faceCounts[i] = CountPixels(faceTextures[i]);
        }

        // Restore original materials
        foreach (var kvp in originalMaterials)
        {
            kvp.Key.sharedMaterials = kvp.Value;
        }

        return result;
    }

    private PixelCount CountPixels(RenderTexture rt)
    {
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        Color[] pixels = tex.GetPixels();

        int redPixels = 0;
        int bluePixels = 0;

        foreach (Color pixel in pixels)
        {
            // Red channel > 0.5 = back-facing (inside view)
            if (pixel.r > 0.5f && pixel.b < 0.5f)
                redPixels++;
            // Blue channel > 0.5 = front-facing (outside view)
            else if (pixel.b > 0.5f && pixel.r < 0.5f)
                bluePixels++;
        }

        Destroy(tex);

        return new PixelCount { red = redPixels, blue = bluePixels, total = pixels.Length };
    }

    public void Cleanup()
    {
        if (renderCamera != null)
            DestroyImmediate(renderCamera.gameObject);

        if (twoSidedMaterial != null)
            DestroyImmediate(twoSidedMaterial);

        if (faceTextures != null)
        {
            foreach (var tex in faceTextures)
                if (tex != null) tex.Release();
        }
    }

    public struct PixelCount
    {
        public int red;
        public int blue;
        public int total;

        public float RedPercent => total > 0 ? (float)red / total : 0f;
        public float BluePercent => total > 0 ? (float)blue / total : 0f;
    }

    public struct CubeMapResult
    {
        public PixelCount[] faceCounts;

        public CubeMapResult(bool init)
        {
            faceCounts = new PixelCount[6];
            for (int i = 0; i < 6; i++)
            {
                faceCounts[i] = new PixelCount(); // Initialize each element
            }
        }
    }
}
