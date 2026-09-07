{ system ? "x86_64-linux" }:

let
  revisionPinnedForRebuildDeterminism = "0e251e24a4f24e036a084b6b4b2d2491af4167f4";

  nixpkgsPinnedTarball = builtins.fetchTarball {
    url = "https://github.com/NixOS/nixpkgs/archive/${revisionPinnedForRebuildDeterminism}.tar.gz";
    sha256 = "118n3xlp9fyf52588yhxa0a5xyi0gchci09l0vblrm7m8zimvln8";
  };
in
import nixpkgsPinnedTarball {
  inherit system;
  config = { };
  overlays = [ ];
}
