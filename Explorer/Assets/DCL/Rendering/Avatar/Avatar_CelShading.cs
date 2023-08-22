using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Avatar_CelShading : MonoBehaviour
{
    private MaterialPropertyBlock _propertyBlock;

    [SerializeField]
    Color col_MatPropBlock = Color.red;
    [SerializeField]
    int nTextureArrayID = 0;

    private void Awake()
    {
        // Make sure to initialize it.
        // Usually it needs to be initialized only once.
        _propertyBlock = new MaterialPropertyBlock();
    }

    // Start is called before the first frame update
    void Start()
    {
        SetColor("_BaseColor", col_MatPropBlock);
        SetTextureArrayID("_TextureArrayID", nTextureArrayID);
    }

    private void SetColor(string colorPropertyName, Color color)
    {
        this.GetComponent<Renderer>().GetPropertyBlock(_propertyBlock); // Get previously set values. They will reset otherwise
        _propertyBlock.SetColor(colorPropertyName, color);
        this.GetComponent<Renderer>().SetPropertyBlock(_propertyBlock);
    }

    private void SetTextureArrayID(string colorPropertyName, int _nTextureArrayID)
    {
        Renderer meshRenderer = GetComponent<Renderer>();
        meshRenderer.GetPropertyBlock(_propertyBlock); // Get previously set values. They will reset otherwise
        _propertyBlock.SetInt(colorPropertyName, _nTextureArrayID);
        meshRenderer.SetPropertyBlock(_propertyBlock);

        if(TextureArrayCreator.Instance)
            meshRenderer.material.SetTexture("_MainTex", TextureArrayCreator.Instance.Texture2DArray);
    }
}
