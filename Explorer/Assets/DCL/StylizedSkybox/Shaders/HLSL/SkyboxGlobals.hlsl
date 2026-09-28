#ifndef DCL_SKYBOX_GLOBALS_INCLUDED
#define DCL_SKYBOX_GLOBALS_INCLUDED

// Global shader values shared by the skybox Custom Function files. SkyboxRenderController sets them with
// Shader.SetGlobal*, so the main sky and the reflection-cubemap bake read the same data and the graphs need no
// extra properties. Kept in one header so several files can use them without redefinitions.

float4 _DclSunDirection;     // direction toward the active celestial body (sun by day, moon by night), world space
float4 _DclCelestialParams;  // horizonDarkeningHeight (0 = off), horizonDarkeningFloor, singleSidedBacklight, backlightWeight
float4 _DclSunHazeParams;    // factor (0 = off), size boost, vertical squash, edge softness (fraction of the radius)
float4 _DclSunHazeParams2;   // ragged rim strength, rim detail (harmonics), rim speed, gradient power
float4 _DclSunHazeTop;       // disc colour at the top of the disc while hazed
float4 _DclSunHazeBottom;    // disc colour at the bottom of the disc while hazed

#endif
