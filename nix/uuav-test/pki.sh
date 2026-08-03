#!/usr/bin/env bash
# Local CA + server certificate for the UUAV media harness.
#
# Why a CA at all: retail builds allow only https, so a harness serving plain
# http exercises nothing about the retail protocol whitelist. And a bare
# self-signed leaf is worse than useless - SecureTransport (macOS) and Schannel
# (Windows) reject it, so every case fails for a reason that has nothing to do
# with the sandbox. The only configuration those two TLS stacks accept is a leaf
# chaining to a root installed in the OS trust store, which is exactly what this
# produces: one root the operator installs once, one leaf reissued freely.
#
# Two deliberate choices:
#   * openssl, not mkcert. mkcert automates the install but issues 825-day
#     leaves; Apple platforms reject server certificates valid for more than
#     398 days, so mkcert's leaves fail on the one platform that matters most
#     here. The leaf below is 397 days.
#   * SANs cover both a name and the loopback/LAN addresses. FFmpeg's
#     SecureTransport backend hands the url host to SSLSetPeerDomainName, whose
#     IP-address matching is unreliable, so the DNS name is the supported path
#     and the IP SANs are the convenience fallback for Linux/OpenSSL.
set -euo pipefail

PKI="${1:?usage: pki.sh PKI_DIR [extra-san,...]}"
EXTRA_SANS="${2:-}"
CANAME="UUAV media test harness local CA"

mkdir -p "$PKI"
chmod 700 "$PKI"

ca_valid() {
    [ -f "$PKI/ca.pem" ] && [ -f "$PKI/ca-key.pem" ] &&
        openssl x509 -in "$PKI/ca.pem" -noout -checkend 2592000 >/dev/null 2>&1
}

leaf_valid() {
    [ -f "$PKI/server.pem" ] && [ -f "$PKI/server-key.pem" ] &&
        openssl x509 -in "$PKI/server.pem" -noout -checkend 86400 >/dev/null 2>&1 &&
        [ -f "$PKI/server.sans" ] && [ "$(cat "$PKI/server.sans")" = "$SAN_LIST" ]
}

# Every address a client might dial. Extra names come from UUAV_TEST_EXTRA_SAN,
# which is how a Mac or Windows box on the LAN gets a matching certificate.
sans=(
    "DNS:media.uuav.test"
    "DNS:uuav-test.localhost"
    "DNS:localhost"
    "IP:127.0.0.1"
    "IP:::1"
)
if [ -n "$EXTRA_SANS" ]; then
    IFS=',' read -r -a extra <<<"$EXTRA_SANS"
    for e in "${extra[@]}"; do
        e="${e// /}"
        [ -n "$e" ] || continue
        if printf '%s' "$e" | grep -qE '^[0-9]+(\.[0-9]+){3}$|:'; then
            sans+=("IP:$e")
        else
            sans+=("DNS:$e")
        fi
    done
fi
SAN_LIST="$(
    IFS=,
    echo "${sans[*]}"
)"

if ! ca_valid; then
    echo "uuav-test: minting local CA in $PKI"
    rm -f "$PKI/server.pem" "$PKI/server-key.pem" "$PKI/server.sans"
    openssl req -x509 -newkey rsa:3072 -sha256 -days 3650 -nodes \
        -keyout "$PKI/ca-key.pem" -out "$PKI/ca.pem" \
        -subj "/CN=$CANAME/O=uuav-test" \
        -addext "basicConstraints=critical,CA:TRUE,pathlen:0" \
        -addext "keyUsage=critical,keyCertSign,cRLSign" 2>/dev/null
    # DER as well: Windows certutil and some MDM tooling want it.
    openssl x509 -in "$PKI/ca.pem" -outform DER -out "$PKI/ca.crt"
fi

if ! leaf_valid; then
    echo "uuav-test: issuing server certificate for $SAN_LIST"
    openssl req -newkey rsa:2048 -sha256 -nodes \
        -keyout "$PKI/server-key.pem" -out "$PKI/server.csr" \
        -subj "/CN=media.uuav.test/O=uuav-test" 2>/dev/null
    {
        echo "subjectAltName=$SAN_LIST"
        echo "basicConstraints=critical,CA:FALSE"
        echo "keyUsage=critical,digitalSignature,keyEncipherment"
        echo "extendedKeyUsage=serverAuth"
    } >"$PKI/server.ext"
    # 397 days: under Apple's 398-day ceiling for server certificates.
    openssl x509 -req -in "$PKI/server.csr" -CA "$PKI/ca.pem" -CAkey "$PKI/ca-key.pem" \
        -CAcreateserial -out "$PKI/server.pem" -days 397 -sha256 \
        -extfile "$PKI/server.ext" 2>/dev/null
    cat "$PKI/ca.pem" >>"$PKI/server.pem"
    printf '%s' "$SAN_LIST" >"$PKI/server.sans"
    rm -f "$PKI/server.csr" "$PKI/server.ext"
fi

chmod 600 "$PKI"/*-key.pem
