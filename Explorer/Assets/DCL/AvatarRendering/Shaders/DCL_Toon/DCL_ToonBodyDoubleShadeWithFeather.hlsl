#if defined(_IS_CLIPPING_MODE) || defined(_IS_CLIPPING_TRANSMODE)
// Returns true if AlphaToMask functionality is currently available
// NOTE: This does NOT guarantee that AlphaToMask is enabled for the current draw. It only indicates that AlphaToMask functionality COULD be enabled for it.
//       In cases where AlphaToMask COULD be enabled, we export a specialized alpha value from the shader.
//       When AlphaToMask is enabled:     The specialized alpha value is combined with the sample mask
//       When AlphaToMask is not enabled: The specialized alpha value is either written into the framebuffer or dropped entirely depending on the color write mask
bool IsAlphaToMaskAvailable()
{
    return (_AlphaToMaskAvailable != 0.0);
}

// When AlphaToMask is available:     Returns a modified alpha value that should be exported from the shader so it can be combined with the sample mask
// When AlphaToMask is not available: Terminates the current invocation if the alpha value is below the cutoff and returns the input alpha value otherwise

half AlphaClip(half alpha, half cutoff)
{
    // Produce 0.0 if the input value would be clipped by traditional alpha clipping and produce the original input value otherwise.
    // WORKAROUND: The alpha parameter in this ternary expression MUST be converted to a float in order to work around a known HLSL compiler bug.
    //             See Fogbugz 934464 for more information
    half clippedAlpha = (alpha >= cutoff) ? float(alpha) : 0.0;

    // Calculate a specialized alpha value that should be used when alpha-to-coverage is enabled

    // If the user has specified zero as the cutoff threshold, the expectation is that the shader will function as if alpha-clipping was disabled.
    // Ideally, the user should just turn off the alpha-clipping feature in this case, but in order to make this case work as expected, we force alpha
    // to 1.0 here to ensure that alpha-to-coverage never throws away samples when its active. (This would cause opaque objects to appear transparent)
    half alphaToCoverageAlpha = (cutoff <= 0.0) ? 1.0 : SharpenAlpha(alpha, cutoff);

    // When alpha-to-coverage is available:     Use the specialized value which will be exported from the shader and combined with the MSAA coverage mask.
    // When alpha-to-coverage is not available: Use the "clipped" value. A clipped value will always result in thread termination via the clip() logic below.
    alpha = IsAlphaToMaskAvailable() ? alphaToCoverageAlpha : clippedAlpha;

    // Terminate any threads that have an alpha value of 0.0 since we know they won't contribute anything to the final image
    clip(alpha - 0.0001);

    return alpha;
}
#endif

float4 fragDoubleShadeFeather(VertexOutput i, half facing : VFACE) : SV_TARGET 
{
    float2 Set_UV0 = i.uv0;
    
    // if (false)
    // {
    //     int nMainTexArrID = _MainTexArr_ID;
    //     float2 uv_maintex = TRANSFORM_TEX(Set_UV0, _MainTex);
    //     float4 _MainTex_var = SAMPLE_MAINTEX(uv_maintex,nMainTexArrID);
    //
    //     int nNormalMapArrID = _NormalMapArr_ID;
    //     float3 _NormalMap_var = UnpackNormalScale(SAMPLE_NORMALMAP(TRANSFORM_TEX(Set_UV0, _NormalMap), nNormalMapArrID), _BumpScale);
    //     
    //     int n1st_ShadeMapArrID = _1st_ShadeMapArr_ID;
    //     float4 _1st_ShadeMap_var = SAMPLE_1ST_SHADEMAP(TRANSFORM_TEX(Set_UV0, _1st_ShadeMap),n1st_ShadeMapArrID);
    //
    //     int n2nd_ShadeMapArrID = _2nd_ShadeMapArr_ID;
    //     float4 _2nd_ShadeMap_var = SAMPLE_2ND_SHADEMAP(TRANSFORM_TEX(Set_UV0, _2nd_ShadeMap),n2nd_ShadeMapArrID);
    //
    //     int nSet_1st_ShadePositionArrID = _Set_1st_ShadePositionArr_ID;
    //     float4 _1st_ShadePosition_var = SAMPLE_SET_1ST_SHADEPOSITION(TRANSFORM_TEX(Set_UV0,_Set_1st_ShadePosition),nSet_1st_ShadePositionArrID);
    //
    //     int nSet_2nd_ShadePositionArrID = _Set_2nd_ShadePositionArr_ID;
    //     float4 _2nd_ShadePosition_var = SAMPLE_SET_2ND_SHADEPOSITION(TRANSFORM_TEX(Set_UV0,_Set_2nd_ShadePosition),nSet_2nd_ShadePositionArrID);
    //
    //     int nSet_RimLightMaskArrID = _Set_RimLightMaskArr_ID;
    //     float4 _RimLightMask_var = SAMPLE_SET_RIMLIGHTMASK(TRANSFORM_TEX(Set_UV0, _Set_RimLightMask), nSet_RimLightMaskArrID);
    //
    //     float2 _Rot_MatCapNmUV_var = CalculateMapCapNormalUV(   UNITY_MATRIX_V,
    //                                                             _NormalMapForMatCap_var,
    //                                                             _ViewDirection,
    //                                                             _NormalDirection,
    //                                                             _Is_NormalMapForMatCap,
    //                                                             _TangentTransform,
    //                                                             _Is_Ortho,
    //                                                             _Tweak_MatCapUV,
    //                                                             _Rotate_MatCapUV,
    //                                                             _MirrorFlag,
    //                                                             _CameraRolling_Stabilizer);
    //     int nNormalMapForMatCapArrID = _NormalMapForMatCapArr_ID;
    //     float4 _NormalMapForMatCap_var = UnpackNormalScale(SAMPLE_NORMALMAPFORMATCAP(TRANSFORM_TEX(_Rot_MatCapNmUV_var, _NormalMapForMatCap), nNormalMapForMatCapArrID), _BumpScaleMatcap);
    //
    //     int nMatCap_SamplerArrID = _MatCap_SamplerArr_ID;
    //     float4 _MatCap_Sampler_var = SAMPLE_MATCAP(TRANSFORM_TEX(_Rot_MatCapUV_var, _MatCap_Sampler), nMatCap_SamplerArrID, _BlurLevelMatcap);
    //
    //     int nSet_MatcapMaskArrID = _Set_MatcapMaskArr_ID;
    //     float4 _Set_MatcapMask_var = SAMPLE_SET_MATCAPMASK(TRANSFORM_TEX(Set_UV0, _Set_MatcapMask), nSet_MatcapMaskArrID);
    //
    //     int nEmissive_TexArrID = _Emissive_TexArr_ID;
    //     float4 _Emissive_Tex_var = SAMPLE_EMISSIVE(TRANSFORM_TEX(Set_UV0, _Emissive_Tex), nEmissive_TexArrID);
    //
    //     int nEmissive_TexArrID = _Emissive_TexArr_ID;
    //     float4 _Emissive_Tex_var = SAMPLE_EMISSIVE(TRANSFORM_TEX(Set_UV0, _Emissive_Tex), nEmissive_TexArrID);
    //     _Emissive_Tex_var = SAMPLE_EMISSIVE(TRANSFORM_TEX(_rotate_EmissiveUV_var, _Emissive_Tex), nEmissive_TexArrID);
    //
    //     int nClippingMaskArrID = _ClippingMaskArr_ID;
    //     float4 _ClippingMask_var = SAMPLE_CLIPPINGMASK(TRANSFORM_TEX(Set_UV0, _ClippingMask), nClippingMaskArrID);
    //     
    //     int nSet_HighColorMaskArrID = _Set_HighColorMaskArr_ID;
    //     float4 _Set_HighColorMask_var = SAMPLE_HIGHCOLORMASK(TRANSFORM_TEX(Set_UV0, _Set_HighColorMask), nSet_HighColorMaskArrID);
    //
    //     int nHighColor_TexArrID = _HighColor_TexArr_ID;
    //     float4 _HighColor_Tex_var = SAMPLE_HIGHCOLOR(TRANSFORM_TEX(Set_UV0, _HighColor_Tex), nHighColor_TexArrID);
    //
    //     int nSet_RimLightMaskArrID = _Set_RimLightMaskArr_ID;
    //     float4 _Set_RimLightMask_var = SAMPLE_SET_RIMLIGHTMASK(TRANSFORM_TEX(Set_UV0, _Set_RimLightMask), nSet_RimLightMaskArrID);
    // }
    
    i.normalDir = normalize(i.normalDir);
    float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);

    float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);

    
    //v.2.0.6

    int nNormalMapArrID = _NormalMapArr_ID;
    float3 _NormalMap_var = UnpackNormalScale(SAMPLE_NORMALMAP(TRANSFORM_TEX(Set_UV0, _NormalMap), nNormalMapArrID), _BumpScale);

    float3 normalLocal = _NormalMap_var.rgb;
    float3 normalDirection = normalize(mul( normalLocal, tangentTransform )); // Perturbed normals


    // todo. not necessary to calc gi factor in  shadowcaster pass.
    SurfaceData surfaceData;
    InitializeStandardLitSurfaceDataUTS(i.uv0, surfaceData);

    InputData inputData;
    Varyings  input = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    #ifdef LIGHTMAP_ON

    #else
        input.vertexSH = i.vertexSH;
    #endif
    input.uv = i.uv0;
    input.positionCS = i.pos;
    #if defined(_ADDITIONAL_LIGHTS_VERTEX) ||  (VERSION_LOWER(12, 0))  
		input.fogFactorAndVertexLight = i.fogFactorAndVertexLight;
    #else
		input.fogFactor = i.fogFactor;
    #endif

    #ifdef REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
        input.shadowCoord = i.shadowCoord;
    #endif

    #ifdef REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
        input.positionWS = i.posWorld.xyz;
    #endif

    #ifdef _NORMALMAP
        input.normalWS = half4(i.normalDir, viewDirection.x);      // xyz: normal, w: viewDir.x
        input.tangentWS = half4(i.tangentDir, viewDirection.y);        // xyz: tangent, w: viewDir.y
        #if (VERSION_LOWER(7, 5))
            input.bitangentWS = half4(i.bitangentDir, viewDirection.z);    // xyz: bitangent, w: viewDir.z
        #endif
    #else //ifdef _NORMALMAP
        input.normalWS = half3(i.normalDir);
        #if (VERSION_LOWER(12, 0))  
            input.viewDirWS = half3(viewDirection);
        #endif  //    #if (VERSION_LOWER(12, 0))  
    #endif
    
    InitializeInputData(input, surfaceData.normalTS, inputData);

    BRDFData brdfData;
    InitializeBRDFData( surfaceData.albedo,
                        surfaceData.metallic,
                        surfaceData.specular,
                        surfaceData.smoothness,
                        surfaceData.alpha, brdfData);

    half3 envColor = GlobalIlluminationUTS(brdfData, inputData.bakedGI, surfaceData.occlusion, inputData.normalWS, inputData.viewDirectionWS, i.posWorld.xyz, inputData.normalizedScreenSpaceUV);
    envColor *= 1.8f;

    UtsLight mainLight = GetMainUtsLightByID(i.mainLightID, i.posWorld.xyz, inputData.shadowCoord, i.positionCS);

    half3 mainLightColor = GetLightColor(mainLight);
    int nMainTexArrID = _MainTexArr_ID;
    float2 uv_maintex = TRANSFORM_TEX(Set_UV0, _MainTex);
    float4 _MainTex_var = SAMPLE_MAINTEX(uv_maintex,nMainTexArrID);
    
    // Clipping modes - early outs
    #if defined(_IS_CLIPPING_MODE) || defined(_IS_CLIPPING_TRANSMODE)
        float fAlphaClip = _MainTex_var.a * _BaseColor.a;
        AlphaClip(fAlphaClip, _Clipping_Level);
    #endif

    float shadowAttenuation = 1.0;
    #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        shadowAttenuation = mainLight.shadowAttenuation;
    #endif

    // Mainlight colour and default lighting colour (Unlit)
    //#ifdef _IS_PASS_FWDBASE
        float3 defaultLightDirection = normalize(UNITY_MATRIX_V[2].xyz + UNITY_MATRIX_V[1].xyz);
        float3 defaultLightColor = saturate(max(half3(0.05,0.05,0.05)*_Unlit_Intensity,max(ShadeSH9(half4(0.0, 0.0, 0.0, 1.0)),ShadeSH9(half4(0.0, -1.0, 0.0, 1.0)).rgb)*_Unlit_Intensity));
        float3 customLightDirection = normalize(mul( unity_ObjectToWorld, float4(((float3(1.0,0.0,0.0)*_Offset_X_Axis_BLD*10)+(float3(0.0,1.0,0.0)*_Offset_Y_Axis_BLD*10)+(float3(0.0,0.0,-1.0)*lerp(-1.0,1.0,_Inverse_Z_Axis_BLD))),0)).xyz);
        float3 lightDirection = normalize(lerp(defaultLightDirection, mainLight.direction.xyz,any(mainLight.direction.xyz)));
        lightDirection = lerp(lightDirection, customLightDirection, _Is_BLD);

        half3 originalLightColor = mainLightColor.rgb;

        float3 lightColor = lerp(max(defaultLightColor, originalLightColor), max(defaultLightColor, saturate(originalLightColor)), _Is_Filter_LightColor);
    //#endif

    ////// Lighting:
    float3 halfDirection = normalize(viewDirection+lightDirection);
    _Color = _BaseColor;

    //#ifdef _IS_PASS_FWDBASE
        // SHARED START
        float3 Set_LightColor = lightColor.rgb;
        float3 Set_BaseColor = lerp( (_BaseColor.rgb*_MainTex_var.rgb), ((_BaseColor.rgb*_MainTex_var.rgb)*Set_LightColor), _Is_LightColor_Base );
        
        // 1st ShadeMap
        int n1st_ShadeMapArrID = _1st_ShadeMapArr_ID;
        float4 _1st_ShadeMap_var = lerp(SAMPLE_1ST_SHADEMAP(TRANSFORM_TEX(Set_UV0, _1st_ShadeMap),n1st_ShadeMapArrID),float4(Set_BaseColor.rgb, _MainTex_var.a),_Use_BaseAs1st);
        float3 Set_1st_ShadeColor = lerp( (_1st_ShadeColor.rgb*_1st_ShadeMap_var.rgb), ((_1st_ShadeColor.rgb*_1st_ShadeMap_var.rgb)*Set_LightColor), _Is_LightColor_1st_Shade );
        // 2nd ShadeMap
        int n2nd_ShadeMapArrID = _2nd_ShadeMapArr_ID;
        float4 _2nd_ShadeMap_var = lerp(SAMPLE_2ND_SHADEMAP(TRANSFORM_TEX(Set_UV0, _2nd_ShadeMap),n2nd_ShadeMapArrID),_1st_ShadeMap_var,_Use_1stAs2nd);
        float3 Set_2nd_ShadeColor = lerp( (_2nd_ShadeColor.rgb*_2nd_ShadeMap_var.rgb), ((_2nd_ShadeColor.rgb*_2nd_ShadeMap_var.rgb)*Set_LightColor), _Is_LightColor_2nd_Shade );
        float _HalfLambert_var = 0.5*dot(lerp( i.normalDir, normalDirection, _Is_NormalMapToBase ),lightDirection)+0.5;
    
        int nSet_1st_ShadePositionArrID = _Set_1st_ShadePositionArr_ID;
        float4 _Set_1st_ShadePosition_var = SAMPLE_SET_1ST_SHADEPOSITION(TRANSFORM_TEX(Set_UV0,_Set_1st_ShadePosition),nSet_1st_ShadePositionArrID);

        int nSet_2nd_ShadePositionArrID = _Set_2nd_ShadePositionArr_ID;
        float4 _Set_2nd_ShadePosition_var = SAMPLE_SET_2ND_SHADEPOSITION(TRANSFORM_TEX(Set_UV0,_Set_2nd_ShadePosition),nSet_2nd_ShadePositionArrID);
        // SHARED END
    
        //Minmimum value is same as the Minimum Feather's value with the Minimum Step's value as threshold.
        float _SystemShadowsLevel_var = (shadowAttenuation*0.5)+0.5+_Tweak_SystemShadowsLevel > 0.001 ? (shadowAttenuation*0.5)+0.5+_Tweak_SystemShadowsLevel : 0.0001;
        float Set_FinalShadowMask = saturate((1.0 + ( (lerp( _HalfLambert_var, _HalfLambert_var*saturate(_SystemShadowsLevel_var), _Set_SystemShadowsToBase ) - (_BaseColor_Step-_BaseShade_Feather)) * ((1.0 - _Set_1st_ShadePosition_var.rgb).r - 1.0) ) / (_BaseColor_Step - (_BaseColor_Step-_BaseShade_Feather))));

        //Composition: 3 Basic Colors as Set_FinalBaseColor
        float3 Set_FinalBaseColor = lerp(Set_BaseColor,lerp(Set_1st_ShadeColor,Set_2nd_ShadeColor,saturate((1.0 + ( (_HalfLambert_var - (_ShadeColor_Step-_1st2nd_Shades_Feather)) * ((1.0 - _Set_2nd_ShadePosition_var.rgb).r - 1.0) ) / (_ShadeColor_Step - (_ShadeColor_Step-_1st2nd_Shades_Feather))))),Set_FinalShadowMask); // Final Color

        // SHARED START
        int nSet_HighColorMaskArrID = _Set_HighColorMaskArr_ID;
        float4 _Set_HighColorMask_var = SAMPLE_HIGHCOLORMASK(TRANSFORM_TEX(Set_UV0, _Set_HighColorMask), nSet_HighColorMaskArrID);

        float _Specular_var = 0.5*dot(halfDirection,lerp( i.normalDir, normalDirection, _Is_NormalMapToHighColor ))+0.5; //  Specular                
        float _TweakHighColorMask_var = (saturate((_Set_HighColorMask_var.g+_Tweak_HighColorMaskLevel))*lerp( (1.0 - step(_Specular_var,(1.0 - pow(abs(_HighColor_Power),5)))), pow(abs(_Specular_var),exp2(lerp(11,1,_HighColor_Power))), _Is_SpecularToHighColor ));
    
        int nHighColor_TexArrID = _HighColor_TexArr_ID;
        float4 _HighColor_Tex_var = SAMPLE_HIGHCOLOR(TRANSFORM_TEX(Set_UV0, _HighColor_Tex), nHighColor_TexArrID);
        float3 _HighColor_var = (lerp( (_HighColor_Tex_var.rgb*_HighColor.rgb), ((_HighColor_Tex_var.rgb*_HighColor.rgb)*Set_LightColor), _Is_LightColor_HighColor )*_TweakHighColorMask_var);
        // SHARED END
        //Composition: 3 Basic Colors and HighColor as Set_HighColor
        float3 Set_HighColor = (lerp(SATURATE_IF_SDR((Set_FinalBaseColor-_TweakHighColorMask_var)), Set_FinalBaseColor, lerp(_Is_BlendAddToHiColor,1.0,_Is_SpecularToHighColor) )+lerp( _HighColor_var, (_HighColor_var*((1.0 - Set_FinalShadowMask)+(Set_FinalShadowMask*_TweakHighColorOnShadow))), _Is_UseTweakHighColorOnShadow ));

        // Rimlight - Mainlight only
        int nSet_RimLightMaskArrID = _Set_RimLightMaskArr_ID;
        float4 _Set_RimLightMask_var = SAMPLE_SET_RIMLIGHTMASK(TRANSFORM_TEX(Set_UV0, _Set_RimLightMask), nSet_RimLightMaskArrID);
        float3 _Is_LightColor_RimLight_var = lerp( _RimLightColor.rgb, (_RimLightColor.rgb*Set_LightColor), _Is_LightColor_RimLight );
        float _RimArea_var = abs(1.0 - dot(lerp( i.normalDir, normalDirection, _Is_NormalMapToRimLight ),viewDirection));
        float _RimLightPower_var = pow(_RimArea_var,exp2(lerp(3,0,_RimLight_Power)));
        float _Rimlight_InsideMask_var = saturate(lerp( (0.0 + ( (_RimLightPower_var - _RimLight_InsideMask) * (1.0 - 0.0) ) / (1.0 - _RimLight_InsideMask)), step(_RimLight_InsideMask,_RimLightPower_var), _RimLight_FeatherOff ));
        float _VertHalfLambert_var = 0.5*dot(i.normalDir,lightDirection)+0.5;
        float3 _LightDirection_MaskOn_var = lerp( (_Is_LightColor_RimLight_var*_Rimlight_InsideMask_var), (_Is_LightColor_RimLight_var*saturate((_Rimlight_InsideMask_var-((1.0 - _VertHalfLambert_var)+_Tweak_LightDirection_MaskLevel)))), _LightDirection_MaskOn );
        float _ApRimLightPower_var = pow(_RimArea_var,exp2(lerp(3,0,_Ap_RimLight_Power)));
        float3 Set_RimLight = (saturate((_Set_RimLightMask_var.g+_Tweak_RimLightMaskLevel))*lerp( _LightDirection_MaskOn_var, (_LightDirection_MaskOn_var+(lerp( _Ap_RimLightColor.rgb, (_Ap_RimLightColor.rgb*Set_LightColor), _Is_LightColor_Ap_RimLight )*saturate((lerp( (0.0 + ( (_ApRimLightPower_var - _RimLight_InsideMask) * (1.0 - 0.0) ) / (1.0 - _RimLight_InsideMask)), step(_RimLight_InsideMask,_ApRimLightPower_var), _Ap_RimLight_FeatherOff )-(saturate(_VertHalfLambert_var)+_Tweak_LightDirection_MaskLevel))))), _Add_Antipodean_RimLight ));
        //Composition: HighColor and RimLight as _RimLight_var
        float3 _RimLight_var = lerp( Set_HighColor, (Set_HighColor+Set_RimLight), _RimLight );
        // Rimlight - End
    
        // Matcap - Mainlight only
        // CameraRolling Stabilizer
        float3 _Camera_Right = UNITY_MATRIX_V[0].xyz;
        float3 _Camera_Front = UNITY_MATRIX_V[2].xyz;
        float3 _Up_Unit = float3(0, 1, 0);
        float3 _Right_Axis = cross(_Camera_Front, _Up_Unit);
        // Invert if it's "inside the mirror".
        half _sign_Mirror = i.mirrorFlag; // Mirror Script Determination: if sign_Mirror = -1, determine "Inside the mirror".
        if(_sign_Mirror < 0)
        {
            _Right_Axis = -1 * _Right_Axis;
            _Rotate_MatCapUV = -1 * _Rotate_MatCapUV;
        }
        else
        {
            _Right_Axis = _Right_Axis;
        }
        float _Camera_Right_Magnitude = sqrt(_Camera_Right.x*_Camera_Right.x + _Camera_Right.y*_Camera_Right.y + _Camera_Right.z*_Camera_Right.z);
        float _Right_Axis_Magnitude = sqrt(_Right_Axis.x*_Right_Axis.x + _Right_Axis.y*_Right_Axis.y + _Right_Axis.z*_Right_Axis.z);
        float _Camera_Roll_Cos = dot(_Right_Axis, _Camera_Right) / (_Right_Axis_Magnitude * _Camera_Right_Magnitude);
        float _Camera_Roll = acos(clamp(_Camera_Roll_Cos, -1, 1));
        half _Camera_Dir = _Camera_Right.y < 0 ? -1 : 1;
        float _Rot_MatCapUV_var_ang = (_Rotate_MatCapUV*3.141592654) - _Camera_Dir*_Camera_Roll*_CameraRolling_Stabilizer;
        //v.2.0.7
        float2 _Rot_MatCapNmUV_var = RotateUV(Set_UV0, (_Rotate_NormalMapForMatCapUV*3.141592654), float2(0.5, 0.5), 1.0);
        //V.2.0.6
        int nNormalMapForMatCapArrID = _NormalMapForMatCapArr_ID;
        float3 _NormalMapForMatCap_var = UnpackNormalScale(SAMPLE_NORMALMAPFORMATCAP(TRANSFORM_TEX(_Rot_MatCapNmUV_var, _NormalMapForMatCap), nNormalMapForMatCapArrID), _BumpScaleMatcap);
        // MatCap with camera skew correction
        float3 viewNormal = (mul(UNITY_MATRIX_V, float4(lerp( i.normalDir, mul( _NormalMapForMatCap_var.rgb, tangentTransform ).rgb, _Is_NormalMapForMatCap ),0))).rgb;
        float3 NormalBlend_MatcapUV_Detail = viewNormal.rgb * float3(-1,-1,1);
        float3 NormalBlend_MatcapUV_Base = (mul( UNITY_MATRIX_V, float4(viewDirection,0) ).rgb*float3(-1,-1,1)) + float3(0,0,1);
        float3 noSknewViewNormal = NormalBlend_MatcapUV_Base*dot(NormalBlend_MatcapUV_Base, NormalBlend_MatcapUV_Detail)/NormalBlend_MatcapUV_Base.b - NormalBlend_MatcapUV_Detail;                
        float2 _ViewNormalAsMatCapUV = (lerp(noSknewViewNormal,viewNormal,_Is_Ortho).rg*0.5)+0.5;
        //v.2.0.7
        float2 _Rot_MatCapUV_var = RotateUV((0.0 + ((_ViewNormalAsMatCapUV - (0.0+_Tweak_MatCapUV)) * (1.0 - 0.0) ) / ((1.0-_Tweak_MatCapUV) - (0.0+_Tweak_MatCapUV))), _Rot_MatCapUV_var_ang, float2(0.5, 0.5), 1.0);

        // If it is "inside the mirror", flip the UV left and right.
        if(_sign_Mirror < 0)
        {
            _Rot_MatCapUV_var.x = 1-_Rot_MatCapUV_var.x;
        }
        else
        {
            _Rot_MatCapUV_var = _Rot_MatCapUV_var;
        }
    
        int nMatCap_SamplerArrID = _MatCap_SamplerArr_ID;
        float4 _MatCap_Sampler_var = SAMPLE_MATCAP(TRANSFORM_TEX(_Rot_MatCapUV_var, _MatCap_Sampler), nMatCap_SamplerArrID, _BlurLevelMatcap);

        int nSet_MatcapMaskArrID = _Set_MatcapMaskArr_ID;
        float4 _Set_MatcapMask_var = SAMPLE_SET_MATCAPMASK(TRANSFORM_TEX(Set_UV0, _Set_MatcapMask), nSet_MatcapMaskArrID);

        // MatcapMask
        float _Tweak_MatcapMaskLevel_var = saturate(lerp(_Set_MatcapMask_var.g, (1.0 - _Set_MatcapMask_var.g), _Inverse_MatcapMask) + _Tweak_MatcapMaskLevel);
        float3 _Is_LightColor_MatCap_var = lerp( (_MatCap_Sampler_var.rgb*_MatCapColor.rgb), ((_MatCap_Sampler_var.rgb*_MatCapColor.rgb)*Set_LightColor), _Is_LightColor_MatCap );
        // ShadowMask on Matcap in Blend mode : multiply
        float3 Set_MatCap = lerp( _Is_LightColor_MatCap_var, (_Is_LightColor_MatCap_var*((1.0 - Set_FinalShadowMask)+(Set_FinalShadowMask*_TweakMatCapOnShadow)) + lerp(Set_HighColor*Set_FinalShadowMask*(1.0-_TweakMatCapOnShadow), float3(0.0, 0.0, 0.0), _Is_BlendAddToMatCap)), _Is_UseTweakMatCapOnShadow );

        // Composition: RimLight and MatCap as finalColor
        // Broke down finalColor composition
        float3 matCapColorOnAddMode = _RimLight_var+Set_MatCap*_Tweak_MatcapMaskLevel_var;
        float _Tweak_MatcapMaskLevel_var_MultiplyMode = _Tweak_MatcapMaskLevel_var * lerp (1.0, (1.0 - (Set_FinalShadowMask)*(1.0 - _TweakMatCapOnShadow)), _Is_UseTweakMatCapOnShadow);
        float3 matCapColorOnMultiplyMode = Set_HighColor*(1-_Tweak_MatcapMaskLevel_var_MultiplyMode) + Set_HighColor*Set_MatCap*_Tweak_MatcapMaskLevel_var_MultiplyMode + lerp(float3(0,0,0),Set_RimLight,_RimLight);
        float3 matCapColorFinal = lerp(matCapColorOnMultiplyMode, matCapColorOnAddMode, _Is_BlendAddToMatCap);
        float3 finalColor = lerp(_RimLight_var, matCapColorFinal, _MatCap);// Final Composition before Emissive
        // Matcap - End

        // GI_Intensity with Intensity Multiplier Filter
        float3 envLightColor = envColor.rgb;
        float envLightIntensity = 0.299*envLightColor.r + 0.587*envLightColor.g + 0.114*envLightColor.b <1 ? (0.299*envLightColor.r + 0.587*envLightColor.g + 0.114*envLightColor.b) : 1;
        float3 pointLightColor = 0;
        #ifdef _ADDITIONAL_LIGHTS
            int pixelLightCount = GetAdditionalLightsCount();

            #if USE_FORWARD_PLUS
                for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
                    int iLight = lightIndex;
                    // if (iLight != i.mainLightID)
                    {
                        float notDirectional = 1.0f; //_WorldSpaceLightPos0.w of the legacy code.

                        UtsLight additionalLight = GetUrpMainUtsLight(0,0);
                        additionalLight = GetAdditionalUtsLight(iLight, inputData.positionWS,i.positionCS);
                        half3 additionalLightColor = GetLightColor(additionalLight);
                        //					attenuation = light.distanceAttenuation; 


                        float3 lightDirection = additionalLight.direction;
                        //v.2.0.5: 
                        float3 addPassLightColor = (0.5*dot(lerp(i.normalDir, normalDirection, _Is_NormalMapToBase), lightDirection) + 0.5) * additionalLightColor.rgb;
                        float  pureIntencity = max(0.001, (0.299*additionalLightColor.r + 0.587*additionalLightColor.g + 0.114*additionalLightColor.b));
                        float3 lightColor = max(float3(0.0,0.0,0.0), lerp(addPassLightColor, lerp(float3(0.0,0.0,0.0), min(addPassLightColor, addPassLightColor / pureIntencity), notDirectional), _Is_Filter_LightColor));
                        float3 halfDirection = normalize(viewDirection + lightDirection); // has to be recalced here.

                        //v.2.0.5:
                        float baseColorStep = saturate(_BaseColor_Step + _StepOffset);
                        float shadeColorStep = saturate(_ShadeColor_Step + _StepOffset);
                        //
                        //v.2.0.5: If Added lights is directional, set 0 as _LightIntensity
                        float _LightIntensity = lerp(0, (0.299*additionalLightColor.r + 0.587*additionalLightColor.g + 0.114*additionalLightColor.b), notDirectional);
                        //v.2.0.5: Filtering the high intensity zone of PointLights
                        float3 Set_LightColor = lightColor;
                        //
                        float3 Set_BaseColor = lerp((_BaseColor.rgb*_MainTex_var.rgb*_LightIntensity), ((_BaseColor.rgb*_MainTex_var.rgb)*Set_LightColor), _Is_LightColor_Base);
                        //v.2.0.5
                        int n1st_ShadeMapArrID = _1st_ShadeMapArr_ID;
                        float4 _1st_ShadeMap_var = lerp(SAMPLE_1ST_SHADEMAP(TRANSFORM_TEX(Set_UV0, _1st_ShadeMap), n1st_ShadeMapArrID), float4(Set_BaseColor.rgb, _MainTex_var.a), _Use_BaseAs1st);
                        float3 Set_1st_ShadeColor = lerp((_1st_ShadeColor.rgb*_1st_ShadeMap_var.rgb*_LightIntensity), ((_1st_ShadeColor.rgb*_1st_ShadeMap_var.rgb)*Set_LightColor), _Is_LightColor_1st_Shade);
                        //v.2.0.5
                        int n2nd_ShadeMapArrID = _2nd_ShadeMapArr_ID;
                        float4 _2nd_ShadeMap_var = lerp(SAMPLE_2ND_SHADEMAP(TRANSFORM_TEX(Set_UV0, _2nd_ShadeMap), n2nd_ShadeMapArrID), _1st_ShadeMap_var, _Use_1stAs2nd);
                        float3 Set_2nd_ShadeColor = lerp((_2nd_ShadeColor.rgb*_2nd_ShadeMap_var.rgb*_LightIntensity), ((_2nd_ShadeColor.rgb*_2nd_ShadeMap_var.rgb)*Set_LightColor), _Is_LightColor_2nd_Shade);
                        float _HalfLambert_var = 0.5*dot(lerp(i.normalDir, normalDirection, _Is_NormalMapToBase), lightDirection) + 0.5;


                        int nSet_2nd_ShadePositionArrID = _Set_2nd_ShadePositionArr_ID;
                        float4 _Set_2nd_ShadePosition_var = SAMPLE_SET_2ND_SHADEPOSITION(TRANSFORM_TEX(Set_UV0, _Set_2nd_ShadePosition), nSet_2nd_ShadePositionArrID);
                        
                        int nSet_1st_ShadePositionArrID = _Set_1st_ShadePositionArr_ID;
                        float4 _Set_1st_ShadePosition_var = SAMPLE_SET_1ST_SHADEPOSITION(TRANSFORM_TEX(Set_UV0, _Set_1st_ShadePosition), nSet_1st_ShadePositionArrID);

                        //v.2.0.5:
                        float Set_FinalShadowMask = saturate((1.0 + ((lerp(_HalfLambert_var, (_HalfLambert_var*saturate(1.0 + _Tweak_SystemShadowsLevel)), _Set_SystemShadowsToBase) - (baseColorStep - _BaseShade_Feather)) * ((1.0 - _Set_1st_ShadePosition_var.rgb).r - 1.0)) / (baseColorStep - (baseColorStep - _BaseShade_Feather))));
                        //Composition: 3 Basic Colors as finalColor
                        float3 finalColor = lerp(Set_BaseColor, lerp(Set_1st_ShadeColor, Set_2nd_ShadeColor, saturate((1.0 + ((_HalfLambert_var - (shadeColorStep - _1st2nd_Shades_Feather)) * ((1.0 - _Set_2nd_ShadePosition_var.rgb).r - 1.0)) / (shadeColorStep - (shadeColorStep - _1st2nd_Shades_Feather))))), Set_FinalShadowMask); // Final Color

                        //v.2.0.6: Add HighColor if _Is_Filter_HiCutPointLightColor is False
                        int nSet_HighColorMaskArrID = _Set_HighColorMaskArr_ID;
                        float4 _Set_HighColorMask_var = SAMPLE_HIGHCOLORMASK(TRANSFORM_TEX(Set_UV0, _Set_HighColorMask), nSet_HighColorMaskArrID);
                        float _Specular_var = 0.5*dot(halfDirection, lerp(i.normalDir, normalDirection, _Is_NormalMapToHighColor)) + 0.5; //  Specular                
                        float _TweakHighColorMask_var = (saturate((_Set_HighColorMask_var.g + _Tweak_HighColorMaskLevel))*lerp((1.0 - step(_Specular_var, (1.0 - pow(_HighColor_Power, 5)))), pow(_Specular_var, exp2(lerp(11, 1, _HighColor_Power))), _Is_SpecularToHighColor));

                        int nHighColor_TeArrID = _HighColor_TexArr_ID;
                        float4 _HighColor_Tex_var = SAMPLE_HIGHCOLOR(TRANSFORM_TEX(Set_UV0, _HighColor_Tex), nHighColor_TeArrID);
                        float3 _HighColor_var = (lerp((_HighColor_Tex_var.rgb*_HighColor.rgb), ((_HighColor_Tex_var.rgb*_HighColor.rgb)*Set_LightColor), _Is_LightColor_HighColor)*_TweakHighColorMask_var);
                        finalColor = finalColor + lerp(lerp(_HighColor_var, (_HighColor_var*((1.0 - Set_FinalShadowMask) + (Set_FinalShadowMask*_TweakHighColorOnShadow))), _Is_UseTweakHighColorOnShadow), float3(0, 0, 0), _Is_Filter_HiCutPointLightColor);
                        finalColor = SATURATE_IF_SDR(finalColor);
                        pointLightColor += finalColor;
                    }
                }
            #endif  // USE_FORWARD_PLUS

            // determine main light inorder to apply light culling
            // when the loop counter start from negative value, MAINLIGHT_IS_MAINLIGHT = -1, some compiler doesn't work well.
            // for (int iLight = MAINLIGHT_IS_MAINLIGHT; iLight < pixelLightCount ; ++iLight)
            UTS_LIGHT_LOOP_BEGIN(pixelLightCount - MAINLIGHT_IS_MAINLIGHT)
            #if USE_FORWARD_PLUS
                int iLight = lightIndex;
            #else
                int iLight = loopCounter + MAINLIGHT_IS_MAINLIGHT;
                if (iLight != i.mainLightID)
            #endif
                {
                    float notDirectional = 1.0f; //_WorldSpaceLightPos0.w of the legacy code.

                    UtsLight additionalLight = GetUrpMainUtsLight(0,0);
                    if (iLight != -1)
                    {
                        additionalLight = GetAdditionalUtsLight(iLight, inputData.positionWS,i.positionCS);
                    }
                    half3 additionalLightColor = GetLightColor(additionalLight);
                    //					attenuation = light.distanceAttenuation; 


                    float3 lightDirection = additionalLight.direction;
                    //v.2.0.5: 
                    float3 addPassLightColor = (0.5*dot(lerp(i.normalDir, normalDirection, _Is_NormalMapToBase), lightDirection) + 0.5) * additionalLightColor.rgb;
                    float  pureIntencity = max(0.001, (0.299*additionalLightColor.r + 0.587*additionalLightColor.g + 0.114*additionalLightColor.b));
                    float3 lightColor = max(float3(0.0,0.0,0.0), lerp(addPassLightColor, lerp(float3(0.0,0.0,0.0), min(addPassLightColor, addPassLightColor / pureIntencity), notDirectional), _Is_Filter_LightColor));
                    float3 halfDirection = normalize(viewDirection + lightDirection); // has to be recalced here.

                    //v.2.0.5:
                    float baseColorStep = saturate(_BaseColor_Step + _StepOffset);
                    float shadeColorStep = saturate(_ShadeColor_Step + _StepOffset);
                    //
                    //v.2.0.5: If Added lights is directional, set 0 as _LightIntensity
                    float _LightIntensity = lerp(0, (0.299*additionalLightColor.r + 0.587*additionalLightColor.g + 0.114*additionalLightColor.b), notDirectional);
                    //v.2.0.5: Filtering the high intensity zone of PointLights
                    float3 Set_LightColor = lightColor;
                    //
                    float3 Set_BaseColor = lerp((_BaseColor.rgb*_MainTex_var.rgb*_LightIntensity), ((_BaseColor.rgb*_MainTex_var.rgb)*Set_LightColor), _Is_LightColor_Base);
                    //v.2.0.5
                    int n1st_ShadeMapArrID = _1st_ShadeMapArr_ID;
                    float4 _1st_ShadeMap_var = lerp(SAMPLE_1ST_SHADEMAP(TRANSFORM_TEX(Set_UV0, _1st_ShadeMap), n1st_ShadeMapArrID), float4(Set_BaseColor.rgb, _MainTex_var.a), _Use_BaseAs1st);
                    float3 Set_1st_ShadeColor = lerp((_1st_ShadeColor.rgb*_1st_ShadeMap_var.rgb*_LightIntensity), ((_1st_ShadeColor.rgb*_1st_ShadeMap_var.rgb)*Set_LightColor), _Is_LightColor_1st_Shade);
                    //v.2.0.5
                    int n2nd_ShadeMapArrID = _2nd_ShadeMapArr_ID;
                    float4 _2nd_ShadeMap_var = lerp(SAMPLE_2ND_SHADEMAP(TRANSFORM_TEX(Set_UV0, _2nd_ShadeMap), n2nd_ShadeMapArrID), _1st_ShadeMap_var, _Use_1stAs2nd);
                    float3 Set_2nd_ShadeColor = lerp((_2nd_ShadeColor.rgb*_2nd_ShadeMap_var.rgb*_LightIntensity), ((_2nd_ShadeColor.rgb*_2nd_ShadeMap_var.rgb)*Set_LightColor), _Is_LightColor_2nd_Shade);
                    float _HalfLambert_var = 0.5*dot(lerp(i.normalDir, normalDirection, _Is_NormalMapToBase), lightDirection) + 0.5;
                    
                    int nSet_2nd_ShadePositionArrID = _Set_2nd_ShadePositionArr_ID;
                    float4 _Set_2nd_ShadePosition_var = SAMPLE_SET_2ND_SHADEPOSITION(TRANSFORM_TEX(Set_UV0, _Set_2nd_ShadePosition), nSet_2nd_ShadePositionArrID);

                    int nSet_1st_ShadePositionArrID = _Set_1st_ShadePositionArr_ID;
                    float4 _Set_1st_ShadePosition_var = SAMPLE_SET_1ST_SHADEPOSITION(TRANSFORM_TEX(Set_UV0, _Set_1st_ShadePosition), nSet_1st_ShadePositionArrID);

                    //v.2.0.5:
                    float Set_FinalShadowMask = saturate((1.0 + ((lerp(_HalfLambert_var, (_HalfLambert_var*saturate(1.0 + _Tweak_SystemShadowsLevel)), _Set_SystemShadowsToBase) - (baseColorStep - _BaseShade_Feather)) * ((1.0 - _Set_1st_ShadePosition_var.rgb).r - 1.0)) / (baseColorStep - (baseColorStep - _BaseShade_Feather))));
                    //Composition: 3 Basic Colors as finalColor
                    float3 finalColor = lerp(Set_BaseColor, lerp(Set_1st_ShadeColor, Set_2nd_ShadeColor, saturate((1.0 + ((_HalfLambert_var - (shadeColorStep - _1st2nd_Shades_Feather)) * ((1.0 - _Set_2nd_ShadePosition_var.rgb).r - 1.0)) / (shadeColorStep - (shadeColorStep - _1st2nd_Shades_Feather))))), Set_FinalShadowMask); // Final Color

                    //v.2.0.6: Add HighColor if _Is_Filter_HiCutPointLightColor is False
                    int nSet_HighColorMaskArrID = _Set_HighColorMaskArr_ID;
                    float4 _Set_HighColorMask_var = SAMPLE_HIGHCOLORMASK(TRANSFORM_TEX(Set_UV0, _Set_HighColorMask), nSet_HighColorMaskArrID);

                    float _Specular_var = 0.5*dot(halfDirection, lerp(i.normalDir, normalDirection, _Is_NormalMapToHighColor)) + 0.5; //  Specular                
                    float _TweakHighColorMask_var = (saturate((_Set_HighColorMask_var.g + _Tweak_HighColorMaskLevel))*lerp((1.0 - step(_Specular_var, (1.0 - pow(_HighColor_Power, 5)))), pow(_Specular_var, exp2(lerp(11, 1, _HighColor_Power))), _Is_SpecularToHighColor));
                    
                    int nHighColor_TexArrID = _HighColor_TexArr_ID;
                    float4 _HighColor_Tex_var = SAMPLE_HIGHCOLOR(TRANSFORM_TEX(Set_UV0, _HighColor_Tex), nHighColor_TexArrID);
                    float3 _HighColor_var = (lerp((_HighColor_Tex_var.rgb*_HighColor.rgb), ((_HighColor_Tex_var.rgb*_HighColor.rgb)*Set_LightColor), _Is_LightColor_HighColor)*_TweakHighColorMask_var);
                    finalColor = finalColor + lerp(lerp(_HighColor_var, (_HighColor_var*((1.0 - Set_FinalShadowMask) + (Set_FinalShadowMask*_TweakHighColorOnShadow))), _Is_UseTweakHighColorOnShadow), float3(0, 0, 0), _Is_Filter_HiCutPointLightColor);
                    finalColor = SATURATE_IF_SDR(finalColor);
                    pointLightColor += finalColor;
                    //	pointLightColor += lightColor;
                }
                UTS_LIGHT_LOOP_END
        #endif


        //v.2.0.7
        #ifdef _EMISSIVE_SIMPLE
            int nEmissive_TexArrID = _Emissive_TexArr_ID;
            float4 _Emissive_Tex_var = SAMPLE_EMISSIVE(TRANSFORM_TEX(Set_UV0, _Emissive_Tex), nEmissive_TexArrID);
            float emissiveMask = _Emissive_Tex_var.a;
            emissive = _Emissive_Tex_var.rgb * _Emissive_Color.rgb * emissiveMask;
        #elif _EMISSIVE_ANIMATION
            //v.2.0.7 Calculation View Coord UV for Scroll 
            float3 viewNormal_Emissive = (mul(UNITY_MATRIX_V, float4(i.normalDir,0))).xyz;
            float3 NormalBlend_Emissive_Detail = viewNormal_Emissive * float3(-1,-1,1);
            float3 NormalBlend_Emissive_Base = (mul( UNITY_MATRIX_V, float4(viewDirection,0)).xyz*float3(-1,-1,1)) + float3(0,0,1);
            float3 noSknewViewNormal_Emissive = NormalBlend_Emissive_Base*dot(NormalBlend_Emissive_Base, NormalBlend_Emissive_Detail)/NormalBlend_Emissive_Base.z - NormalBlend_Emissive_Detail;
            float2 _ViewNormalAsEmissiveUV = noSknewViewNormal_Emissive.xy*0.5+0.5;
            float2 _ViewCoord_UV = RotateUV(_ViewNormalAsEmissiveUV, -(_Camera_Dir*_Camera_Roll), float2(0.5,0.5), 1.0);
            //Invert if it's "inside the mirror".
            if(_sign_Mirror < 0){
                _ViewCoord_UV.x = 1-_ViewCoord_UV.x;
            }else{
                _ViewCoord_UV = _ViewCoord_UV;
            }
            float2 emissive_uv = lerp(i.uv0, _ViewCoord_UV, _Is_ViewCoord_Scroll);
            //
            float4 _time_var = _Time;
            float _base_Speed_var = (_time_var.g*_Base_Speed);
            float _Is_PingPong_Base_var = lerp(_base_Speed_var, sin(_base_Speed_var), _Is_PingPong_Base );
            float2 scrolledUV = emissive_uv - float2(_Scroll_EmissiveU, _Scroll_EmissiveV)*_Is_PingPong_Base_var;
            float rotateVelocity = _Rotate_EmissiveUV*3.141592654;
            float2 _rotate_EmissiveUV_var = RotateUV(scrolledUV, rotateVelocity, float2(0.5, 0.5), _Is_PingPong_Base_var);

            int nEmissive_TexArrID = _Emissive_TexArr_ID;
            float4 _Emissive_Tex_var = SAMPLE_EMISSIVE(TRANSFORM_TEX(Set_UV0, _Emissive_Tex), nEmissive_TexArrID);
            float emissiveMask = _Emissive_Tex_var.a;
    
            _Emissive_Tex_var = SAMPLE_EMISSIVE(TRANSFORM_TEX(_rotate_EmissiveUV_var, _Emissive_Tex), nEmissive_TexArrID);
            float _colorShift_Speed_var = 1.0 - cos(_time_var.g*_ColorShift_Speed);
            float viewShift_var = smoothstep( 0.0, 1.0, max(0,dot(normalDirection,viewDirection)));
            float4 colorShift_Color = lerp(_Emissive_Color, lerp(_Emissive_Color, _ColorShift, _colorShift_Speed_var), _Is_ColorShift);
            float4 viewShift_Color = lerp(_ViewShift, colorShift_Color, viewShift_var);
            float4 emissive_Color = lerp(colorShift_Color, viewShift_Color, _Is_ViewShift);
            emissive = emissive_Color.rgb * _Emissive_Tex_var.rgb * emissiveMask;
        #endif

        //Final Composition#if 
        finalColor = SATURATE_IF_SDR(finalColor) + (envLightColor*envLightIntensity*_GI_Intensity*smoothstep(1,0,envLightIntensity/2)) + emissive;
        
        finalColor += pointLightColor;
    //#endif
    
    #ifdef _IS_CLIPPING_OFF
        half4 finalRGBA = half4(finalColor,1);
    #elif _IS_CLIPPING_MODE || _IS_CLIPPING_TRANSMODE
        float Set_Opacity = SATURATE_IF_SDR((_MainTex_var.a+_Tweak_transparency));
        half4 finalRGBA = half4(finalColor,Set_Opacity);
    #endif
    
    return finalRGBA;
}
