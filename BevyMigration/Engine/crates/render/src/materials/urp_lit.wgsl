// URP Lit family (BevyMigration/docs/README.md § 8 "Material families"): Bevy's PBR with Unity's
// channel conventions for the metallic-gloss and occlusion maps, and the SceneBounds extension that
// dithers geometry outside the parcel rectangle.
#import bevy_pbr::{
    pbr_bindings,
    pbr_fragment::pbr_input_from_standard_material,
    pbr_functions::alpha_discard,
}

#ifdef PREPASS_PIPELINE
#import bevy_pbr::{
    prepass_io::{VertexOutput, FragmentOutput},
    pbr_deferred_functions::deferred_output,
}
#else
#import bevy_pbr::{
    forward_io::{VertexOutput, FragmentOutput},
    pbr_functions::{apply_pbr_lighting, main_pass_post_lighting_processing},
    pbr_types::STANDARD_MATERIAL_FLAGS_UNLIT_BIT,
}
#endif

struct UrpLitParams {
    metallic: f32,
    smoothness: f32,
    occlusion_strength: f32,
    flags: u32,
    bounds_min: vec2<f32>,
    bounds_max: vec2<f32>,
}

const FLAG_METALLIC_GLOSS_MAP: u32 = 1u;
const FLAG_OCCLUSION_MAP: u32 = 2u;
const FLAG_SCENE_BOUNDS: u32 = 4u;
const FLAG_SDF_TEXT: u32 = 8u;

@group(2) @binding(100) var<uniform> params: UrpLitParams;
@group(2) @binding(101) var metallic_gloss_texture: texture_2d<f32>;
@group(2) @binding(102) var metallic_gloss_sampler: sampler;
@group(2) @binding(103) var occlusion_texture: texture_2d<f32>;
@group(2) @binding(104) var occlusion_sampler: sampler;

// 4x4 ordered dither so geometry fades out over two metres past the parcel edge instead of cutting.
fn dither_threshold(frag: vec2<f32>) -> f32 {
    let x = u32(frag.x) % 4u;
    let y = u32(frag.y) % 4u;
    let index = x + y * 4u;
    var bayer = array<f32, 16>(
        0.0, 8.0, 2.0, 10.0,
        12.0, 4.0, 14.0, 6.0,
        3.0, 11.0, 1.0, 9.0,
        15.0, 7.0, 13.0, 5.0,
    );
    return (bayer[index] + 0.5) / 16.0;
}

@fragment
fn fragment(in: VertexOutput, @builtin(front_facing) is_front: bool) -> FragmentOutput {
    var pbr_input = pbr_input_from_standard_material(in, is_front);

#ifdef VERTEX_UVS_A
    if ((params.flags & FLAG_SDF_TEXT) != 0u) {
        // TextMeshPro atlas: the signed distance lives in alpha, the glyph colour is the material's.
        let sdf = textureSample(pbr_bindings::base_color_texture, pbr_bindings::base_color_sampler, in.uv).a;
        var text_color = pbr_bindings::material.base_color;
#ifdef VERTEX_COLORS
        text_color = text_color * in.color;
#endif
        pbr_input.material.base_color = vec4<f32>(text_color.rgb, text_color.a * sdf);
    }
#endif

    var metallic = params.metallic;
    var smoothness = params.smoothness;

// The maps need a UV set; a mesh without one keeps the scalar properties.
#ifdef VERTEX_UVS_A
    if ((params.flags & FLAG_METALLIC_GLOSS_MAP) != 0u) {
        // Unity: metallic in R, smoothness in A, both scaled by the scalar properties.
        let mg = textureSample(metallic_gloss_texture, metallic_gloss_sampler, in.uv);
        metallic = mg.r * params.metallic;
        smoothness = mg.a * params.smoothness;
    }
#endif

    pbr_input.material.metallic = metallic;
    pbr_input.material.perceptual_roughness = clamp(1.0 - smoothness, 0.045, 1.0);

#ifdef VERTEX_UVS_A
    if ((params.flags & FLAG_OCCLUSION_MAP) != 0u) {
        // Unity: occlusion in G, blended by the strength.
        let occlusion = textureSample(occlusion_texture, occlusion_sampler, in.uv).g;
        let factor = mix(1.0, occlusion, params.occlusion_strength);
        pbr_input.diffuse_occlusion = pbr_input.diffuse_occlusion * factor;
    }
#endif

    if ((params.flags & FLAG_SCENE_BOUNDS) != 0u) {
        // Bevy's world is Unity's with Z mirrored (render crate), so the bound on Z is negated here.
        let p = vec2<f32>(in.world_position.x, -in.world_position.z);
        let outside = max(
            max(params.bounds_min.x - p.x, p.x - params.bounds_max.x),
            max(params.bounds_min.y - p.y, p.y - params.bounds_max.y),
        );
        if (outside > 0.0 && min(outside * 0.5, 1.0) > dither_threshold(in.position.xy)) {
            discard;
        }
    }

    pbr_input.material.base_color = alpha_discard(pbr_input.material, pbr_input.material.base_color);

#ifdef PREPASS_PIPELINE
    let out = deferred_output(in, pbr_input);
#else
    var out: FragmentOutput;
    if (pbr_input.material.flags & STANDARD_MATERIAL_FLAGS_UNLIT_BIT) == 0u {
        out.color = apply_pbr_lighting(pbr_input);
    } else {
        out.color = pbr_input.material.base_color;
    }
    out.color = main_pass_post_lighting_processing(pbr_input, out.color);
#endif

    return out;
}
