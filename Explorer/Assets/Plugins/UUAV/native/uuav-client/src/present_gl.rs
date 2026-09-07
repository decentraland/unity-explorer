//! OpenGL Core backend of the Linux presentation: imports the helper's
//! opaque memory fds via `GL_EXT_memory_object_fd` (the GL side of
//! Vulkan/GL interop — same driver, same GPU) and copies published
//! slots with `glCopyImageSubData` into owned presentation textures.
//!
//! Every GL call runs on Unity's render thread with the context
//! current (the render event); the entry points load lazily there via
//! `glXGetProcAddressARB`/`eglGetProcAddress` from a dlopen'd
//! `libGL.so.1`. Objects whose owner drops off-thread (a freed player's
//! mirror) go onto a deletion queue drained at the next render-thread
//! call — GL calls without a current context are undefined.

use crate::present::{PLANES, SLOTS};
use anyhow::{Context as _, Result, anyhow, bail, ensure};
use parking_lot::Mutex;
use std::ffi::CString;
use std::os::fd::{IntoRawFd as _, OwnedFd};
use std::os::raw::{c_char, c_int, c_uint, c_void};
use std::sync::OnceLock;
use uuav_ipc::protocol::{TextureImportWire, TexturePlaneWire};

type GLenum = c_uint;
type GLuint = c_uint;
type GLint = c_int;
type GLsizei = c_int;
type GLuint64 = u64;

const GL_TEXTURE_2D: GLenum = 0x0DE1;
const GL_R8: GLenum = 0x8229;
const GL_RG8: GLenum = 0x822B;
const GL_NO_ERROR: GLenum = 0;
const GL_HANDLE_TYPE_OPAQUE_FD_EXT: GLenum = 0x9586;
const GL_TEXTURE_TILING_EXT: GLenum = 0x9580;
const GL_OPTIMAL_TILING_EXT: GLenum = 0x9584;
const GL_LINEAR_TILING_EXT: GLenum = 0x9585;

#[allow(clippy::type_complexity)]
struct GlFns {
    gen_textures: unsafe extern "C" fn(GLsizei, *mut GLuint),
    delete_textures: unsafe extern "C" fn(GLsizei, *const GLuint),
    bind_texture: unsafe extern "C" fn(GLenum, GLuint),
    tex_parameteri: unsafe extern "C" fn(GLenum, GLenum, GLint),
    tex_storage_2d: unsafe extern "C" fn(GLenum, GLsizei, GLenum, GLsizei, GLsizei),
    create_memory_objects: unsafe extern "C" fn(GLsizei, *mut GLuint),
    delete_memory_objects: unsafe extern "C" fn(GLsizei, *const GLuint),
    import_memory_fd: unsafe extern "C" fn(GLuint, GLuint64, GLenum, c_int),
    tex_storage_mem_2d:
        unsafe extern "C" fn(GLenum, GLsizei, GLenum, GLsizei, GLsizei, GLuint, GLuint64),
    copy_image_sub_data: unsafe extern "C" fn(
        GLuint,
        GLenum,
        GLint,
        GLint,
        GLint,
        GLint,
        GLuint,
        GLenum,
        GLint,
        GLint,
        GLint,
        GLint,
        GLsizei,
        GLsizei,
        GLsizei,
    ),
    get_error: unsafe extern "C" fn() -> GLenum,
}

static GL: OnceLock<Result<GlFns, String>> = OnceLock::new();

type GetProc = unsafe extern "C" fn(*const c_char) -> *const c_void;

/// Objects to delete on the next render-thread entry.
static DELETE_QUEUE: Mutex<Vec<(Vec<GLuint>, Vec<GLuint>)>> = Mutex::new(Vec::new());

fn gl() -> Result<&'static GlFns> {
    let loaded = GL.get_or_init(|| load_gl().map_err(|e| e.to_string()));
    loaded.as_ref().map_err(|e| anyhow!("{e}"))
}

// the load! macro transmutes each pointer to its field's fn type; the
// annotation lives on the field declarations above
#[allow(clippy::missing_transmute_annotations)]
fn load_gl() -> Result<GlFns> {
    unsafe {
        let lib = libc::dlopen(c"libGL.so.1".as_ptr(), libc::RTLD_NOW | libc::RTLD_GLOBAL);
        let lib = if lib.is_null() {
            libc::dlopen(c"libGL.so".as_ptr(), libc::RTLD_NOW | libc::RTLD_GLOBAL)
        } else {
            lib
        };
        ensure!(!lib.is_null(), "dlopen(libGL.so.1) failed");

        let get_proc: Option<GetProc> = {
            let glx = libc::dlsym(lib, c"glXGetProcAddressARB".as_ptr());
            let glx = if glx.is_null() {
                libc::dlsym(lib, c"glXGetProcAddress".as_ptr())
            } else {
                glx
            };
            if glx.is_null() {
                let egl = libc::dlopen(c"libEGL.so.1".as_ptr(), libc::RTLD_NOW);
                if egl.is_null() {
                    None
                } else {
                    let egl_get = libc::dlsym(egl, c"eglGetProcAddress".as_ptr());
                    (!egl_get.is_null()).then(|| std::mem::transmute::<*mut c_void, GetProc>(egl_get))
                }
            } else {
                Some(std::mem::transmute::<*mut c_void, GetProc>(glx))
            }
        };
        let get_proc = get_proc.context("no glXGetProcAddress/eglGetProcAddress")?;

        macro_rules! load {
            ($name:literal) => {{
                let cname = CString::new($name).context("symbol name")?;
                let mut ptr = get_proc(cname.as_ptr());
                if ptr.is_null() {
                    ptr = libc::dlsym(lib, cname.as_ptr());
                }
                ensure!(!ptr.is_null(), concat!($name, " is unavailable"));
                std::mem::transmute(ptr)
            }};
        }

        Ok(GlFns {
            gen_textures: load!("glGenTextures"),
            delete_textures: load!("glDeleteTextures"),
            bind_texture: load!("glBindTexture"),
            tex_parameteri: load!("glTexParameteri"),
            tex_storage_2d: load!("glTexStorage2D"),
            create_memory_objects: load!("glCreateMemoryObjectsEXT"),
            delete_memory_objects: load!("glDeleteMemoryObjectsEXT"),
            import_memory_fd: load!("glImportMemoryFdEXT"),
            tex_storage_mem_2d: load!("glTexStorageMem2DEXT"),
            copy_image_sub_data: load!("glCopyImageSubData"),
            get_error: load!("glGetError"),
        })
    }
}

/// Drains queued deletions; render thread only.
fn drain_deletions(fns: &GlFns) {
    let queued: Vec<_> = DELETE_QUEUE.lock().drain(..).collect();
    for (textures, memories) in queued {
        unsafe {
            if !textures.is_empty() {
                (fns.delete_textures)(textures.len() as GLsizei, textures.as_ptr());
            }
            if !memories.is_empty() {
                (fns.delete_memory_objects)(memories.len() as GLsizei, memories.as_ptr());
            }
        }
    }
}

fn check_gl(fns: &GlFns, what: &str) -> Result<()> {
    let error = unsafe { (fns.get_error)() };
    ensure!(error == GL_NO_ERROR, "{what}: GL error {error:#x}");
    Ok(())
}

/// The 6 imported slot textures of one generation, slot-major.
pub struct ImportedSlots {
    textures: Vec<GLuint>,
    memories: Vec<GLuint>,
}

impl Drop for ImportedSlots {
    fn drop(&mut self) {
        // may run off the render thread: defer to the next GL entry
        DELETE_QUEUE
            .lock()
            .push((std::mem::take(&mut self.textures), std::mem::take(&mut self.memories)));
    }
}

/// The owned presentation textures C# wraps.
pub struct PresentationPlanes {
    y: GLuint,
    uv: GLuint,
    pub width: u32,
    pub height: u32,
}

impl Drop for PresentationPlanes {
    fn drop(&mut self) {
        DELETE_QUEUE.lock().push((vec![self.y, self.uv], Vec::new()));
    }
}

impl PresentationPlanes {
    /// Render thread (context current).
    pub fn new(width: u32, height: u32) -> Result<Self> {
        let fns = gl()?;
        drain_deletions(fns);
        let make = |internal: GLenum, w: u32, h: u32| -> Result<GLuint> {
            let mut texture: GLuint = 0;
            unsafe {
                (fns.gen_textures)(1, &mut texture);
                (fns.bind_texture)(GL_TEXTURE_2D, texture);
                (fns.tex_storage_2d)(
                    GL_TEXTURE_2D,
                    1,
                    internal,
                    GLsizei::try_from(w).context("width")?,
                    GLsizei::try_from(h).context("height")?,
                );
                (fns.bind_texture)(GL_TEXTURE_2D, 0);
            }
            check_gl(fns, "presentation texture")?;
            Ok(texture)
        };
        let y = make(GL_R8, width, height)?;
        let uv = match make(
            GL_RG8,
            width.checked_div(2).unwrap_or(0),
            height.checked_div(2).unwrap_or(0),
        ) {
            Ok(uv) => uv,
            Err(e) => {
                unsafe { (fns.delete_textures)(1, &y) };
                return Err(e);
            }
        };
        Ok(Self {
            y,
            uv,
            width,
            height,
        })
    }

    /// The GL texture name C# hands to `CreateExternalTexture`.
    pub const fn texture_ptr(&self, plane: i32) -> *const c_void {
        let name = if plane == 0 { self.y } else { self.uv };
        name as usize as *const c_void
    }
}

/// Imports the 6 opaque memory fds of a generation as textures; render
/// thread (context current) — called from the first present after the
/// set completed.
pub fn import_generation(
    import: TextureImportWire,
    layout: &[TexturePlaneWire],
    fds: [[Option<OwnedFd>; PLANES]; SLOTS],
    width: u32,
    height: u32,
) -> Result<ImportedSlots> {
    let TextureImportWire::OpaqueFd { tiling_optimal } = import else {
        bail!("a GL client expects opaque-fd texture sets");
    };
    let fns = gl()?;
    drain_deletions(fns);

    let mut slots = ImportedSlots {
        textures: Vec::with_capacity(SLOTS * PLANES),
        memories: Vec::with_capacity(SLOTS * PLANES),
    };
    for (slot_index, slot) in fds.into_iter().enumerate() {
        for (plane_index, fd) in slot.into_iter().enumerate() {
            let fd = fd.ok_or_else(|| anyhow!("missing fd for slot {slot_index}"))?;
            let wire = layout
                .iter()
                .find(|p| {
                    usize::from(p.slot) == slot_index && usize::from(p.plane) == plane_index
                })
                .ok_or_else(|| anyhow!("missing layout for slot {slot_index}"))?;
            let (internal, plane_width, plane_height) = if plane_index == 0 {
                (GL_R8, width, height)
            } else {
                (
                    GL_RG8,
                    width.checked_div(2).unwrap_or(0),
                    height.checked_div(2).unwrap_or(0),
                )
            };

            let mut memory: GLuint = 0;
            let mut texture: GLuint = 0;
            unsafe {
                (fns.create_memory_objects)(1, &mut memory);
                // GL takes ownership of the fd on import
                (fns.import_memory_fd)(
                    memory,
                    wire.size,
                    GL_HANDLE_TYPE_OPAQUE_FD_EXT,
                    fd.into_raw_fd(),
                );
                (fns.gen_textures)(1, &mut texture);
                (fns.bind_texture)(GL_TEXTURE_2D, texture);
                (fns.tex_parameteri)(
                    GL_TEXTURE_2D,
                    GL_TEXTURE_TILING_EXT,
                    if tiling_optimal {
                        GL_OPTIMAL_TILING_EXT
                    } else {
                        GL_LINEAR_TILING_EXT
                    } as GLint,
                );
                (fns.tex_storage_mem_2d)(
                    GL_TEXTURE_2D,
                    1,
                    internal,
                    GLsizei::try_from(plane_width).context("width")?,
                    GLsizei::try_from(plane_height).context("height")?,
                    memory,
                    0,
                );
                (fns.bind_texture)(GL_TEXTURE_2D, 0);
            }
            slots.memories.push(memory);
            slots.textures.push(texture);
            check_gl(fns, "import slot plane")?;
        }
    }
    Ok(slots)
}

/// Copies one published slot into the presentation textures; render
/// thread (context current).
pub fn copy_slot(slots: &ImportedSlots, slot: u8, planes: &PresentationPlanes) -> Result<()> {
    let fns = gl()?;
    drain_deletions(fns);
    let base = usize::from(slot).saturating_mul(PLANES);
    let source_y = *slots.textures.get(base).context("published slot out of range")?;
    let source_uv = *slots
        .textures
        .get(base.saturating_add(1))
        .context("published slot out of range")?;

    let copy = |source: GLuint, dest: GLuint, w: u32, h: u32| -> Result<()> {
        unsafe {
            (fns.copy_image_sub_data)(
                source,
                GL_TEXTURE_2D,
                0,
                0,
                0,
                0,
                dest,
                GL_TEXTURE_2D,
                0,
                0,
                0,
                0,
                GLsizei::try_from(w).context("width")?,
                GLsizei::try_from(h).context("height")?,
                1,
            );
        }
        Ok(())
    };
    copy(source_y, planes.y, planes.width, planes.height)?;
    copy(
        source_uv,
        planes.uv,
        planes.width.checked_div(2).unwrap_or(0),
        planes.height.checked_div(2).unwrap_or(0),
    )?;
    check_gl(fns, "slot copy")
}
