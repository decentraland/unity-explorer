//! Resource upload (plan § 8 "Resource transfer", phase 1): meshes, textures and materials copied once
//! from the host into Bevy assets, keyed by the host's handles.

use crate::materials::{UrpLitExtension, UrpLitMaterial, urp_lit_flags};
use abi::{
    BmAlphaMode, BmMaterial, BmMaterialFamily, BmMeshHeader, BmSubmesh, BmTextureFormat,
    BmTextureHeader, TEXTURE_NONE, filter_mode, index_format, mesh_attributes, wrap_mode,
};
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    pbr::ExtendedMaterial,
    prelude::*,
    render::{
        mesh::{Indices, PrimitiveTopology},
        render_resource::{Extent3d, TextureDimension, TextureFormat},
    },
};
use std::collections::HashMap;

/// A resource command copied out of host memory by the bridge.
#[derive(Debug, Clone)]
pub enum ResourceCommand {
    MeshUpload { handle: u32, payload: Vec<u8> },
    MeshRelease { handle: u32 },
    TextureUpload { handle: u32, payload: Vec<u8> },
    TextureRelease { handle: u32 },
    MaterialUpload { handle: u32, material: BmMaterial },
    MaterialRelease { handle: u32 },
}

/// Commands queued by the embedder; applied before the next snapshot.
#[derive(Resource, Debug, Default)]
pub struct PendingResources(pub Vec<ResourceCommand>);

/// Host handle to Bevy asset, per resource kind. A mesh is one Bevy mesh per submesh.
#[derive(Resource, Debug, Default)]
pub struct ResourceTables {
    meshes: HashMap<u32, Vec<Handle<Mesh>>>,
    textures: HashMap<u32, Handle<Image>>,
    materials: HashMap<u32, Handle<UrpLitMaterial>>,
    /// Uploads that could not be decoded, kept so the snapshot can fall back to placeholders quietly.
    pub rejected: u64,
}

impl ResourceTables {
    pub fn mesh(&self, handle: u32) -> Option<&[Handle<Mesh>]> {
        self.meshes.get(&handle).map(Vec::as_slice)
    }

    pub fn texture(&self, handle: u32) -> Option<&Handle<Image>> {
        self.textures.get(&handle)
    }

    pub fn material(&self, handle: u32) -> Option<&Handle<UrpLitMaterial>> {
        self.materials.get(&handle)
    }

    pub fn counts(&self) -> (usize, usize, usize) {
        (self.meshes.len(), self.textures.len(), self.materials.len())
    }
}

pub fn apply_resources(
    mut pending: ResMut<PendingResources>,
    mut tables: ResMut<ResourceTables>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut images: ResMut<Assets<Image>>,
    mut materials: ResMut<Assets<UrpLitMaterial>>,
) {
    for command in pending.0.drain(..) {
        match command {
            ResourceCommand::MeshUpload { handle, payload } => {
                if let Some(decoded) = decode_mesh(&payload) {
                    let handles = decoded.into_iter().map(|mesh| meshes.add(mesh)).collect();
                    tables.meshes.insert(handle, handles);
                } else {
                    tables.rejected += 1;
                    warn!("mesh upload {handle} rejected: malformed payload");
                }
            }
            ResourceCommand::MeshRelease { handle } => {
                tables.meshes.remove(&handle);
            }
            ResourceCommand::TextureUpload { handle, payload } => {
                if let Some(image) = decode_texture(&payload) {
                    tables.textures.insert(handle, images.add(image));
                } else {
                    tables.rejected += 1;
                    warn!("texture upload {handle} rejected: malformed payload");
                }
            }
            ResourceCommand::TextureRelease { handle } => {
                tables.textures.remove(&handle);
            }
            ResourceCommand::MaterialUpload { handle, material } => {
                let asset = build_material(&material, &tables);
                tables.materials.insert(handle, materials.add(asset));
            }
            ResourceCommand::MaterialRelease { handle } => {
                tables.materials.remove(&handle);
            }
        }
    }
}

/// Little-endian reader over the payload; every read is bounds checked.
struct Reader<'a> {
    bytes: &'a [u8],
    offset: usize,
}

impl<'a> Reader<'a> {
    fn new(bytes: &'a [u8]) -> Self {
        Self { bytes, offset: 0 }
    }

    fn take(&mut self, len: usize) -> Option<&'a [u8]> {
        let end = self.offset.checked_add(len)?;
        let slice = self.bytes.get(self.offset..end)?;
        self.offset = end;
        Some(slice)
    }

    fn u32(&mut self) -> Option<u32> {
        let bytes = self.take(4)?;
        Some(u32::from_le_bytes([
            *bytes.first()?,
            *bytes.get(1)?,
            *bytes.get(2)?,
            *bytes.get(3)?,
        ]))
    }

    fn f32s(&mut self, count: usize) -> Option<Vec<f32>> {
        let bytes = self.take(count.checked_mul(4)?)?;
        Some(
            bytes
                .chunks_exact(4)
                .map(|c| f32::from_le_bytes([c[0], c[1], c[2], c[3]]))
                .collect(),
        )
    }

    fn remaining(&self) -> &'a [u8] {
        self.bytes.get(self.offset..).unwrap_or(&[])
    }
}

fn read_mesh_header(reader: &mut Reader<'_>) -> Option<BmMeshHeader> {
    Some(BmMeshHeader {
        vertex_count: reader.u32()?,
        index_count: reader.u32()?,
        submesh_count: reader.u32()?,
        attributes: reader.u32()?,
        index_format: reader.u32()?,
    })
}

fn read_texture_header(reader: &mut Reader<'_>) -> Option<BmTextureHeader> {
    let width = reader.u32()?;
    let height = reader.u32()?;
    let mip_count = reader.u32()?;
    let format = match reader.u32()? {
        0 => BmTextureFormat::Rgba8,
        1 => BmTextureFormat::Bgra8,
        2 => BmTextureFormat::Bc1,
        3 => BmTextureFormat::Bc3,
        4 => BmTextureFormat::Bc4,
        5 => BmTextureFormat::Bc5,
        6 => BmTextureFormat::Bc7,
        7 => BmTextureFormat::R8,
        8 => BmTextureFormat::Rg8,
        _ => return None,
    };
    Some(BmTextureHeader {
        width,
        height,
        mip_count,
        format,
        srgb: reader.u32()?,
        wrap_u: reader.u32()?,
        wrap_v: reader.u32()?,
        filter: reader.u32()?,
    })
}

fn group<const N: usize>(values: Vec<f32>) -> Vec<[f32; N]> {
    values
        .chunks_exact(N)
        .map(|chunk| {
            let mut out = [0.0; N];
            out.copy_from_slice(chunk);
            out
        })
        .collect()
}

/// Decodes a mesh upload into one Bevy mesh per submesh. Vertices stay in Unity's mesh space (the
/// world matrix handles the mirror); the triangle winding is reversed because the mirrored world
/// turns Unity's clockwise front faces into clockwise ones in Bevy's clip space, where front is
/// counter-clockwise.
pub fn decode_mesh(payload: &[u8]) -> Option<Vec<Mesh>> {
    let mut reader = Reader::new(payload);
    let header = read_mesh_header(&mut reader)?;
    let vertex_count = usize::try_from(header.vertex_count).ok()?;
    let index_count = usize::try_from(header.index_count).ok()?;
    let submesh_count = usize::try_from(header.submesh_count).ok()?;

    if vertex_count == 0 || submesh_count == 0 {
        return None;
    }

    let positions = group::<3>(reader.f32s(vertex_count.checked_mul(3)?)?);
    let normals = if header.attributes & mesh_attributes::NORMAL != 0 {
        Some(group::<3>(reader.f32s(vertex_count.checked_mul(3)?)?))
    } else {
        None
    };
    let tangents = if header.attributes & mesh_attributes::TANGENT != 0 {
        Some(group::<4>(reader.f32s(vertex_count.checked_mul(4)?)?))
    } else {
        None
    };
    let uv0 = if header.attributes & mesh_attributes::UV0 != 0 {
        Some(group::<2>(reader.f32s(vertex_count.checked_mul(2)?)?))
    } else {
        None
    };
    let uv1 = if header.attributes & mesh_attributes::UV1 != 0 {
        Some(group::<2>(reader.f32s(vertex_count.checked_mul(2)?)?))
    } else {
        None
    };
    let colors = if header.attributes & mesh_attributes::COLOR != 0 {
        Some(group::<4>(reader.f32s(vertex_count.checked_mul(4)?)?))
    } else {
        None
    };

    let indices: Vec<u32> = match header.index_format {
        index_format::U16 => reader
            .take(index_count.checked_mul(2)?)?
            .chunks_exact(2)
            .map(|c| u32::from(u16::from_le_bytes([c[0], c[1]])))
            .collect(),
        index_format::U32 => reader
            .take(index_count.checked_mul(4)?)?
            .chunks_exact(4)
            .map(|c| u32::from_le_bytes([c[0], c[1], c[2], c[3]]))
            .collect(),
        _ => return None,
    };

    let mut submeshes = Vec::with_capacity(submesh_count);

    for _ in 0..submesh_count {
        submeshes.push(BmSubmesh {
            index_start: reader.u32()?,
            index_count: reader.u32()?,
        });
    }

    let max_index = u32::try_from(vertex_count).ok()?;
    let mut meshes = Vec::with_capacity(submesh_count);

    for submesh in submeshes {
        let start = usize::try_from(submesh.index_start).ok()?;
        let end = start.checked_add(usize::try_from(submesh.index_count).ok()?)?;
        let range = indices.get(start..end)?;

        if range.iter().any(|&index| index >= max_index) {
            return None;
        }

        let mut rewound = Vec::with_capacity(range.len());

        for triangle in range.chunks_exact(3) {
            rewound.extend_from_slice(&[triangle[0], triangle[2], triangle[1]]);
        }

        let mut mesh = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::RENDER_WORLD,
        );
        mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, positions.clone());

        if let Some(normals) = &normals {
            mesh.insert_attribute(Mesh::ATTRIBUTE_NORMAL, normals.clone());
        }

        if let Some(tangents) = &tangents {
            mesh.insert_attribute(Mesh::ATTRIBUTE_TANGENT, tangents.clone());
        }

        if let Some(uv0) = &uv0 {
            mesh.insert_attribute(Mesh::ATTRIBUTE_UV_0, uv0.clone());
        }

        if let Some(uv1) = &uv1 {
            mesh.insert_attribute(Mesh::ATTRIBUTE_UV_1, uv1.clone());
        }

        if let Some(colors) = &colors {
            mesh.insert_attribute(Mesh::ATTRIBUTE_COLOR, colors.clone());
        }

        mesh.insert_indices(Indices::U32(rewound));

        if normals.is_none() {
            mesh.compute_normals();
        }

        meshes.push(mesh);
    }

    Some(meshes)
}

fn texture_format(format: BmTextureFormat, srgb: bool) -> TextureFormat {
    match (format, srgb) {
        (BmTextureFormat::Rgba8, true) => TextureFormat::Rgba8UnormSrgb,
        (BmTextureFormat::Rgba8, false) => TextureFormat::Rgba8Unorm,
        (BmTextureFormat::Bgra8, true) => TextureFormat::Bgra8UnormSrgb,
        (BmTextureFormat::Bgra8, false) => TextureFormat::Bgra8Unorm,
        (BmTextureFormat::Bc1, true) => TextureFormat::Bc1RgbaUnormSrgb,
        (BmTextureFormat::Bc1, false) => TextureFormat::Bc1RgbaUnorm,
        (BmTextureFormat::Bc3, true) => TextureFormat::Bc3RgbaUnormSrgb,
        (BmTextureFormat::Bc3, false) => TextureFormat::Bc3RgbaUnorm,
        (BmTextureFormat::Bc4, _) => TextureFormat::Bc4RUnorm,
        (BmTextureFormat::Bc5, _) => TextureFormat::Bc5RgUnorm,
        (BmTextureFormat::Bc7, true) => TextureFormat::Bc7RgbaUnormSrgb,
        (BmTextureFormat::Bc7, false) => TextureFormat::Bc7RgbaUnorm,
        (BmTextureFormat::R8, _) => TextureFormat::R8Unorm,
        (BmTextureFormat::Rg8, _) => TextureFormat::Rg8Unorm,
    }
}

/// Bytes one mip level of `width` x `height` takes in `format`.
fn mip_size(format: TextureFormat, width: u32, height: u32) -> Option<usize> {
    let (block_width, block_height) = format.block_dimensions();
    let block_size = format.block_copy_size(None)?;
    let columns = width.max(1).div_ceil(block_width);
    let rows = height.max(1).div_ceil(block_height);
    usize::try_from(columns.checked_mul(rows)?.checked_mul(block_size)?).ok()
}

/// Decodes a texture upload: the payload must hold exactly the mip chain the header announces.
pub fn decode_texture(payload: &[u8]) -> Option<Image> {
    let mut reader = Reader::new(payload);
    let header = read_texture_header(&mut reader)?;

    if header.width == 0 || header.height == 0 || header.mip_count == 0 {
        return None;
    }

    let format = texture_format(header.format, header.srgb != 0);
    let mut expected = 0usize;

    for level in 0..header.mip_count {
        let width = (header.width >> level).max(1);
        let height = (header.height >> level).max(1);
        expected = expected.checked_add(mip_size(format, width, height)?)?;
    }

    let data = reader.remaining();

    if data.len() != expected {
        return None;
    }

    // `Image::new` asserts a single-level payload; the data here holds the whole mip chain.
    let mut image = Image::new_uninit(
        Extent3d {
            width: header.width,
            height: header.height,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        format,
        RenderAssetUsages::RENDER_WORLD,
    );
    image.data = Some(data.to_vec());
    image.texture_descriptor.mip_level_count = header.mip_count;

    let address = |mode: u32| match mode {
        wrap_mode::CLAMP => ImageAddressMode::ClampToEdge,
        wrap_mode::MIRROR => ImageAddressMode::MirrorRepeat,
        _ => ImageAddressMode::Repeat,
    };
    let (mag, min, mipmap) = match header.filter {
        filter_mode::POINT => (
            ImageFilterMode::Nearest,
            ImageFilterMode::Nearest,
            ImageFilterMode::Nearest,
        ),
        filter_mode::BILINEAR => (
            ImageFilterMode::Linear,
            ImageFilterMode::Linear,
            ImageFilterMode::Nearest,
        ),
        _ => (
            ImageFilterMode::Linear,
            ImageFilterMode::Linear,
            ImageFilterMode::Linear,
        ),
    };
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        address_mode_u: address(header.wrap_u),
        address_mode_v: address(header.wrap_v),
        mag_filter: mag,
        min_filter: min,
        mipmap_filter: mipmap,
        ..default()
    });

    Some(image)
}

fn build_material(material: &BmMaterial, tables: &ResourceTables) -> UrpLitMaterial {
    let texture = |handle: u32| {
        if handle == TEXTURE_NONE {
            None
        } else {
            tables.texture(handle).cloned()
        }
    };
    let base_color = Color::linear_rgba(
        material.base_color[0],
        material.base_color[1],
        material.base_color[2],
        material.base_color[3],
    );
    let emissive = LinearRgba::rgb(
        material.emission[0],
        material.emission[1],
        material.emission[2],
    );
    let unlit = material.family == BmMaterialFamily::Unlit;

    let alpha_mode = match material.alpha_mode {
        BmAlphaMode::Opaque => AlphaMode::Opaque,
        BmAlphaMode::Cutout => AlphaMode::Mask(material.alpha_cutoff),
        BmAlphaMode::Fade | BmAlphaMode::Transparent => AlphaMode::Blend,
    };

    let base = StandardMaterial {
        base_color,
        base_color_texture: texture(material.base_map),
        emissive,
        emissive_texture: texture(material.emission_map),
        normal_map_texture: texture(material.normal_map),
        metallic: material.metallic,
        perceptual_roughness: (1.0 - material.smoothness).clamp(0.045, 1.0),
        alpha_mode,
        double_sided: material.double_sided != 0,
        cull_mode: if material.double_sided != 0 {
            None
        } else {
            Some(bevy::render::render_resource::Face::Back)
        },
        unlit,
        uv_transform: bevy::math::Affine2::from_scale_angle_translation(
            Vec2::new(material.base_map_st[0], material.base_map_st[1]),
            0.0,
            Vec2::new(material.base_map_st[2], material.base_map_st[3]),
        ),
        ..default()
    };

    let mut flags = 0;
    let metallic_gloss_map = texture(material.metallic_gloss_map);
    let occlusion_map = texture(material.occlusion_map);

    if metallic_gloss_map.is_some() {
        flags |= urp_lit_flags::METALLIC_GLOSS_MAP;
    }

    if occlusion_map.is_some() {
        flags |= urp_lit_flags::OCCLUSION_MAP;
    }

    if material.scene_bounds_enabled != 0 {
        flags |= urp_lit_flags::SCENE_BOUNDS;
    }

    ExtendedMaterial {
        base,
        extension: UrpLitExtension {
            metallic: material.metallic,
            smoothness: material.smoothness,
            occlusion_strength: material.occlusion_strength,
            flags,
            bounds_min: Vec2::from_array(material.scene_bounds_min),
            bounds_max: Vec2::from_array(material.scene_bounds_max),
            metallic_gloss_map,
            occlusion_map,
        },
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn push_u32(out: &mut Vec<u8>, value: u32) {
        out.extend_from_slice(&value.to_le_bytes());
    }

    fn push_f32s(out: &mut Vec<u8>, values: &[f32]) {
        for value in values {
            out.extend_from_slice(&value.to_le_bytes());
        }
    }

    fn quad_payload() -> Vec<u8> {
        let mut out = Vec::new();
        push_u32(&mut out, 4); // vertices
        push_u32(&mut out, 6); // indices
        push_u32(&mut out, 1); // submeshes
        push_u32(&mut out, mesh_attributes::UV0);
        push_u32(&mut out, index_format::U16);
        push_f32s(
            &mut out,
            &[0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 1.0, 1.0, 0.0, 0.0, 1.0, 0.0],
        );
        push_f32s(&mut out, &[0.0, 0.0, 1.0, 0.0, 1.0, 1.0, 0.0, 1.0]);
        for index in [0u16, 1, 2, 0, 2, 3] {
            out.extend_from_slice(&index.to_le_bytes());
        }
        push_u32(&mut out, 0);
        push_u32(&mut out, 6);
        out
    }

    #[test]
    fn decodes_a_quad_and_rewinds_its_triangles() {
        let meshes = decode_mesh(&quad_payload());

        assert!(meshes.is_some());
        let Some(meshes) = meshes else {
            return;
        };
        assert_eq!(meshes.len(), 1);
        let indices: Vec<u32> = meshes
            .first()
            .and_then(|m| m.indices())
            .map(|i| i.iter().map(|v| u32::try_from(v).unwrap_or(0)).collect())
            .unwrap_or_default();
        assert_eq!(indices, vec![0, 2, 1, 0, 3, 2]);
        assert!(
            meshes
                .first()
                .is_some_and(|m| m.attribute(Mesh::ATTRIBUTE_NORMAL).is_some()),
            "normals are computed when absent"
        );
    }

    #[test]
    fn rejects_truncated_and_out_of_range_meshes() {
        let payload = quad_payload();
        assert!(decode_mesh(&payload[..payload.len() - 4]).is_none());

        let mut bad = payload.clone();
        let index_offset = 20 + 12 * 4 + 8 * 4;
        bad[index_offset] = 9;
        assert!(
            decode_mesh(&bad).is_none(),
            "an index past the vertex count is rejected"
        );
    }

    #[test]
    fn decodes_a_bc7_texture_with_its_mip_chain() {
        let mut out = Vec::new();
        push_u32(&mut out, 8);
        push_u32(&mut out, 8);
        push_u32(&mut out, 2);
        push_u32(&mut out, 6); // Bc7
        push_u32(&mut out, 1);
        push_u32(&mut out, wrap_mode::CLAMP);
        push_u32(&mut out, wrap_mode::REPEAT);
        push_u32(&mut out, filter_mode::TRILINEAR);
        // 8x8 BC7 = 4 blocks of 16 bytes; 4x4 = 1 block.
        out.extend(std::iter::repeat_n(0u8, 16 * 4 + 16));

        let image = decode_texture(&out);

        assert!(image.is_some());
        let Some(image) = image else {
            return;
        };
        assert_eq!(
            image.texture_descriptor.format,
            TextureFormat::Bc7RgbaUnormSrgb
        );
        assert_eq!(image.texture_descriptor.mip_level_count, 2);

        out.push(0);
        assert!(
            decode_texture(&out).is_none(),
            "a payload that does not match the mip chain is rejected"
        );
    }
}
