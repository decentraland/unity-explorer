using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Avatar_CelShading : MonoBehaviour
{
    private MaterialPropertyBlock _propertyBlock;

    public Color col_MatPropBlock = Color.red;
    public int _BaseMapArr_ID = 0;
    public int _AlphaTextureArr_ID = 0;
    public int _MetallicGlossMapArr_ID = 0;
    public int _BumpMapArr_ID = 0;
    public int _EmissionMapArr_ID = 0;

    static int _BaseMapArr_ShaderID = Shader.PropertyToID("_BaseMapArr_ID");
    static int _AlphaTextureArr_ShaderID = Shader.PropertyToID("_AlphaTextureArr_ID");
    static int _MetallicGlossMapArr_ShaderID = Shader.PropertyToID("_MetallicGlossMapArr_ID");
    static int _BumpMapArr_ShaderID = Shader.PropertyToID("_BumpMapArr_ID");
    static int _EmissionMapArr_ShaderID = Shader.PropertyToID("_EmissionMapArr_ID");

    private void Awake()
    {
        // Make sure to initialize it.
        // Usually it needs to be initialized only once.
        _propertyBlock = new MaterialPropertyBlock();
        //SetColor("_BaseColor", col_MatPropBlock);
        SetTextureArrayID();

        this.GetComponent<MeshRenderer>().material.SetInteger(_BaseMapArr_ShaderID, _BaseMapArr_ID);
        this.GetComponent<MeshRenderer>().material.SetInteger(_AlphaTextureArr_ShaderID, _AlphaTextureArr_ID);
        this.GetComponent<MeshRenderer>().material.SetInteger(_MetallicGlossMapArr_ShaderID, _MetallicGlossMapArr_ID);
        this.GetComponent<MeshRenderer>().material.SetInteger(_BumpMapArr_ShaderID, _BumpMapArr_ID);
        this.GetComponent<MeshRenderer>().material.SetInteger(_EmissionMapArr_ShaderID, _EmissionMapArr_ID);
    }

    // Start is called before the first frame update
    void Update()
    {
        SetTextureArrayID();
    }

    private void SetColor(string colorPropertyName, Color color)
    {
        // this.GetComponent<Renderer>().GetPropertyBlock(_propertyBlock); // Get previously set values. They will reset otherwise
        // _propertyBlock.SetColor(colorPropertyName, color);
        // this.GetComponent<Renderer>().SetPropertyBlock(_propertyBlock);
    }

    private void SetTextureArrayID()
    {

        // this.GetComponent<Renderer>().GetPropertyBlock(_propertyBlock); // Get previously set values. They will reset otherwise
        // _propertyBlock.SetInteger("_BaseMapArr_ID", _BaseMapArr_ID);
        // _propertyBlock.SetInteger("_AlphaTextureArr_ID", _AlphaTextureArr_ID);
        // _propertyBlock.SetInteger("_MetallicGlossMapArr_ID", _MetallicGlossMapArr_ID);
        // _propertyBlock.SetInteger("_BumpMapArr_ID", _BumpMapArr_ID);
        // _propertyBlock.SetInteger("_EmissionMapArr_ID", _EmissionMapArr_ID);
        // this.GetComponent<Renderer>().SetPropertyBlock(_propertyBlock);

        if(TextureArrayCreator.Instance)
        {
            this.GetComponent<Renderer>().material.SetTexture("_BaseMapArr",            TextureArrayCreator.Instance.m_TextureArrays.texture2DArray_BaseMap);
            this.GetComponent<Renderer>().material.SetTexture("_AlphaTextureArr",       TextureArrayCreator.Instance.m_TextureArrays.texture2DArray_AlphaTexture);
            this.GetComponent<Renderer>().material.SetTexture("_MetallicGlossMapArr",   TextureArrayCreator.Instance.m_TextureArrays.texture2DArray_MetallicGlossMap);
            this.GetComponent<Renderer>().material.SetTexture("_BumpMapArr",            TextureArrayCreator.Instance.m_TextureArrays.texture2DArray_BumpMap);
            this.GetComponent<Renderer>().material.SetTexture("_EmissionMapArr",        TextureArrayCreator.Instance.m_TextureArrays.texture2DArray_EmissionMap);
        }
    }
}
