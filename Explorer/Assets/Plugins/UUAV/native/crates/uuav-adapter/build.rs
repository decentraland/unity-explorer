
use std::fmt::Write as _;
use std::path::Path;

const FRAME_INFO_PATH: &str = "../../src/frame_info.rs";

const EXPECTED: &str = "75cb6fbb5f240967f06bd645da00dbbf678b8fe9e72a3087c48e3bf17d7e0ca1";

fn main() {
    println!("cargo:rerun-if-changed={FRAME_INFO_PATH}");
    println!("cargo:rerun-if-changed=build.rs");
    println!("cargo::rustc-env=UUAV_FRAME_INFO_DECL_SHA256={EXPECTED}");

    write_guard("");

    let actual = match declaration_digest(Path::new(FRAME_INFO_PATH)) {
        Ok(digest) => digest,
        Err(reason) => fail(&reason),
    };

    if actual != EXPECTED {
        fail(&format!(
            "the frozen core's FrameInfo declaration changed: FRAME_INFO_DECL_SHA256 expected \
             {EXPECTED}, found {actual}. The media core under \
             Explorer/Assets/Plugins/UUAV/native/src is frozen; if the change is intended, \
             update FRAME_INFO_DECL_SHA256 in crates/uuav-abi/src/lib.rs, the golden offsets in \
             crates/uuav-abi/src/layout.rs, and the FrameInfo declaration in \
             crates/uuav-abi/src/lib.rs together."
        ));
    }
}

fn fail(reason: &str) -> ! {
    let escaped = reason.replace('\\', r"\\").replace('"', "\\\"");
    write_guard(&format!("compile_error!(\"{escaped}\");\n"));
    println!("cargo::error={reason}");
    std::process::exit(1);
}

fn write_guard(contents: &str) {
    let Ok(out_dir) = std::env::var("OUT_DIR") else {
        return;
    };
    let _ = std::fs::write(Path::new(&out_dir).join("guard.rs"), contents);
}

fn declaration_digest(path: &Path) -> Result<String, String> {
    let source = std::fs::read_to_string(path)
        .map_err(|error| format!("cannot read {}: {error}", path.display()))?;
    let declaration = normalise(&source).ok_or_else(|| {
        format!(
            "no `pub struct FrameInfo {{ ... }}` block found in {} - the frozen declaration the \
             FRAME_INFO_DECL_SHA256 guard digests is gone",
            path.display()
        )
    })?;
    Ok(sha256(declaration.as_bytes()))
}

fn normalise(source: &str) -> Option<String> {
    let mut lines = source.lines().enumerate();
    let declaration = lines
        .by_ref()
        .find(|(_, line)| line.trim_start().starts_with("pub struct FrameInfo"))
        .map(|(index, _)| index)?;
    let end = lines
        .find(|(_, line)| line.starts_with('}'))
        .map(|(index, _)| index)?;
    let attributes = source.lines().take(declaration).fold(0usize, |run, line| {
        if line.trim_start().starts_with("#[") {
            run.saturating_add(1)
        } else {
            0
        }
    });
    let start = declaration.checked_sub(attributes)?;

    let mut out = String::new();
    for line in source.lines().take(end.checked_add(1)?).skip(start) {
        let code = line.find("//").map_or(line, |at| line.split_at(at).0);
        for word in code.split_whitespace() {
            if !out.is_empty() {
                out.push(' ');
            }
            out.push_str(word);
        }
    }
    Some(out)
}

#[rustfmt::skip]
const K: [u32; 64] = [
    0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
    0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
    0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
    0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
    0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
    0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
    0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
    0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
];

fn sha256(input: &[u8]) -> String {
    #[rustfmt::skip]
    let mut h: [u32; 8] = [
        0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
        0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19,
    ];

    let mut message = input.to_vec();
    let bit_length = (input.len() as u64).wrapping_mul(8);
    message.push(0x80);
    while message.len() % 64 != 56 {
        message.push(0);
    }
    message.extend_from_slice(&bit_length.to_be_bytes());

    for chunk in message.chunks_exact(64) {
        let mut w = [0u32; 64];
        for (index, word) in chunk.chunks_exact(4).enumerate() {
            w[index] = u32::from_be_bytes([word[0], word[1], word[2], word[3]]);
        }
        for index in 16..64 {
            let x = w[index - 15];
            let y = w[index - 2];
            let s0 = x.rotate_right(7) ^ x.rotate_right(18) ^ (x >> 3);
            let s1 = y.rotate_right(17) ^ y.rotate_right(19) ^ (y >> 10);
            w[index] = w[index - 16]
                .wrapping_add(s0)
                .wrapping_add(w[index - 7])
                .wrapping_add(s1);
        }

        let [mut a, mut b, mut c, mut d, mut e, mut f, mut g, mut hh] = h;
        for index in 0..64 {
            let s1 = e.rotate_right(6) ^ e.rotate_right(11) ^ e.rotate_right(25);
            let ch = (e & f) ^ ((!e) & g);
            let temp1 = hh
                .wrapping_add(s1)
                .wrapping_add(ch)
                .wrapping_add(K[index])
                .wrapping_add(w[index]);
            let s0 = a.rotate_right(2) ^ a.rotate_right(13) ^ a.rotate_right(22);
            let maj = (a & b) ^ (a & c) ^ (b & c);
            let temp2 = s0.wrapping_add(maj);
            hh = g;
            g = f;
            f = e;
            e = d.wrapping_add(temp1);
            d = c;
            c = b;
            b = a;
            a = temp1.wrapping_add(temp2);
        }
        for (slot, value) in h.iter_mut().zip([a, b, c, d, e, f, g, hh]) {
            *slot = slot.wrapping_add(value);
        }
    }

    let mut out = String::with_capacity(64);
    for word in h {
        let _ = write!(out, "{word:08x}");
    }
    out
}
