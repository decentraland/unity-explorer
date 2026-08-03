{
  description = "unity-explorer: local media test harness for the UUAV native video plugin";

  # This flake exists so `nix run .#uuav-test` resolves from the repository root,
  # which is where a contributor stands. It deliberately does NOT consume
  # nix/uuav/flake.nix: that flake pins FFmpeg sources for binary provenance and
  # locks on its own cadence, and making the harness an input of it - or it an
  # input of the harness - would couple two unrelated lock files. They stay two
  # independent flakes in one repository.
  #
  # dcl-one-sdk is likewise NOT an input. It lives in the private dcl-one
  # monorepo (~/one/catalyrst/crates/dcl-one-sdk, exported standalone by
  # ~/one/scripts/export/export-dcl-one-sdk.sh), so pinning it here would put a
  # machine-local path in flake.lock and break the flake for everyone else. The
  # runner resolves it at run time instead: DCL_ONE_SDK_BIN, then PATH, then
  # `nix run "$UUAV_TEST_SDK_FLAKE#dcl-one-sdk"`.
  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-26.05";
  };

  outputs = { self, nixpkgs }:
    let
      systems = [ "x86_64-linux" "aarch64-linux" "x86_64-darwin" "aarch64-darwin" ];
      forAllSystems = f: nixpkgs.lib.genAttrs systems (system: f nixpkgs.legacyPackages.${system});

      harnessFor = pkgs: import ./nix/uuav-test {
        inherit pkgs;
        sceneSrc = ./test-scenes/uuav-media-sandbox;
      };
    in
    {
      packages = forAllSystems (pkgs:
        let harness = harnessFor pkgs; in
        {
          uuav-test = harness.runner;
          uuav-test-media = harness.media;
          uuav-test-cases = harness.cases;
          default = harness.runner;
        });

      apps = forAllSystems (pkgs: rec {
        uuav-test = {
          type = "app";
          program = "${(harnessFor pkgs).runner}/bin/uuav-test";
        };
        default = uuav-test;
      });

      nixosModules.uuav-test = { pkgs, ... }: {
        imports = [ ./nix/uuav-test/nixos-module.nix ];
        services.uuav-test.package = nixpkgs.lib.mkDefault (harnessFor pkgs).runner;
      };

      devShells = forAllSystems (pkgs: {
        default = pkgs.mkShell {
          packages = [ pkgs.ffmpeg-full pkgs.jq pkgs.nginx pkgs.openssl pkgs.curl ];
          shellHook = ''
            echo "UUAV media test harness"
            echo "  nix run .#uuav-test                  serve everything and launch the scene"
            echo "  nix run .#uuav-test -- --check       serve, assert every case, exit"
            echo "  nix run .#uuav-test -- --trust       how to install the local CA"
          '';
        };
      });

      formatter = forAllSystems (pkgs: pkgs.nixpkgs-fmt);
    };
}
