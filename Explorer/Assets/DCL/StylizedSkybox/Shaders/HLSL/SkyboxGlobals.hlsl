#ifndef DCL_SKYBOX_GLOBALS_INCLUDED
#define DCL_SKYBOX_GLOBALS_INCLUDED

// Global shader values shared by the skybox Custom Function files. SkyboxRenderController sets them with
// Shader.SetGlobal*, so the main sky and the reflection-cubemap bake read the same data and the graphs need no
// extra properties. Kept in one header so several files can use them without redefinitions.

float4 _DclSunDirection;     // direction toward the active celestial body (sun by day, moon by night), world space
float4 _DclCelestialParams;  // horizonDarkeningHeight (0 = off), horizonDarkeningFloor, singleSidedBacklight, backlightWeight

#endif
