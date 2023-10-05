UNITY_DECLARE_TEX2D(_CameraColorTexture);
float4 _CameraColorTexture_TexelSize;
UNITY_DECLARE_TEX2D(_CameraDepthTexture);
UNITY_DECLARE_TEX2D(_CameraDepthNormalsTexture);
 
float3 DecodeNormal(float4 _vEncodedNormal)
{
    const float fScale = 1.7777f;
    float3 vDecodedNormal = _vEncodedNormal.xyz * float3(2.0f*fScale, 2.0f*fScale, 0.0f) + float3(-fScale, -fScale, 1.0f);
    float fFactorisedNormal = 2.0f / dot(vDecodedNormal.xyz, vDecodedNormal.xyz);
    float3 vOutputNormal;
    vOutputNormal.xy = fFactorisedNormal * vDecodedNormal.xy;
    vOutputNormal.z = fFactorisedNormal - 1.0f;
    return vOutputNormal;
}

void Outline_float(float2 _UV, float _fOutlineThickness, float _fDepthSensitivity, float _fNormalsSensitivity, float _fColorSensitivity, float4 _vOutlineColor, out float4 _vOut)
{
    float fHalfScaleFloor = floor(_fOutlineThickness * 0.5f);
    float fHalfScaleCeil = ceil(_fOutlineThickness * 0.5f);
    float2 vTexel = 1.0f / float2(_CameraColorTexture_TexelSize.z, _CameraColorTexture_TexelSize.w);

    float2 vUVSamples[4];
    vUVSamples[0] = _UV.xy - (float2(vTexel.x, vTexel.y) * fHalfScaleFloor);
    vUVSamples[1] = _UV.xy + (float2(vTexel.x, vTexel.y) * fHalfScaleCeil);
    vUVSamples[2] = _UV.xy + float2(vTexel.x * fHalfScaleCeil, -vTexel.y * fHalfScaleFloor);
    vUVSamples[3] = _UV.xy + float2(-vTexel.x * fHalfScaleFloor, vTexel.y * fHalfScaleCeil);

    float fDepthSampling = 0.0f;
    float fDepthSamples[4];
    float3 vNormalSamples[4];
    float3 vColorSamples[4];
    for(unsigned int i = 0; i < 4 ; ++i)
    {
        fDepthSamples[i] = UNITY_SAMPLE_TEX2D(_CameraDepthTexture, vUVSamples[i]).r;
        float4 vDepthNormOutput = UNITY_SAMPLE_TEX2D(_CameraDepthNormalsTexture, vUVSamples[i]);
        fDepthSampling += vDepthNormOutput.w;
        vNormalSamples[i] = DecodeNormal(vDepthNormOutput.xyzw);
        vColorSamples[i] = UNITY_SAMPLE_TEX2D(_CameraColorTexture, vUVSamples[i]);
    }
    
    // Depth
    float fEdgeDepth = 0.0f;
    if ((fDepthSampling * 0.25f) <= 0.9f)
    {
        float fDepthNormOutput = fDepthSamples[1] - fDepthSamples[0];
        float fDepthFiniteDifference1 = fDepthSamples[3] - fDepthSamples[2];
        fEdgeDepth = sqrt(pow(fDepthNormOutput, 2.0f) + pow(fDepthFiniteDifference1, 2)) * 100.0f;
        float fDepthThreshold = (1.0f/_fDepthSensitivity) * fDepthSamples[0];
        fEdgeDepth = fEdgeDepth > fDepthThreshold ? 1.0f : 0.0f;
    }

    // Normals
    float fEdgeNormal = 0.0f;
    if ((fDepthSampling * 0.25f) >= 0.9f)
    {
        float3 vNormalFiniteDifference0 = vNormalSamples[1] - vNormalSamples[0];
        float3 vNormalFiniteDifference1 = vNormalSamples[3] - vNormalSamples[2];
        fEdgeNormal = sqrt(dot(vNormalFiniteDifference0, vNormalFiniteDifference0) + dot(vNormalFiniteDifference1, vNormalFiniteDifference1));
        fEdgeNormal = fEdgeNormal > (1/_fNormalsSensitivity) ? 1.0f : 0.0f;
        _vOutlineColor.a = 0.25f;
    }

    // Color
    float fEdgeColor = 0.0f;
    if (true) // Need to modify code to not always do colour edge detection
    {
        float3 vColorFiniteDifference0 = vColorSamples[1] - vColorSamples[0];
        float3 vColorFiniteDifference1 = vColorSamples[3] - vColorSamples[2];
        fEdgeColor = sqrt(dot(vColorFiniteDifference0, vColorFiniteDifference0) + dot(vColorFiniteDifference1, vColorFiniteDifference1));
        fEdgeColor = fEdgeColor > (1.0f/_fColorSensitivity) ? 1.0f : 0.0f;
    }

    float fEdge = max(fEdgeDepth, max(fEdgeNormal, fEdgeColor));
    float4 vOriginal = UNITY_SAMPLE_TEX2D(_CameraColorTexture, vUVSamples[0]);	
    _vOut = ((1.0f - fEdge) * vOriginal) + (fEdge * lerp(vOriginal, _vOutlineColor,  _vOutlineColor.a));
}