# Genesis Skybox V3
The skybox is divided into 2 parts: the GenesisSky shader, and the SkyboxController component.

## GenesisSky Shader
`Location: Shaders/GenesisSky.shadergraph`
This shader manages the rendering of the sky, exposing the following parameters:

color, blend, spread, and twisting of the different color bands (zenith, ground, nadir)
the size and color of the sun
the star tile texture
the rotation speed and textures of 2 cloud layers
The nearby clouds move at twice the speed defined by the CloudsRotationSpeed.

The directional light set in the scene, within `Lighting > Environment` settings, controls the position of the sun.

Some additional parameters can be exposed, such as the blending mask of the color bands, the tiling of the stars, etc.

There is an example configuration in:
`Location: Materials/GenesisSky.material`

## SkyboxController
`Location: Scripts/SkyboxController.csharp`
This component, assigned to any empty transform within the scene, manages the time loop from 0 to 24 hours.
It is responsible for controlling the speed of time, the refresh rate, the color ramps of each of the shader's elements, in addition to controlling indirect light and fog.

It is necessary to add an AnimationClip associated with a directional light to control elements like rotation. In the example found in SunCycle24h.anim, a 24-second animation is created to match 24 hours, but the length of the animation can be longer since it is normalized based on its length.
`Location: Animations/SunCycle24h.anim`

A TextMeshProGUI component can be associated with the skybox to display the current time in hours and normalized.

## SkyboxController_Editor
`Location: Editor/SkyboxController_Editor.csharp`
This is a class to control certain buttons of the inspector, like the sliders and fixe positions of time.

# Post-processing
In order to make everything pop, the bloom efect must be enabled in the post-processing volume.
A global reflection probe that updates everytime is placed in the scene to reflect the skybox in the surfaces.