use ffmpeg_sys_next as ff;

use crate::ffutil::OwnedFrame;

#[repr(C)]
#[derive(Clone, Copy)]
pub struct FrameInfo {
    pub yuv_to_rgb: [f32; 12],
    pub uv_transform: [f32; 6],
    pub visible_width: u32,
    pub visible_height: u32,
    pub plane_width: [u32; 2],
    pub plane_height: [u32; 2],
    pub colorspace: i32,
    pub color_range: i32,
    pub color_primaries: i32,
    pub rotation: i32,
    pub bit_depth: u32,
    pub frame_index: u64,
    pub surface_generation: u64,
    pub planes: [usize; 2],
}

impl FrameInfo {
    pub(crate) fn of(frame: &OwnedFrame, bit_depth: u32) -> Self {
        let width = frame.width().max(0) as u32;
        let height = frame.height().max(0) as u32;
        let colorspace = frame.colorspace();
        let color_range = frame.color_range();
        let rotation = frame.display_rotation();
        Self {
            yuv_to_rgb: yuv_to_rgb(colorspace, color_range, bit_depth, height),
            uv_transform: uv_transform(rotation, 1.0, 1.0),
            visible_width: width,
            visible_height: height,
            plane_width: [width, width / 2],
            plane_height: [height, height / 2],
            colorspace: colorspace as i32,
            color_range: color_range as i32,
            color_primaries: frame.color_primaries() as i32,
            rotation,
            bit_depth,
            frame_index: 0,
            surface_generation: 0,
            planes: [0; 2],
        }
    }

    pub(crate) fn fit_planes(&mut self, planes: [(u32, u32); 2]) {
        let [(y_width, y_height), (uv_width, uv_height)] = planes;
        self.plane_width = [y_width, uv_width];
        self.plane_height = [y_height, uv_height];
        self.uv_transform = uv_transform(
            self.rotation,
            visible_ratio(self.visible_width, y_width),
            visible_ratio(self.visible_height, y_height),
        );
    }
}

fn visible_ratio(visible: u32, allocated: u32) -> f32 {
    if visible == 0 || allocated == 0 || visible >= allocated {
        1.0
    } else {
        visible as f32 / allocated as f32
    }
}

fn uv_transform(rotation: i32, sx: f32, sy: f32) -> [f32; 6] {
    match rotation {
        90 => [0.0, -sx, sx, -sy, 0.0, sy],
        180 => [-sx, 0.0, sx, 0.0, sy, 0.0],
        270 => [0.0, sx, 0.0, sy, 0.0, 0.0],
        _ => [sx, 0.0, 0.0, 0.0, -sy, sy],
    }
}

const fn luma_coefficients(colorspace: ff::AVColorSpace, height: u32) -> (f64, f64) {
    use ff::AVColorSpace::{
        AVCOL_SPC_BT470BG, AVCOL_SPC_BT709, AVCOL_SPC_BT2020_CL, AVCOL_SPC_BT2020_NCL,
        AVCOL_SPC_FCC, AVCOL_SPC_SMPTE170M, AVCOL_SPC_SMPTE240M,
    };
    match colorspace {
        AVCOL_SPC_BT709 => (0.2126, 0.0722),
        AVCOL_SPC_FCC => (0.30, 0.11),
        AVCOL_SPC_BT470BG | AVCOL_SPC_SMPTE170M => (0.299, 0.114),
        AVCOL_SPC_SMPTE240M => (0.212, 0.087),
        AVCOL_SPC_BT2020_NCL | AVCOL_SPC_BT2020_CL => (0.2627, 0.0593),
        _ if height >= 720 => (0.2126, 0.0722),
        _ => (0.299, 0.114),
    }
}

#[allow(clippy::similar_names)]
fn yuv_to_rgb(
    colorspace: ff::AVColorSpace,
    range: ff::AVColorRange,
    bit_depth: u32,
    height: u32,
) -> [f32; 12] {
    let (kr, kb) = luma_coefficients(colorspace, height);
    let kg = 1.0 - kr - kb;

    let (peak, code, unit) = if bit_depth > 8 {
        (1023.0, 65535.0 / 64.0, 4.0)
    } else {
        (255.0, 255.0, 1.0)
    };
    let (ay, by, ac, bc) = match range {
        ff::AVColorRange::AVCOL_RANGE_JPEG => {
            (code / peak, 0.0, code / peak, -128.0 * unit / peak)
        }
        _ => (
            code / (219.0 * unit),
            -16.0 / 219.0,
            code / (224.0 * unit),
            -128.0 / 224.0,
        ),
    };

    let cr_r = 2.0 * (1.0 - kr);
    let cb_g = -2.0 * kb * (1.0 - kb) / kg;
    let cr_g = -2.0 * kr * (1.0 - kr) / kg;
    let cb_b = 2.0 * (1.0 - kb);

    [
        ay as f32,
        0.0,
        (cr_r * ac) as f32,
        cr_r.mul_add(bc, by) as f32,
        ay as f32,
        (cb_g * ac) as f32,
        (cr_g * ac) as f32,
        (cb_g + cr_g).mul_add(bc, by) as f32,
        ay as f32,
        (cb_b * ac) as f32,
        0.0,
        cb_b.mul_add(bc, by) as f32,
    ]
}

#[cfg(test)]
#[allow(clippy::indexing_slicing, clippy::arithmetic_side_effects)]
mod tests {
    use super::*;

    fn rgb(space: ff::AVColorSpace, range: ff::AVColorRange, yuv: [f32; 3]) -> [f32; 3] {
        let m = yuv_to_rgb(space, range, 8, 1080);
        let [y, cb, cr] = yuv;
        [0, 4, 8].map(|i| m[i] * y + m[i + 1] * cb + m[i + 2] * cr + m[i + 3])
    }

    #[test]
    fn range_and_matrix_come_from_the_stream() {
        use ff::AVColorRange::{AVCOL_RANGE_JPEG, AVCOL_RANGE_MPEG};
        use ff::AVColorSpace::{AVCOL_SPC_BT709, AVCOL_SPC_SMPTE170M};

        let neutral = 128.0 / 255.0;
        let white = rgb(AVCOL_SPC_BT709, AVCOL_RANGE_JPEG, [1.0, neutral, neutral]);
        assert!(white.iter().all(|c| (c - 1.0).abs() < 1e-3), "{white:?}");

        let red601 = rgb(AVCOL_SPC_SMPTE170M, AVCOL_RANGE_MPEG, [0.5, 0.0, 1.0]);
        let red709 = rgb(AVCOL_SPC_BT709, AVCOL_RANGE_MPEG, [0.5, 0.0, 1.0]);
        let shift = (red601[1] - red709[1]).abs();
        assert!(shift > 8.0 / 255.0, "{shift} {red601:?} {red709:?}");
    }

    #[test]
    fn rotation_turns_the_sampler() {
        assert_eq!(uv_transform(0, 1.0, 1.0), [1.0, 0.0, 0.0, 0.0, -1.0, 1.0]);
        let m = uv_transform(90, 1.0, 1.0);
        assert_eq!((m[1] + m[2], m[4] + m[5]), (0.0, 1.0));
    }
}
