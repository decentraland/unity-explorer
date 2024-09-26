use segment::message::{Identify, Track, User};
use time::OffsetDateTime;

pub fn new_track(
    used_id: &str,
    event_name: &str,
    properties_json: &str,
    context_json: &str,
) -> Option<Track> {
    let properties = serde_json::from_str(properties_json);
    if properties.is_err() {
        return None;
    }

    let context = serde_json::from_str(context_json);
    if context.is_err() {
        return None;
    }

    Some(Track {
        user: User::UserId {
            user_id: used_id.to_string(),
        },
        event: event_name.to_string(),
        properties: properties.unwrap(),
        context: context.unwrap(),
        timestamp: Some(OffsetDateTime::now_utc()),
        ..Default::default()
    })
}

pub fn new_identify(used_id: &str, traits_json: &str, context_json: &str) -> Option<Identify> {
    let traits = serde_json::from_str(traits_json);
    if traits.is_err() {
        return None;
    }

    let context = serde_json::from_str(context_json);
    if context.is_err() {
        return None;
    }

    Some(Identify {
        user: User::UserId {
            user_id: used_id.to_string(),
        },
        traits: traits.unwrap(),
        context: context.unwrap(),
        timestamp: Some(OffsetDateTime::now_utc()),
        ..Default::default()
    })
}
