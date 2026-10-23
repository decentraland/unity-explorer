//! Generates the C# side of the ABI with csbindgen (plan § 7, step 2): one copy for the Unity-free
//! solution and one for the Unity project, both tracked as generated files.

use std::{fs, path::Path};

fn main() {
    println!("cargo:rerun-if-changed=src/lib.rs");
    println!("cargo:rerun-if-changed=../abi/src/lib.rs");

    let manifest = Path::new(env!("CARGO_MANIFEST_DIR"));
    let repo = manifest.join("../../../..");
    let net_bindings = repo.join("BevyMigration/Explorer.Net/Bindings/NativeMethods.g.cs");
    let unity_bindings =
        repo.join("Explorer/Assets/DCL/BevyMigration/Bridge/Generated/NativeMethods.g.cs");

    if let Some(parent) = net_bindings.parent() {
        let _ = fs::create_dir_all(parent);
    }

    let generated = csbindgen::Builder::default()
        .input_extern_file("../abi/src/lib.rs")
        .input_extern_file("src/lib.rs")
        .csharp_dll_name("unity_bridge")
        .csharp_namespace("DCL.BevyMigration.Bridge.Generated")
        .csharp_class_name("NativeMethods")
        .csharp_class_accessibility("public")
        .generate_csharp_file(&net_bindings);

    match generated {
        Ok(()) => {
            if let Some(parent) = unity_bindings.parent()
                && parent.exists()
            {
                let _ = fs::copy(&net_bindings, &unity_bindings);
            }
        }
        Err(error) => println!("cargo:warning=csbindgen failed: {error}"),
    }
}
