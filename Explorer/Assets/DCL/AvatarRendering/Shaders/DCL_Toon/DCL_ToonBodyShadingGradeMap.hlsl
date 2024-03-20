float4 fragShadingGradeMap(VertexOutput i, half facing : VFACE) : SV_TARGET
{
    i.normalDir = normalize(i.normalDir);
    float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);
    float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
    float2 Set_UV0 = i.uv0;
    //v.2.0.6
    int nNormalMapArrID = _NormalMapArr_ID;
    float3 _NormalMap_var = UnpackNormalScale(SAMPLE_NORMALMAP(TRANSFORM_TEX(Set_UV0, _NormalMap),nNormalMapArrID), _BumpScale);
    float3 normalLocal = _NormalMap_var.rgb;
    float3 normalDirection = normalize(mul( normalLocal, tangentTransform )); // Perturbed normals
    
    // todo. not necessary to calc gi factor in  shadowcaster pass.
    SurfaceData surfaceData;
    InitializeStandardLitSurfaceDataUTS(i.uv0, surfaceData);

    InputData inputData;
    Varyings  input = (Varyings)0;

    // todo.  it has to be cared more.
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    # ifdef LIGHTMAP_ON

    # else
        input.vertexSH = i.vertexSH;
    # endif
    
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
        #endif //
    #else
        input.normalWS = half3(i.normalDir);
        #if (VERSION_LOWER(12, 0))
            input.viewDirWS = half3(viewDirection);
        #endif //(VERSION_LOWER(12, 0))
    #endif
    
    InitializeInputData(input, surfaceData.normalTS, inputData);

    BRDFData brdfData;
    InitializeBRDFData(surfaceData.albedo,
        surfaceData.metallic,
        surfaceData.specular,
        surfaceData.smoothness,
        surfaceData.alpha, brdfData);

    half3 envColor = GlobalIlluminationUTS(brdfData, inputData.bakedGI, surfaceData.occlusion, inputData.normalWS, inputData.viewDirectionWS, i.posWorld.xyz, inputData.normalizedScreenSpaceUV);
    envColor *= 1.8f;

    UtsLight mainLight = GetMainUtsLightByID(i.mainLightID, i.posWorld.xyz, inputData.shadowCoord, i.positionCS);
    half3 mainLightColor = GetLightColor(mainLight);

    int nMainTexArrID = _MainTexArr_ID;
    float4 _MainTex_var = SAMPLE_MAINTEX(TRANSFORM_TEX(Set_UV0, _MainTex), nMainTexArrID);

    //v.2.0.4
    #ifdef _IS_TRANSCLIPPING_OFF

    #elif _IS_TRANSCLIPPING_ON
        int nClippingMaskArrID = _ClippingMaskArr_ID;
        float4 _ClippingMask_var = SAMPLE_CLIPPINGMASK(TRANSFORM_TEX(Set_UV0, _ClippingMask), nClippingMaskArrID);
        float Set_MainTexAlpha = _MainTex_var.a;
        float _IsBaseMapAlphaAsClippingMask_var = lerp( _ClippingMask_var.r, Set_MainTexAlpha, _IsBaseMapAlphaAsClippingMask );
        float _Inverse_Clipping_var = lerp( _IsBaseMapAlphaAsClippingMask_var, (1.0 - _IsBaseMapAlphaAsClippingMask_var), _Inverse_Clipping );
        float Set_Clipping = saturate((_Inverse_Clipping_var+_Clipping_Level));
        clip(Set_Clipping - 0.5);
    #endif
    
    float shadowAttenuation = 1.0;
    #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        shadowAttenuation = mainLight.shadowAttenuation;
    #endif
    
    //v.2.0.4
    float3 defaultLightDirection = normalize(UNITY_MATRIX_V[2].xyz + UNITY_MATRIX_V[1].xyz);
    //v.2.0.5
    float3 defaultLightColor = saturate(max(half3(0.05,0.05,0.05)*_Unlit_Intensity,max(ShadeSH9(half4(0.0, 0.0, 0.0, 1.0)),ShadeSH9(half4(0.0, -1.0, 0.0, 1.0)).rgb)*_Unlit_Intensity));
    float3 customLightDirection = normalize(mul( unity_ObjectToWorld, float4(((float3(1.0,0.0,0.0)*_Offset_X_Axis_BLD*10)+(float3(0.0,1.0,0.0)*_Offset_Y_Axis_BLD*10)+(float3(0.0,0.0,-1.0)*lerp(-1.0,1.0,_Inverse_Z_Axis_BLD))),0)).xyz);
    float3 lightDirection = normalize(lerp(defaultLightDirection, mainLight.direction.xyz,any(mainLight.direction.xyz)));
    lightDirection = lerp(lightDirection, customLightDirection, _Is_BLD);
    //v.2.0.5: 
    half3 originalLightColor = mainLightColor.rgb;
    float3 lightColor = lerp(max(defaultLightColor, originalLightColor), max(defaultLightColor, saturate(originalLightColor)), _Is_Filter_LightColor);
    
    ////// Lighting:
    float3 halfDirection = normalize(viewDirection+lightDirection);
    //v.2.0.5
    _Color = _BaseColor;

    //#ifdef _IS_PASS_FWDBASE
        float3 Set_LightColor = lightColor.rgb;
        float3 Set_BaseColor = lerp( (_MainTex_var.rgb*_BaseColor.rgb), ((_MainTex_var.rgb*_BaseColor.rgb)*Set_LightColor), _Is_LightColor_Base );
        //v.2.0.5
        int n1st_ShadeMapArrID = _1st_ShadeMapArr_ID;
        float4 _1st_ShadeMap_var = lerp(SAMPLE_1ST_SHADEMAP(TRANSFORM_TEX(Set_UV0, _1st_ShadeMap), n1st_ShadeMapArrID),_MainTex_var,_Use_BaseAs1st);
        float3 _Is_LightColor_1st_Shade_var = lerp( (_1st_ShadeMap_var.rgb*_1st_ShadeColor.rgb), ((_1st_ShadeMap_var.rgb*_1st_ShadeColor.rgb)*Set_LightColor), _Is_LightColor_1st_Shade );
        float _HalfLambert_var = 0.5*dot(lerp( i.normalDir, normalDirection, _Is_NormalMapToBase ),lightDirection)+0.5; // Half Lambert

        //v.2.0.6
        int nShadingGradeMapArrID = _ShadingGradeMapArr_ID;
        float4 _ShadingGradeMap_var = SAMPLE_SHADINGGRADEMAP(TRANSFORM_TEX(Set_UV0, _ShadingGradeMap), nShadingGradeMapArrID, _BlurLevelSGM));

        //the value of shadowAttenuation is darker than legacy and it cuases noise in terminaters.
        #if !defined (UTS_USE_RAYTRACING_SHADOW)
            shadowAttenuation *= 2.0f;
            shadowAttenuation = saturate(shadowAttenuation);
        #endif

        //v.2.0.6
        //Minmimum value is same as the Minimum Feather's value with the Minimum Step's value as threshold.
        float _SystemShadowsLevel_var = (shadowAttenuation *0.5)+0.5+_Tweak_SystemShadowsLevel > 0.001 ? (shadowAttenuation *0.5)+0.5+_Tweak_SystemShadowsLevel : 0.0001;
        float _ShadingGradeMapLevel_var = _ShadingGradeMap_var.r < 0.95 ? _ShadingGradeMap_var.r+_Tweak_ShadingGradeMapLevel : 1;
        float Set_ShadingGrade = saturate(_ShadingGradeMapLevel_var)*lerp( _HalfLambert_var, (_HalfLambert_var*saturate(_SystemShadowsLevel_var)), _Set_SystemShadowsToBase );
        //float Set_ShadingGrade = saturate(_ShadingGradeMapLevel_var)*lerp( _HalfLambert_var, (_HalfLambert_var*saturate(1.0+_Tweak_SystemShadowsLevel)), _Set_SystemShadowsToBase );
        float Set_FinalShadowMask = saturate((1.0 + ( (Set_ShadingGrade - (_1st_ShadeColor_Step-_1st_ShadeColor_Feather)) * (0.0 - 1.0) ) / (_1st_ShadeColor_Step - (_1st_ShadeColor_Step-_1st_ShadeColor_Feather)))); // Base and 1st Shade Mask
        float3 _BaseColor_var = lerp(Set_BaseColor,_Is_LightColor_1st_Shade_var,Set_FinalShadowMask);
        //v.2.0.5
        int n2nd_ShadeMapArrID = _2nd_ShadeMapArr_ID;
        float4 _2nd_ShadeMap_var = lerp(SAMPLE_1ST_SHADEMAP(TRANSFORM_TEX(Set_UV0, _2nd_ShadeMap), n2nd_ShadeMapArrID),_1st_ShadeMap_var,_Use_1stAs2nd);
        float Set_ShadeShadowMask = saturate((1.0 + ( (Set_ShadingGrade - (_2nd_ShadeColor_Step-_2nd_ShadeColor_Feather)) * (0.0 - 1.0) ) / (_2nd_ShadeColor_Step - (_2nd_ShadeColor_Step-_2nd_ShadeColor_Feather)))); // 1st and 2nd Shades Mask
        //Composition: 3 Basic Colors as Set_FinalBaseColor
        float3 Set_FinalBaseColor = lerp(_BaseColor_var,lerp(_Is_LightColor_1st_Shade_var,lerp( (_2nd_ShadeMap_var.rgb*_2nd_ShadeColor.rgb), ((_2nd_ShadeMap_var.rgb*_2nd_ShadeColor.rgb)*Set_LightColor), _Is_LightColor_2nd_Shade ),Set_ShadeShadowMask),Set_FinalShadowMask);

        int nSet_HighColorMaskArrID = _Set_HighColorMaskArr_ID;
        float4 _Set_HighColorMask_var = SAMPLE_HIGHCOLORMASK(TRANSFORM_TEX(Set_UV0, _Set_HighColorMask), nSet_HighColorMaskArrID);

        float _Specular_var = 0.5*dot(halfDirection,lerp( i.normalDir, normalDirection, _Is_NormalMapToHighColor ))+0.5; // Specular
        float _TweakHighColorMask_var = (saturate((_Set_HighColorMask_var.g+_Tweak_HighColorMaskLevel))*lerp( (1.0 - step(_Specular_var,(1.0 - pow(abs(_HighColor_Power),5)))), pow(abs(_Specular_var),exp2(lerp(11,1,_HighColor_Power))), _Is_SpecularToHighColor ));

        int nHighColor_TexArr_ID = _HighColor_TexArr_ID;
        float4 _HighColor_Tex_var = SAMPLE_HIGHCOLOR(TRANSFORM_TEX(Set_UV0, _HighColor_Tex), nHighColor_TexArr_ID);

        float3 _HighColor_var = (lerp( (_HighColor_Tex_var.rgb*_HighColor.rgb), ((_HighColor_Tex_var.rgb*_HighColor.rgb)*Set_LightColor), _Is_LightColor_HighColor )*_TweakHighColorMask_var);
        //Composition: 3 Basic Colors and HighColor as Set_HighColor
        float3 Set_HighColor = (lerp(SATURATE_IF_SDR((Set_FinalBaseColor-_TweakHighColorMask_var)), Set_FinalBaseColor, lerp(_Is_BlendAddToHiColor,1.0,_Is_SpecularToHighColor) )+lerp( _HighColor_var, (_HighColor_var*((1.0 - Set_FinalShadowMask)+(Set_FinalShadowMask*_TweakHighColorOnShadow))), _Is_UseTweakHighColorOnShadow ));

        int nSet_RimLightMaskArr_ID = _Set_RimLightMaskArr_ID;
        float4 _Set_RimLightMask_var = SAMPLE_SET_RIMLIGHTMASK(TRANSFORM_TEX(Set_UV0, _Set_RimLightMask), nSet_RimLightMaskArr_ID);

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
        //Matcap
        //v.2.0.6 : CameraRolling Stabilizer
        //Mirror Script Determination: if sign_Mirror = -1, determine "Inside the mirror".
        //v.2.0.7
        fixed _sign_Mirror = i.mirrorFlag;
        float3 _Camera_Right = UNITY_MATRIX_V[0].xyz;
        float3 _Camera_Front = UNITY_MATRIX_V[2].xyz;
        float3 _Up_Unit = float3(0, 1, 0);
        float3 _Right_Axis = cross(_Camera_Front, _Up_Unit);
        //Invert if it's "inside the mirror".
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
        fixed _Camera_Dir = _Camera_Right.y < 0 ? -1 : 1;
        float _Rot_MatCapUV_var_ang = (_Rotate_MatCapUV*3.141592654) - _Camera_Dir*_Camera_Roll*_CameraRolling_Stabilizer;
        //v.2.0.7
        float2 _Rot_MatCapNmUV_var = RotateUV(Set_UV0, (_Rotate_NormalMapForMatCapUV*3.141592654), float2(0.5, 0.5), 1.0);
        //V.2.0.6
        int nNormalMapForMatCapArrID = _NormalMapForMatCapArr_ID;
        float3 _NormalMapForMatCap_var = UnpackNormalScale(SAMPLE_NORMALMAPFORMATCAP( TRANSFORM_TEX(_Rot_MatCapNmUV_var, _NormalMapForMatCap), nNormalMapForMatCapArrID), _BumpScaleMatcap);

        //v.2.0.5: MatCap with camera skew correction
        float3 viewNormal = (mul(UNITY_MATRIX_V, float4(lerp( i.normalDir, mul( _NormalMapForMatCap_var.rgb, tangentTransform ).rgb, _Is_NormalMapForMatCap ),0))).rgb;
        float3 NormalBlend_MatcapUV_Detail = viewNormal.rgb * float3(-1,-1,1);
        float3 NormalBlend_MatcapUV_Base = (mul( UNITY_MATRIX_V, float4(viewDirection,0) ).rgb*float3(-1,-1,1)) + float3(0,0,1);
        float3 noSknewViewNormal = NormalBlend_MatcapUV_Base*dot(NormalBlend_MatcapUV_Base, NormalBlend_MatcapUV_Detail)/NormalBlend_MatcapUV_Base.b - NormalBlend_MatcapUV_Detail;                
        float2 _ViewNormalAsMatCapUV = (lerp(noSknewViewNormal,viewNormal,_Is_Ortho).rg*0.5)+0.5;
        //
        //v.2.0.7
        float2 _Rot_MatCapUV_var = RotateUV((0.0 + ((_ViewNormalAsMatCapUV - (0.0+_Tweak_MatCapUV)) * (1.0 - 0.0) ) / ((1.0-_Tweak_MatCapUV) - (0.0+_Tweak_MatCapUV))), _Rot_MatCapUV_var_ang, float2(0.5, 0.5), 1.0);
        //If it is "inside the mirror", flip the UV left and right.

        if(_sign_Mirror < 0)
        {
            _Rot_MatCapUV_var.x = 1-_Rot_MatCapUV_var.x;
        }
        else
        {
            _Rot_MatCapUV_var = _Rot_MatCapUV_var;
        }

        int nMatCap_SamplerArr_ID = _MatCap_SamplerArr_ID;
        float4 _MatCap_Sampler_var = SAMPLE_MATCAP(TRANSFORM_TEX(_Rot_MatCapUV_var, _MatCap_Sampler), nMatCap_SamplerArr_ID, _BlurLevelMatcap));
        int nSet_MatcapMaskArr_ID = _Set_MatcapMaskArr_ID;
        float4 _Set_MatcapMask_var = SAMPLE_SET_MATCAPMASK(TRANSFORM_TEX(Set_UV0, _Set_MatcapMask), nSet_MatcapMaskArr_ID);

        //MatcapMask
        float _Tweak_MatcapMaskLevel_var = saturate(lerp(_Set_MatcapMask_var.g, (1.0 - _Set_MatcapMask_var.g), _Inverse_MatcapMask) + _Tweak_MatcapMaskLevel);
        float3 _Is_LightColor_MatCap_var = lerp( (_MatCap_Sampler_var.rgb*_MatCapColor.rgb), ((_MatCap_Sampler_var.rgb*_MatCapColor.rgb)*Set_LightColor), _Is_LightColor_MatCap );
        //v.2.0.6 : ShadowMask on Matcap in Blend mode : multiply
        float3 Set_MatCap = lerp( _Is_LightColor_MatCap_var, (_Is_LightColor_MatCap_var*((1.0 - Set_FinalShadowMask)+(Set_FinalShadowMask*_TweakMatCapOnShadow)) + lerp(Set_HighColor*Set_FinalShadowMask*(1.0-_TweakMatCapOnShadow), float3(0.0, 0.0, 0.0), _Is_BlendAddToMatCap)), _Is_UseTweakMatCapOnShadow );
    
        //v.2.0.6
        //Composition: RimLight and MatCap as finalColor
        //Broke down finalColor composition
        float3 matCapColorOnAddMode = _RimLight_var+Set_MatCap*_Tweak_MatcapMaskLevel_var;
        float _Tweak_MatcapMaskLevel_var_MultiplyMode = _Tweak_MatcapMaskLevel_var * lerp (1, (1 - (Set_FinalShadowMask)*(1 - _TweakMatCapOnShadow)), _Is_UseTweakMatCapOnShadow);
        float3 matCapColorOnMultiplyMode = Set_HighColor*(1-_Tweak_MatcapMaskLevel_var_MultiplyMode) + Set_HighColor*Set_MatCap*_Tweak_MatcapMaskLevel_var_MultiplyMode + lerp(float3(0,0,0),Set_RimLight,_RimLight);
        float3 matCapColorFinal = lerp(matCapColorOnMultiplyMode, matCapColorOnAddMode, _Is_BlendAddToMatCap);
        //v.2.0.4
        //#ifdef _IS_ANGELRING_OFF
            float3 finalColor = lerp(_RimLight_var, matCapColorFinal, _MatCap);// Final Composition before Emissive
            //
        // #elif _IS_ANGELRING_ON
        //     float3 finalColor = lerp(_RimLight_var, matCapColorFinal, _MatCap);// Final Composition before AR
        //     //v.2.0.7 AR Camera Rolling Stabilizer
        //     float3 _AR_OffsetU_var = lerp(mul(UNITY_MATRIX_V, float4(i.normalDir,0)).xyz,float3(0,0,1),_AR_OffsetU);
        //     float2 AR_VN = _AR_OffsetU_var.xy*0.5 + float2(0.5,0.5);
        //     float2 AR_VN_Rotate = RotateUV(AR_VN, -(_Camera_Dir*_Camera_Roll), float2(0.5,0.5), 1.0);
        //     float2 _AR_OffsetV_var = float2(AR_VN_Rotate.x, lerp(i.uv1.y, AR_VN_Rotate.y, _AR_OffsetV));
        //     int nAngelRing_SamplerArr_ID = _AngelRing_SamplerArr_ID;
        //     float4 _AngelRing_Sampler_var = SAMPLE_ANGELRING(TRANSFORM_TEX(_AR_OffsetV_var, _AngelRing_Sampler), nAngelRing_SamplerArr_ID);
        //     float3 _Is_LightColor_AR_var = lerp( (_AngelRing_Sampler_var.rgb*_AngelRing_Color.rgb), ((_AngelRing_Sampler_var.rgb*_AngelRing_Color.rgb)*Set_LightColor), _Is_LightColor_AR );
        //     float3 Set_AngelRing = _Is_LightColor_AR_var;
        //     float Set_ARtexAlpha = _AngelRing_Sampler_var.a;
        //     float3 Set_AngelRingWithAlpha = (_Is_LightColor_AR_var*_AngelRing_Sampler_var.a);
        //     //Composition: MatCap and AngelRing as finalColor
        //     finalColor = lerp(finalColor, lerp((finalColor + Set_AngelRing), ((finalColor*(1.0 - Set_ARtexAlpha))+Set_AngelRingWithAlpha), _ARSampler_AlphaOn ), _AngelRing );// Final Composition before Emissive
        // #endif
    
        //v.2.0.7
        #ifdef _EMISSIVE_SIMPLE
            int nEmissive_TexArr_ID = _Emissive_TexArr_ID;
            float4 _Emissive_Tex_var = SAMPLE_EMISSIVE(TRANSFORM_TEX(Set_UV0, _Emissive_Tex), nEmissive_TexArr_ID);
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
            //鏡の中ならUV左右反転.
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
            float2 scrolledUV = emissive_uv + float2(_Scroll_EmissiveU, _Scroll_EmissiveV)*_Is_PingPong_Base_var;
            float rotateVelocity = _Rotate_EmissiveUV*3.141592654;
            float2 _rotate_EmissiveUV_var = RotateUV(scrolledUV, rotateVelocity, float2(0.5, 0.5), _Is_PingPong_Base_var);
            int nEmissive_TexArr_ID = _Emissive_TexArr_ID;
            float4 _Emissive_Tex_var = SAMPLE_EMISSIVE(TRANSFORM_TEX(Set_UV0, _Emissive_Tex), nEmissive_TexArr_ID);
            float emissiveMask = _Emissive_Tex_var.a;
            _Emissive_Tex_var = SAMPLE_EMISSIVE(TRANSFORM_TEX(_rotate_EmissiveUV_var, _Emissive_Tex), nEmissive_TexArr_ID);
            float _colorShift_Speed_var = 1.0 - cos(_time_var.g*_ColorShift_Speed);
            float viewShift_var = smoothstep( 0.0, 1.0, max(0,dot(normalDirection,viewDirection)));
            float4 colorShift_Color = lerp(_Emissive_Color, lerp(_Emissive_Color, _ColorShift, _colorShift_Speed_var), _Is_ColorShift);
            float4 viewShift_Color = lerp(_ViewShift, colorShift_Color, viewShift_var);
            float4 emissive_Color = lerp(colorShift_Color, viewShift_Color, _Is_ViewShift);
            emissive = emissive_Color.rgb * _Emissive_Tex_var.rgb * emissiveMask;
        #endif

        //v.2.0.6: GI_Intensity with Intensity Multiplier Filter
        float3 envLightColor = envColor.rgb;
        float envLightIntensity = 0.299*envLightColor.r + 0.587*envLightColor.g + 0.114*envLightColor.b <1 ? (0.299*envLightColor.r + 0.587*envLightColor.g + 0.114*envLightColor.b) : 1;
        float3 pointLightColor = 0;
        #ifdef _ADDITIONAL_LIGHTS
            int pixelLightCount = GetAdditionalLightsCount();
            #if USE_FORWARD_PLUS
            for (uint loopCounter = 0; loopCounter < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); loopCounter++)
            {
                int iLight = loopCounter;
                // if (iLight != i.mainLightID)
                {
                    float notDirectional = 1.0f; //_WorldSpaceLightPos0.w of the legacy code.
                    UtsLight additionalLight = GetUrpMainUtsLight(0,0);
                    additionalLight = GetAdditionalUtsLight(loopCounter, inputData.positionWS, i.positionCS);
                    half3 additionalLightColor = GetLightColor(additionalLight);

                    float3 lightDirection = additionalLight.direction;
                    //v.2.0.5: 
                    float3 addPassLightColor = (0.5*dot(lerp(i.normalDir, normalDirection, _Is_NormalMapToBase), lightDirection) + 0.5) * additionalLightColor.rgb;
                    float  pureIntencity = max(0.001, (0.299*additionalLightColor.r + 0.587*additionalLightColor.g + 0.114*additionalLightColor.b));
                    float3 lightColor = max(float3(0.0,0.0,0.0), lerp(addPassLightColor, lerp(float3(0.0,0.0,0.0), min(addPassLightColor, addPassLightColor / pureIntencity), notDirectional), _Is_Filter_LightColor));
                    float3 halfDirection = normalize(viewDirection + lightDirection); // has to be recalced here.

                    //v.2.0.5:
                    float firstShadeColorStep = saturate(_1st_ShadeColor_Step + _StepOffset);
                    float secondShadeColorStep = saturate(_2nd_ShadeColor_Step + _StepOffset);
                    //
                    //v.2.0.5: If Added lights is directional, set 0 as _LightIntensity
                    float _LightIntensity = lerp(0, (0.299*additionalLightColor.r + 0.587*additionalLightColor.g + 0.114*additionalLightColor.b), notDirectional);
                    //v.2.0.5: Filtering the high intensity zone of PointLights
                    float3 Set_LightColor = lightColor;
                    //
                    float3 Set_BaseColor = lerp((_BaseColor.rgb*_MainTex_var.rgb*_LightIntensity), ((_BaseColor.rgb*_MainTex_var.rgb)*Set_LightColor), _Is_LightColor_Base);
                    //v.2.0.5
                    int n1st_ShadeMapArr_ID = _1st_ShadeMapArr_ID;
                    float4 _1st_ShadeMap_var = lerp(SAMPLE_1ST_SHADEMAP(TRANSFORM_TEX(Set_UV0, _1st_ShadeMap), n1st_ShadeMapArr_ID), _MainTex_var, _Use_BaseAs1st);
                    float3 Set_1st_ShadeColor = lerp((_1st_ShadeColor.rgb*_1st_ShadeMap_var.rgb*_LightIntensity), ((_1st_ShadeColor.rgb*_1st_ShadeMap_var.rgb)*Set_LightColor), _Is_LightColor_1st_Shade);
                    //v.2.0.5
                    int n2nd_ShadeMapArr_ID = _2nd_ShadeMapArr_ID;
                    float4 _2nd_ShadeMap_var = lerp(SAMPLE_2ND_SHADEMAP(TRANSFORM_TEX(Set_UV0, _2nd_ShadeMap), n2nd_ShadeMapArr_ID), _1st_ShadeMap_var, _Use_1stAs2nd);
                    float3 Set_2nd_ShadeColor = lerp((_2nd_ShadeColor.rgb*_2nd_ShadeMap_var.rgb*_LightIntensity), ((_2nd_ShadeColor.rgb*_2nd_ShadeMap_var.rgb)*Set_LightColor), _Is_LightColor_2nd_Shade);
                    float _HalfLambert_var = 0.5*dot(lerp(i.normalDir, normalDirection, _Is_NormalMapToBase), lightDirection) + 0.5;

                    // float4 _Set_2nd_ShadePosition_var = tex2D(_Set_2nd_ShadePosition, TRANSFORM_TEX(Set_UV0, _Set_2nd_ShadePosition));
                    // float4 _Set_1st_ShadePosition_var = tex2D(_Set_1st_ShadePosition, TRANSFORM_TEX(Set_UV0, _Set_1st_ShadePosition));
                    // //v.2.0.5:
                    // float Set_FinalShadowMask = saturate((1.0 + ((lerp(_HalfLambert_var, (_HalfLambert_var*saturate(1.0 + _Tweak_SystemShadowsLevel)), _Set_SystemShadowsToBase) - (_1st_ShadeColor_Step - _1st_ShadeColor_Feather)) * ((1.0 - _Set_1st_ShadePosition_var.rgb).r - 1.0)) / (_1st_ShadeColor_Step - (_1st_ShadeColor_Step - _1st_ShadeColor_Feather))));
                    //SGM

                    //v.2.0.6
                    int nShadingGradeMapArr_ID = _ShadingGradeMapArr_ID;
                    float4 _ShadingGradeMap_var = SAMPLE_SHADINGGRADEMAP(TRANSFORM_TEX(Set_UV0, _ShadingGradeMap), nShadingGradeMapArr_ID, _BlurLevelSGM);
                    //v.2.0.6
                    //Minmimum value is same as the Minimum Feather's value with the Minimum Step's value as threshold.
                    //float _SystemShadowsLevel_var = (attenuation*0.5)+0.5+_Tweak_SystemShadowsLevel > 0.001 ? (attenuation*0.5)+0.5+_Tweak_SystemShadowsLevel : 0.0001;
                    float _ShadingGradeMapLevel_var = _ShadingGradeMap_var.r < 0.95 ? _ShadingGradeMap_var.r + _Tweak_ShadingGradeMapLevel : 1;

                    //float Set_ShadingGrade = saturate(_ShadingGradeMapLevel_var)*lerp( _HalfLambert_var, (_HalfLambert_var*saturate(_SystemShadowsLevel_var)), _Set_SystemShadowsToBase );
                    float Set_ShadingGrade = saturate(_ShadingGradeMapLevel_var)*lerp(_HalfLambert_var, (_HalfLambert_var*saturate(1.0 + _Tweak_SystemShadowsLevel)), _Set_SystemShadowsToBase);
                    float Set_FinalShadowMask = saturate((1.0 + ((Set_ShadingGrade - (firstShadeColorStep - _1st_ShadeColor_Feather)) * (0.0 - 1.0)) / (firstShadeColorStep - (firstShadeColorStep - _1st_ShadeColor_Feather))));
                    float Set_ShadeShadowMask = saturate((1.0 + ((Set_ShadingGrade - (secondShadeColorStep - _2nd_ShadeColor_Feather)) * (0.0 - 1.0)) / (secondShadeColorStep - (secondShadeColorStep - _2nd_ShadeColor_Feather)))); // 1st and 2nd Shades Mask

                    //Composition: 3 Basic Colors as finalColor
                    float3 finalColor =
                        lerp(
                            Set_BaseColor,
                            //_BaseColor_var*(Set_LightColor*1.5),

                            lerp(
                                Set_1st_ShadeColor,
                                Set_2nd_ShadeColor,
                                Set_ShadeShadowMask
                            ),
                            Set_FinalShadowMask);
                    //v.2.0.6: Add HighColor if _Is_Filter_HiCutPointLightColor is False
                    int nSet_HighColorMaskArrID = _Set_HighColorMaskArr_ID;
                    float4 _Set_HighColorMask_var = SAMPLE_HIGHCOLORMASK(TRANSFORM_TEX(Set_UV0, _Set_HighColorMask), nSet_HighColorMaskArrID);
                    float _Specular_var = 0.5*dot(halfDirection, lerp(i.normalDir, normalDirection, _Is_NormalMapToHighColor)) + 0.5; //  Specular                
                    float _TweakHighColorMask_var = (saturate((_Set_HighColorMask_var.g + _Tweak_HighColorMaskLevel))*lerp((1.0 - step(_Specular_var, (1.0 - pow(abs(_HighColor_Power), 5)))), pow(abs(_Specular_var), exp2(lerp(11, 1, _HighColor_Power))), _Is_SpecularToHighColor));
                    
                    int nHighColor_TexArrID = _HighColor_TexArr_ID;
                    float4 _HighColor_Tex_var = SAMPLE_HIGHCOLOR(TRANSFORM_TEX(Set_UV0, _HighColor_Tex), nHighColor_TexArrID);
                    float3 _HighColor_var = (lerp((_HighColor_Tex_var.rgb*_HighColor.rgb), ((_HighColor_Tex_var.rgb*_HighColor.rgb)*Set_LightColor), _Is_LightColor_HighColor)*_TweakHighColorMask_var);
                    finalColor = finalColor + lerp(lerp(_HighColor_var, (_HighColor_var*((1.0 - Set_FinalShadowMask) + (Set_FinalShadowMask*_TweakHighColorOnShadow))), _Is_UseTweakHighColorOnShadow), float3(0, 0, 0), _Is_Filter_HiCutPointLightColor);
                    finalColor = SATURATE_IF_SDR(finalColor);
                    pointLightColor +=  finalColor;
                }
            }
            #endif  // USE_FORWARD_PLUS
            // determine main light inorder to apply light culling properly

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
                    if (iLight != MAINLIGHT_IS_MAINLIGHT)
                    {
                        additionalLight = GetAdditionalUtsLight(iLight, inputData.positionWS, i.positionCS);
                    }
                    half3 additionalLightColor = GetLightColor(additionalLight);
                    float3 lightDirection = additionalLight.direction;
                    //v.2.0.5: 
                    float3 addPassLightColor = (0.5*dot(lerp(i.normalDir, normalDirection, _Is_NormalMapToBase), lightDirection) + 0.5) * additionalLightColor.rgb;
                    float  pureIntencity = max(0.001, (0.299*additionalLightColor.r + 0.587*additionalLightColor.g + 0.114*additionalLightColor.b));
                    float3 lightColor = max(float3(0.0,0.0,0.0), lerp(addPassLightColor, lerp(float3(0.0,0.0,0.0), min(addPassLightColor, addPassLightColor / pureIntencity), notDirectional), _Is_Filter_LightColor));
                    float3 halfDirection = normalize(viewDirection + lightDirection); // has to be recalced here.

                    //v.2.0.5:
                    float firstShadeColorStep = saturate(_1st_ShadeColor_Step + _StepOffset);
                    float secondShadeColorStep = saturate(_2nd_ShadeColor_Step + _StepOffset);
                    //
                    //v.2.0.5: If Added lights is directional, set 0 as _LightIntensity
                    float _LightIntensity = lerp(0, (0.299*additionalLightColor.r + 0.587*additionalLightColor.g + 0.114*additionalLightColor.b), notDirectional);
                    //v.2.0.5: Filtering the high intensity zone of PointLights
                    float3 Set_LightColor = lightColor;
                    //
                    float3 Set_BaseColor = lerp((_BaseColor.rgb*_MainTex_var.rgb*_LightIntensity), ((_BaseColor.rgb*_MainTex_var.rgb)*Set_LightColor), _Is_LightColor_Base);
                    //v.2.0.5
                    int n1st_ShadeMapArrID = _1st_ShadeMapArr_ID;
                    float4 _1st_ShadeMap_var = lerp(SAMPLE_1ST_SHADEMAP(TRANSFORM_TEX(Set_UV0, _1st_ShadeMap), n1st_ShadeMapArrID), _MainTex_var, _Use_BaseAs1st);
                    float3 Set_1st_ShadeColor = lerp((_1st_ShadeColor.rgb*_1st_ShadeMap_var.rgb*_LightIntensity), ((_1st_ShadeColor.rgb*_1st_ShadeMap_var.rgb)*Set_LightColor), _Is_LightColor_1st_Shade);
                    //v.2.0.5
                    int n2nd_ShadeMapArrID = _2nd_ShadeMapArr_ID;
                    float4 _2nd_ShadeMap_var = lerp(SAMPLE_2ND_SHADEMAP(TRANSFORM_TEX(Set_UV0, _2nd_ShadeMap), n2nd_ShadeMapArrID), _1st_ShadeMap_var, _Use_1stAs2nd);
                    float3 Set_2nd_ShadeColor = lerp((_2nd_ShadeColor.rgb*_2nd_ShadeMap_var.rgb*_LightIntensity), ((_2nd_ShadeColor.rgb*_2nd_ShadeMap_var.rgb)*Set_LightColor), _Is_LightColor_2nd_Shade);
                    float _HalfLambert_var = 0.5*dot(lerp(i.normalDir, normalDirection, _Is_NormalMapToBase), lightDirection) + 0.5;

                    // float4 _Set_2nd_ShadePosition_var = tex2D(_Set_2nd_ShadePosition, TRANSFORM_TEX(Set_UV0, _Set_2nd_ShadePosition));
                    // float4 _Set_1st_ShadePosition_var = tex2D(_Set_1st_ShadePosition, TRANSFORM_TEX(Set_UV0, _Set_1st_ShadePosition));
                    // //v.2.0.5:
                    // float Set_FinalShadowMask = saturate((1.0 + ((lerp(_HalfLambert_var, (_HalfLambert_var*saturate(1.0 + _Tweak_SystemShadowsLevel)), _Set_SystemShadowsToBase) - (_1st_ShadeColor_Step - _1st_ShadeColor_Feather)) * ((1.0 - _Set_1st_ShadePosition_var.rgb).r - 1.0)) / (_1st_ShadeColor_Step - (_1st_ShadeColor_Step - _1st_ShadeColor_Feather))));
                    //SGM

                    //v.2.0.6
                    int nShadingGradeMapArrID = _ShadingGradeMapArr_ID;
                    float4 _ShadingGradeMap_var = SAMPLE_SHADINGGRADEMAP(TRANSFORM_TEX(Set_UV0, _ShadingGradeMap), nShadingGradeMapArrID, _BlurLevelSGM);
                    //v.2.0.6
                    //Minmimum value is same as the Minimum Feather's value with the Minimum Step's value as threshold.
                    //float _SystemShadowsLevel_var = (attenuation*0.5)+0.5+_Tweak_SystemShadowsLevel > 0.001 ? (attenuation*0.5)+0.5+_Tweak_SystemShadowsLevel : 0.0001;
                    float _ShadingGradeMapLevel_var = _ShadingGradeMap_var.r < 0.95 ? _ShadingGradeMap_var.r + _Tweak_ShadingGradeMapLevel : 1;

                    //float Set_ShadingGrade = saturate(_ShadingGradeMapLevel_var)*lerp( _HalfLambert_var, (_HalfLambert_var*saturate(_SystemShadowsLevel_var)), _Set_SystemShadowsToBase );

                    float Set_ShadingGrade = saturate(_ShadingGradeMapLevel_var)*lerp(_HalfLambert_var, (_HalfLambert_var*saturate(1.0 + _Tweak_SystemShadowsLevel)), _Set_SystemShadowsToBase);
                    float Set_FinalShadowMask = saturate((1.0 + ((Set_ShadingGrade - (firstShadeColorStep - _1st_ShadeColor_Feather)) * (0.0 - 1.0)) / (firstShadeColorStep - (firstShadeColorStep - _1st_ShadeColor_Feather))));
                    float Set_ShadeShadowMask = saturate((1.0 + ((Set_ShadingGrade - (secondShadeColorStep - _2nd_ShadeColor_Feather)) * (0.0 - 1.0)) / (secondShadeColorStep - (secondShadeColorStep - _2nd_ShadeColor_Feather)))); // 1st and 2nd Shades Mask
                    
                    //  //Composition: 3 Basic Colors as finalColor
                    //  float3 finalColor = 
                    // lerp(
                    //     Set_BaseColor, 
                    //     lerp(
                    //         Set_1st_ShadeColor,
                    //         Set_2nd_ShadeColor,
                    //         saturate(
                    //            (1.0 + ((_HalfLambert_var - (_2nd_ShadeColor_Step - _2nd_Shades_Feather)) * ((1.0 - _Set_2nd_ShadePosition_var.rgb).r - 1.0)) / (_2nd_ShadeColor_Step - (_2nd_ShadeColor_Step - _2nd_Shades_Feather))))
                    //            ),
                    //     Set_FinalShadowMask); // Final Color


                    //Composition: 3 Basic Colors as finalColor
                    float3 finalColor =
                        lerp(
                            Set_BaseColor,
                            //_BaseColor_var*(Set_LightColor*1.5),

                            lerp(
                                Set_1st_ShadeColor,
                                Set_2nd_ShadeColor,
                                Set_ShadeShadowMask
                            ),
                            Set_FinalShadowMask);
                    //v.2.0.6: Add HighColor if _Is_Filter_HiCutPointLightColor is False
                    int nSet_HighColorMaskArrID = _Set_HighColorMaskArr_ID;
                    float4 _Set_HighColorMask_var = SAMPLE_HIGHCOLORMASK(TRANSFORM_TEX(Set_UV0, _Set_HighColorMask), nSet_HighColorMaskArrID);
                    float _Specular_var = 0.5*dot(halfDirection, lerp(i.normalDir, normalDirection, _Is_NormalMapToHighColor)) + 0.5; //  Specular                
                    float _TweakHighColorMask_var = (saturate((_Set_HighColorMask_var.g + _Tweak_HighColorMaskLevel))*lerp((1.0 - step(_Specular_var, (1.0 - pow(abs(_HighColor_Power), 5)))), pow(abs(_Specular_var), exp2(lerp(11, 1, _HighColor_Power))), _Is_SpecularToHighColor));

                    int nHighColor_TexArrID = _HighColor_TexArr_ID;
                    float4 _HighColor_Tex_var = SAMPLE_HIGHCOLOR(TRANSFORM_TEX(Set_UV0, _HighColor_Tex), nHighColor_TexArrID);
                    float3 _HighColor_var = (lerp((_HighColor_Tex_var.rgb*_HighColor.rgb), ((_HighColor_Tex_var.rgb*_HighColor.rgb)*Set_LightColor), _Is_LightColor_HighColor)*_TweakHighColorMask_var);
                    finalColor = finalColor + lerp(lerp(_HighColor_var, (_HighColor_var*((1.0 - Set_FinalShadowMask) + (Set_FinalShadowMask*_TweakHighColorOnShadow))), _Is_UseTweakHighColorOnShadow), float3(0, 0, 0), _Is_Filter_HiCutPointLightColor);
                    finalColor = SATURATE_IF_SDR(finalColor);
                    pointLightColor +=  finalColor;
                    //	pointLightColor += lightColor;
                }
            UTS_LIGHT_LOOP_END
        #endif // _ADDITIONAL_LIGHTS

        //Final Composition
        finalColor = SATURATE_IF_SDR(finalColor) + (envLightColor*envLightIntensity*_GI_Intensity*smoothstep(1,0,envLightIntensity/2)) + emissive;
        finalColor += pointLightColor;
    //#endif


    //v.2.0.4
    #ifdef _IS_TRANSCLIPPING_OFF
        fixed4 finalRGBA = fixed4(finalColor,1);
    #elif _IS_TRANSCLIPPING_ON
        float Set_Opacity = SATURATE_IF_SDR((_Inverse_Clipping_var+_Tweak_transparency));
        fixed4 finalRGBA = fixed4(finalColor,Set_Opacity);
    #endif

    return finalRGBA;
}


