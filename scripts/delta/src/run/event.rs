//! What happened in a workflow. Events are logged, not stored: the `Run` document holds the
//! current state only.

use std::fmt;

use crate::publishing::values::ETag;
use crate::releases::tag::Tag;
use crate::run::stage::DropReason;

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Event {
    TargetConfirmed(Tag),
    StaleCleared(String),
    TargetDownloaded {
        size: u64,
        etag: ETag,
    },
    BaseDownloaded(Tag),
    BaseExtracted(Tag),
    TargetInspected {
        entries: usize,
        signed: usize,
    },
    BaseInspected {
        base: Tag,
        entries: usize,
    },
    PatchSetEncoded {
        base: Tag,
        patched: usize,
        whole: usize,
        deleted: usize,
        bytes: u64,
    },
    BaseVerified(Tag),
    BaseDropped {
        base: Tag,
        reason: DropReason,
    },
    ManifestAssembled {
        bases: usize,
    },
    TargetRechecked(Tag),
    PatchesUploaded(String),
    ManifestPublished(String),
}

impl fmt::Display for Event {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Event::TargetConfirmed(tag) => write!(f, "{tag} is not published"),
            Event::StaleCleared(prefix) => write!(f, "removed earlier patches under {prefix}/"),
            Event::TargetDownloaded { size, etag } => {
                write!(f, "downloaded the target zip ({size} bytes, ETag {etag})")
            }
            Event::BaseDownloaded(base) => write!(f, "downloaded {base}"),
            Event::BaseExtracted(base) => write!(f, "extracted {base}"),
            Event::TargetInspected { entries, signed } => {
                write!(f, "target build: {entries} entries, {signed} signed artifacts")
            }
            Event::BaseInspected { base, entries } => write!(f, "{base}: {entries} entries"),
            Event::PatchSetEncoded {
                base,
                patched,
                whole,
                deleted,
                bytes,
            } => {
                write!(
                    f,
                    "{base}: {patched} patched, {whole} whole, {deleted} deleted, {bytes} bytes to download"
                )
            }
            Event::BaseVerified(base) => write!(f, "{base}: rebuilt build matches the target, signatures included"),
            Event::BaseDropped { base, reason } => write!(f, "{base} dropped: {reason}"),
            Event::ManifestAssembled { bases } => write!(f, "manifest assembled with {bases} bases"),
            Event::TargetRechecked(tag) => write!(f, "{tag} is still unpublished and its zip is unchanged"),
            Event::PatchesUploaded(prefix) => write!(f, "patches uploaded under {prefix}/"),
            Event::ManifestPublished(prefix) => write!(f, "manifest published at {prefix}/manifest.json"),
        }
    }
}
