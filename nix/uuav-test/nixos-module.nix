# NixOS service definition for the UUAV media sandbox harness.
#
# Deliberately thin: it runs the very same `uuav-test --serve-only` the CLI runs,
# rather than restating the nginx configuration in NixOS options. A second
# nginx description would drift from runner.sh, and then a case that passes on a
# developer's laptop would mean nothing about the box in CI.
#
# What the module owns is the part a NixOS host has to own: a state directory, a
# systemd unit, the firewall, the /etc/hosts entry that makes the certificate's
# DNS name resolve, and - the point of the exercise - the local CA in the system
# trust store via security.pki.certificateFiles.
#
# Usage:
#   imports = [ inputs.uuav-explorer.nixosModules.uuav-test ];
#   services.uuav-test = {
#     enable = true;
#     caFile = "/var/lib/uuav-test/pki/ca.pem";   # after the first run
#     openFirewall = true;
#   };
{ config, lib, pkgs, ... }:

let
  cfg = config.services.uuav-test;
  inherit (lib) mkEnableOption mkIf mkOption types;
in
{
  options.services.uuav-test = {
    enable = mkEnableOption "the UUAV media sandbox test harness";

    package = mkOption {
      type = types.package;
      description = "The uuav-test runner package.";
    };

    hostName = mkOption {
      type = types.str;
      default = "media.uuav.test";
      description = ''
        Host the media urls use. It must be a name in the harness certificate's
        SANs, and it must resolve on every machine running a client.
      '';
    };

    bind = mkOption {
      type = types.str;
      default = "0.0.0.0";
      description = "Address nginx binds. Keep 0.0.0.0 to serve a client on another machine.";
    };

    httpsPort = mkOption { type = types.port; default = 8443; };
    httpPort = mkOption { type = types.port; default = 8080; };
    stallPort = mkOption { type = types.port; default = 8444; };

    stateDir = mkOption {
      type = types.path;
      default = "/var/lib/uuav-test";
      description = "Holds the generated PKI, the staged media and the manifest.";
    };

    extraSubjectAltNames = mkOption {
      type = types.listOf types.str;
      default = [ ];
      example = [ "192.168.1.20" "workstation.lan" ];
      description = ''
        Extra names and addresses for the server certificate. A client on
        another machine dials one of these, so it has to be in the certificate.
      '';
    };

    caFile = mkOption {
      type = types.nullOr types.path;
      default = null;
      example = "/var/lib/uuav-test/pki/ca.pem";
      description = ''
        The harness CA, added to the system trust store. Necessarily a
        chicken-and-egg: the CA is minted on the first run, so leave this null
        for that run, then set it and rebuild. Nothing on this host trusts the
        harness until it is set.
      '';
    };

    openFirewall = mkOption {
      type = types.bool;
      default = false;
      description = "Open httpsPort and httpPort. The stall port stays loopback-only.";
    };
  };

  config = mkIf cfg.enable {
    assertions = [{
      assertion = cfg.caFile != null || cfg.hostName == "media.uuav.test";
      message = ''
        services.uuav-test: a custom hostName needs services.uuav-test.caFile set
        too, otherwise nothing on this host trusts the certificate that names it.
      '';
    }];

    security.pki.certificateFiles = lib.optional (cfg.caFile != null) cfg.caFile;

    networking.hosts = lib.mkIf (cfg.bind == "0.0.0.0" || cfg.bind == "127.0.0.1") {
      "127.0.0.1" = [ cfg.hostName ];
    };

    networking.firewall.allowedTCPPorts =
      lib.optionals cfg.openFirewall [ cfg.httpsPort cfg.httpPort ];

    systemd.services.uuav-test = {
      description = "UUAV media sandbox test harness";
      wantedBy = [ "multi-user.target" ];
      after = [ "network.target" ];

      environment = {
        UUAV_TEST_STATE = cfg.stateDir;
        UUAV_TEST_HOST = cfg.hostName;
        UUAV_TEST_BIND = cfg.bind;
        UUAV_TEST_HTTPS_PORT = toString cfg.httpsPort;
        UUAV_TEST_HTTP_PORT = toString cfg.httpPort;
        UUAV_TEST_STALL_PORT = toString cfg.stallPort;
        UUAV_TEST_EXTRA_SAN = lib.concatStringsSep "," cfg.extraSubjectAltNames;
        HOME = cfg.stateDir;
      };

      serviceConfig = {
        ExecStart = "${cfg.package}/bin/uuav-test --serve-only";
        Restart = "on-failure";
        RestartSec = 5;

        DynamicUser = true;
        StateDirectory = baseNameOf cfg.stateDir;
        WorkingDirectory = cfg.stateDir;

        NoNewPrivileges = true;
        PrivateTmp = true;
        PrivateDevices = true;
        ProtectSystem = "strict";
        ProtectHome = true;
        ProtectKernelTunables = true;
        ProtectKernelModules = true;
        ProtectControlGroups = true;
        RestrictAddressFamilies = [ "AF_INET" "AF_INET6" "AF_UNIX" ];
        RestrictNamespaces = true;
        SystemCallArchitectures = "native";
        # The harness serves untrusted-shaped content on purpose; it should never
        # be the thing that widens the host's own attack surface.
        CapabilityBoundingSet = [ "" ];
        LockPersonality = true;
        MemoryDenyWriteExecute = true;
      };
    };

    environment.systemPackages = [ cfg.package ];
  };
}
