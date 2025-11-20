use anyhow::Result;
use segment::message::{Identify, Track, User};
use serde_json::Value;
use std::ffi::{c_char, CStr};
use time::OffsetDateTime;

pub fn new_track(
    user: User,
    event_name: &str,
    properties_json: &str,
    context_json: &str,
) -> Result<Track> {
    let properties_json: Value = serde_json::from_str(properties_json)?;
    let context_json: Value = serde_json::from_str(context_json)?;

    Ok(Track {
        user,
        event: event_name.to_string(),
        properties: properties_json,
        context: Some(context_json),
        timestamp: Some(OffsetDateTime::now_utc()),
        ..Default::default()
    })
}

pub fn new_identify(user: User, traits_json: &str, context_json: &str) -> Result<Identify> {
    let traits_json: Value = serde_json::from_str(traits_json)?;
    let context_json: Value = serde_json::from_str(context_json)?;

    Ok(Identify {
        user,
        traits: traits_json,
        context: Some(context_json),
        timestamp: Some(OffsetDateTime::now_utc()),
        ..Default::default()
    })
}

///
/// # Safety
///
/// Caller must provide valid pointer to a c style str
///
pub unsafe fn user_from(used_id: *const c_char, anon_id: *const c_char) -> Option<User> {
    if used_id.is_null() && anon_id.is_null() {
        return None;
    }

    if !used_id.is_null() && !anon_id.is_null() {
        let user = as_str(used_id);
        let anon = as_str(anon_id);
        let output = User::Both {
            user_id: user.to_string(),
            anonymous_id: anon.to_string(),
        };
        return Some(output);
    }

    if !used_id.is_null() {
        let user = as_str(used_id);
        let output = User::UserId {
            user_id: user.to_string(),
        };
        return Some(output);
    }

    let anon = as_str(anon_id);
    Some(User::AnonymousId {
        anonymous_id: anon.to_string(),
    })
}

///
/// # Safety
///
/// Caller must provide valid pointer to a c style str
///
pub unsafe fn as_str<'a>(chars: *const c_char) -> &'a str {
    let c_str = unsafe { CStr::from_ptr(chars) };
    c_str.to_str().unwrap()
}
