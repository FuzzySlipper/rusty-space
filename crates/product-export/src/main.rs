//! Static development/export adapter. It materializes one Rust-owned initial
//! frame; it is not a live gameplay server or browser authority.

#![forbid(unsafe_code)]

use std::{env, fs, path::PathBuf};

use rusty_template_runtime::TemplateProductService;
use thiserror::Error;

#[derive(Debug, Error)]
enum ExportError {
    #[error("usage: rusty-template-export <admitted-gameplay.json> <initial-frame.json>")]
    Usage,
    #[error("cannot read {path}: {source}")]
    Read {
        path: PathBuf,
        source: std::io::Error,
    },
    #[error("product export failed: {0}")]
    Product(#[from] rusty_template_runtime::ProductServiceError),
    #[error("cannot serialize frame: {0}")]
    Serialize(#[from] serde_json::Error),
    #[error("cannot write {path}: {source}")]
    Write {
        path: PathBuf,
        source: std::io::Error,
    },
}

fn main() -> Result<(), ExportError> {
    let mut paths = env::args_os().skip(1).map(PathBuf::from);
    let input = paths.next().ok_or(ExportError::Usage)?;
    let output = paths.next().ok_or(ExportError::Usage)?;
    if paths.next().is_some() {
        return Err(ExportError::Usage);
    }

    let bytes = fs::read(&input).map_err(|source| ExportError::Read {
        path: input.clone(),
        source,
    })?;
    let frame = TemplateProductService::admit_gameplay(&bytes)?.initial_frame()?;
    let mut canonical = serde_json::to_vec_pretty(&frame)?;
    canonical.push(b'\n');
    if let Some(parent) = output.parent() {
        fs::create_dir_all(parent).map_err(|source| ExportError::Write {
            path: parent.to_path_buf(),
            source,
        })?;
    }
    fs::write(&output, canonical).map_err(|source| ExportError::Write {
        path: output,
        source,
    })
}
