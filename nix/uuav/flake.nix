{
  description = "Pinned inputs and hermetic verification for the UUAV native binaries";

  # ffmpeg-src is pinned the way ~/one/flake.nix pins abgen: an external build
  # input carried as a flake input, so flake.lock records both the commit and a
  # narHash of the source tree. The narHash is the stronger of the two - a git
  # tag can be moved, a content hash cannot - and it is what lets a reviewer
  # prove the FFmpeg sources behind a shipped dylib are the ones claimed.
  #
  # Deliberately NOT pinned here: nixpkgs' own ffmpeg. That is the technique
  # third-reopen/unitedav uses (pkgs.ffmpeg.override { withGPL = false; }), and
  # it does not transfer - this plugin needs --disable-everything with an
  # explicit allowlist and --install-name-dir='@rpath', neither of which the
  # nixpkgs override interface exposes.
  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-26.05";
    ffmpeg-src = {
      url = "github:FFmpeg/FFmpeg/n8.1";
      flake = false;
    };
  };

  outputs = { self, nixpkgs, ffmpeg-src }:
    let
      systems = [ "x86_64-linux" "aarch64-linux" "x86_64-darwin" "aarch64-darwin" ];
      forAllSystems = f: nixpkgs.lib.genAttrs systems (system: f nixpkgs.legacyPackages.${system});
    in
    {
      # The pinned FFmpeg source, exposed so `nix build .#ffmpeg-source` gives a
      # store path a reviewer can diff against whatever a build machine used.
      packages = forAllSystems (pkgs: {
        ffmpeg-source = pkgs.runCommand "ffmpeg-${ffmpeg-src.shortRev or "n8.1"}-source" { } ''
          mkdir -p $out
          cp -r ${ffmpeg-src}/. $out/
          printf '%s\n' "${ffmpeg-src.rev}" > $out/.pinned-rev
        '';
        default = self.packages.${pkgs.system}.ffmpeg-source;
      });

      # `nix run .#verify` - the same gate CI runs, with a pinned interpreter.
      apps = forAllSystems (pkgs: {
        verify = {
          type = "app";
          program = toString (pkgs.writeShellScript "uuav-verify" ''
            exec ${pkgs.python3}/bin/python3 \
              "$(git rev-parse --show-toplevel)/scripts/uuav/verify-binaries.py" "$@"
          '');
        };
        default = self.apps.${pkgs.system}.verify;
      });

      devShells = forAllSystems (pkgs: {
        default = pkgs.mkShell {
          packages = [ pkgs.python3 pkgs.jq pkgs.curl pkgs.unzip pkgs.cacert ];
          shellHook = ''
            echo "UUAV packaging shell"
            echo "  ffmpeg source pin: ${ffmpeg-src.rev}"
            echo "  verify:  python3 scripts/uuav/verify-binaries.py"
            echo "  relock:  python3 scripts/uuav/verify-binaries.py --update"
          '';
        };
      });
    };
}
