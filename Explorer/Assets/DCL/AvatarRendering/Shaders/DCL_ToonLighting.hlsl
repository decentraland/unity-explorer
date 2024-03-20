float3 ForwardPlusLighting()
{
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
            // attenuation = light.distanceAttenuation; 

            float3 lightDirection = additionalLight.direction;
            //v.2.0.5: 
            float3 addPassLightColor = (0.5*dot(lerp(i.normalDir, normalDirection, _Is_NormalMapToBase), lightDirection) + 0.5) * additionalLightColor.rgb;
            float  pureIntencity = max(0.001, (0.299*additionalLightColor.r + 0.587*additionalLightColor.g + 0.114*additionalLightColor.b));
            float3 lightColor = max(float3(0.0,0.0,0.0), lerp(addPassLightColor, lerp(float3(0.0,0.0,0.0), min(addPassLightColor, addPassLightColor / pureIntencity), notDirectional), _Is_Filter_LightColor));
            float3 halfDirection = normalize(viewDirection + lightDirection); // has to be recalced here.

            //v.2.0.5:
            float baseColorStep = saturate(_BaseColor_Step + _StepOffset);
            float shadeColorStep = saturate(_ShadeColor_Step + _StepOffset);

            //v.2.0.5: If Added lights is directional, set 0 as _LightIntensity
            float _LightIntensity = lerp(0, (0.299*additionalLightColor.r + 0.587*additionalLightColor.g + 0.114*additionalLightColor.b), notDirectional);
            //v.2.0.5: Filtering the high intensity zone of PointLights
            float3 Set_LightColor = lightColor;
            float3 Set_BaseColor = lerp((_BaseColor.rgb*_MainTex_var.rgb*_LightIntensity), ((_BaseColor.rgb*_MainTex_var.rgb)*Set_LightColor), _Is_LightColor_Base);
            // 1st ShadeMap
            int n1st_ShadeMapArrID = _1st_ShadeMapArr_ID;
            float4 _1st_ShadeMap_var = lerp(SAMPLE_1ST_SHADEMAP(TRANSFORM_TEX(Set_UV0, _1st_ShadeMap), n1st_ShadeMapArrID), float4(Set_BaseColor.rgb, _MainTex_var.a), _Use_BaseAs1st);
            float3 Set_1st_ShadeColor = lerp((_1st_ShadeColor.rgb*_1st_ShadeMap_var.rgb*_LightIntensity), ((_1st_ShadeColor.rgb*_1st_ShadeMap_var.rgb)*Set_LightColor), _Is_LightColor_1st_Shade);
            // 2nd ShadeMap
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
}

float3 ForwardLighting()
{
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
            // attenuation = light.distanceAttenuation; 


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
            float Set_FinalShadowMask = saturate((1.0 + ((lerp(_HalfLambert_var, (_HalfLambert_var*saturate(_SystemShadowsLevel_var)),         _Set_SystemShadowsToBase) - (_BaseColor_Step-_BaseShade_Feather)) * ((1.0 - _Set_1st_ShadePosition_var.rgb).r - 1.0) ) / (_BaseColor_Step - (_BaseColor_Step-_BaseShade_Feather))));
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
        }
    UTS_LIGHT_LOOP_END
}

float3 CalculateAdditionalLightingColour()
{
    float3 pointLightColor = 0;
    int pixelLightCount = GetAdditionalLightsCount();

    #if USE_FORWARD_PLUS
    pointLightColor = ForwardPlusLighting();
    #endif  // USE_FORWARD_PLUS

    pointLightColor = ForwardLighting();
}