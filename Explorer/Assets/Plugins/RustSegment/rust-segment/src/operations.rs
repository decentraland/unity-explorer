use segment::message::{Identify, Track, User};
use serde_json::Value;
use time::OffsetDateTime;

pub fn new_track(
    used_id: &str,
    event_name: &str,
    properties_json: &str,
    context_json: &str,
) -> Option<Track> {
    let properties_json: Value = as_option(serde_json::from_str(properties_json))?;
    let context_json: Value = as_option(serde_json::from_str(context_json))?;

    Some(Track {
        user: User::UserId {
            user_id: used_id.to_string(),
        },
        event: event_name.to_string(),
        properties: properties_json,
        context: Some(context_json),
        timestamp: Some(OffsetDateTime::now_utc()),
        ..Default::default()
    })
}

pub fn new_identify(used_id: &str, traits_json: &str, context_json: &str) -> Option<Identify> {
    let traits_json: Value = as_option(serde_json::from_str(traits_json))?;
    let context_json: Value = as_option(serde_json::from_str(context_json))?;

    Some(Identify {
        user: User::UserId {
            user_id: used_id.to_string(),
        },
        traits: traits_json,
        context: Some(context_json),
        timestamp: Some(OffsetDateTime::now_utc()),
        ..Default::default()
    })
}

fn as_option<T, E>(result: Result<T, E>) -> Option<T> {
    match result {
        Ok(value) => Some(value),
        Err(_) => None,
    }
}
