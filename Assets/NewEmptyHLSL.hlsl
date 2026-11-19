#ifndef MAIN_LIGHT_COLOR_INCLUDED
#define MAIN_LIGHT_COLOR_INCLUDED

#ifndef UNIVERSAL_LIGHTING_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif

void GetMainLight_float(out float3 Color)
{
#ifdef SHADERGRAPH_PREVIEW
        Color = float3(1, 1, 1);
#else
    Light mainLight = GetMainLight();
    Color = mainLight.color;
#endif
}

#endif