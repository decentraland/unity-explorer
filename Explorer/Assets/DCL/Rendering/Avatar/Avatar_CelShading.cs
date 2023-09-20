using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Avatar_CelShading : MonoBehaviour
{
    public Color _BaseColor = Color.black;
    public bool _UseArrays = true;

    public int _BaseMapArr_ID = 0;
    public int _AlphaTextureArr_ID = 0;
    public int _MetallicGlossMapArr_ID = 0;
    public int _BumpMapArr_ID = 0;
    public int _EmissionMapArr_ID = 0;

    static int _BaseColour_ShaderID = Shader.PropertyToID("_BaseColor");
    static int _BaseMapArr_ShaderID = Shader.PropertyToID("_BaseMapArr_ID");
    static int _AlphaTextureArr_ShaderID = Shader.PropertyToID("_AlphaTextureArr_ID");
    static int _MetallicGlossMapArr_ShaderID = Shader.PropertyToID("_MetallicGlossMapArr_ID");
    static int _BumpMapArr_ShaderID = Shader.PropertyToID("_BumpMapArr_ID");
    static int _EmissionMapArr_ShaderID = Shader.PropertyToID("_EmissionMapArr_ID");

    private void Awake()
    {
        this.GetComponent<MeshRenderer>().material.SetColor(_BaseColour_ShaderID, _BaseColor);

        if (_UseArrays)
        {
            this.GetComponent<MeshRenderer>().material.SetInteger(_BaseMapArr_ShaderID, _BaseMapArr_ID);
            this.GetComponent<MeshRenderer>().material.SetInteger(_AlphaTextureArr_ShaderID, _AlphaTextureArr_ID);
            this.GetComponent<MeshRenderer>().material.SetInteger(_MetallicGlossMapArr_ShaderID, _MetallicGlossMapArr_ID);
            this.GetComponent<MeshRenderer>().material.SetInteger(_BumpMapArr_ShaderID, _BumpMapArr_ID);
            this.GetComponent<MeshRenderer>().material.SetInteger(_EmissionMapArr_ShaderID, _EmissionMapArr_ID);
        }
    }

    // Start is called before the first frame update
    void Update()
    {
        SetTextureArrayID();
    }

    private void SetTextureArrayID()
    {
        if(TextureArrayCreator.Instance && _UseArrays)
        {
            this.GetComponent<Renderer>().material.SetTexture("_BaseMapArr",            TextureArrayCreator.Instance.m_TextureArrays.texture2DArray_BaseMap);
            this.GetComponent<Renderer>().material.SetTexture("_AlphaTextureArr",       TextureArrayCreator.Instance.m_TextureArrays.texture2DArray_AlphaTexture);
            this.GetComponent<Renderer>().material.SetTexture("_MetallicGlossMapArr",   TextureArrayCreator.Instance.m_TextureArrays.texture2DArray_MetallicGlossMap);
            this.GetComponent<Renderer>().material.SetTexture("_BumpMapArr",            TextureArrayCreator.Instance.m_TextureArrays.texture2DArray_BumpMap);
            this.GetComponent<Renderer>().material.SetTexture("_EmissionMapArr",        TextureArrayCreator.Instance.m_TextureArrays.texture2DArray_EmissionMap);
        }
    }
}
