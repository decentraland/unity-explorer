use std::{
    ffi::{c_char, CStr},
    ptr,
};

use crate::SIGN_SERVER;

/// # Safety
///
/// The foreign language must only provide valid pointers
#[no_mangle]
pub unsafe extern "C" fn sign_server_initialize(data: *const u8, len: usize) -> bool {
    let data = unsafe { std::slice::from_raw_parts(data, len) };
    SIGN_SERVER.setup(data).is_ok()
}

/// # Safety
///
/// The foreign language must only provide valid pointers that handles 65 bytes
#[no_mangle]
pub unsafe extern "C" fn sign_server_sign_message(message: *const c_char, res_ptr: *mut *const u8) {
    let string_message = CStr::from_ptr(message).to_str().unwrap();
    let signature = SIGN_SERVER.sign_message(string_message).unwrap();
    ptr::copy(signature.as_ptr(), res_ptr as *mut u8, signature.len());
}
