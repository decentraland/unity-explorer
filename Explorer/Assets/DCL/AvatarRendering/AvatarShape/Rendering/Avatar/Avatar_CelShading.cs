using UnityEngine;

public class Avatar_CelShading : MonoBehaviour
{
    public Color _BaseColor = Color.black;
    public bool _UseArrays = true;

    public int _BaseMapArr_ID;
    public int _AlphaTextureArr_ID;
    public int _MetallicGlossMapArr_ID;
    public int _BumpMapArr_ID;
    public int _EmissionMapArr_ID;

    private static readonly int _BaseColour_ShaderID = Shader.PropertyToID("_BaseColor");
    private static readonly int _BaseMapArr_ShaderID = Shader.PropertyToID("_BaseMapArr_ID");
    private static readonly int _AlphaTextureArr_ShaderID = Shader.PropertyToID("_AlphaTextureArr_ID");
    private static readonly int _MetallicGlossMapArr_ShaderID = Shader.PropertyToID("_MetallicGlossMapArr_ID");
    private static readonly int _BumpMapArr_ShaderID = Shader.PropertyToID("_BumpMapArr_ID");
    private static readonly int _EmissionMapArr_ShaderID = Shader.PropertyToID("_EmissionMapArr_ID");

    private void Awake()
    {
        GetComponent<MeshRenderer>().material.SetColor(_BaseColour_ShaderID, _BaseColor);

        if (_UseArrays)
        {
            GetComponent<MeshRenderer>().material.SetInteger(_BaseMapArr_ShaderID, _BaseMapArr_ID);
            GetComponent<MeshRenderer>().material.SetInteger(_AlphaTextureArr_ShaderID, _AlphaTextureArr_ID);
            GetComponent<MeshRenderer>().material.SetInteger(_MetallicGlossMapArr_ShaderID, _MetallicGlossMapArr_ID);
            GetComponent<MeshRenderer>().material.SetInteger(_BumpMapArr_ShaderID, _BumpMapArr_ID);
            GetComponent<MeshRenderer>().material.SetInteger(_EmissionMapArr_ShaderID, _EmissionMapArr_ID);
        }
    }

    // Start is called before the first frame update
    private void Update()
    {
        SetTextureArrayID();
    }

    private void SetTextureArrayID()
    {
        if (TextureArrayCreator.Instance && _UseArrays)
        {
            //this.GetComponent<Renderer>().material.SetTexture("_BaseMapArr",            TextureArrayCreator.Instance.m_TextureArrays.texture2DArray_BaseMap);
            GetComponent<Renderer>().material.SetTexture("_AlphaTextureArr", TextureArrayCreator.Instance.m_TextureArrays.texture2DArray_AlphaTexture);
            GetComponent<Renderer>().material.SetTexture("_MetallicGlossMapArr", TextureArrayCreator.Instance.m_TextureArrays.texture2DArray_MetallicGlossMap);
            GetComponent<Renderer>().material.SetTexture("_BumpMapArr", TextureArrayCreator.Instance.m_TextureArrays.texture2DArray_BumpMap);
            GetComponent<Renderer>().material.SetTexture("_EmissionMapArr", TextureArrayCreator.Instance.m_TextureArrays.texture2DArray_EmissionMap);
        }
    }
}
