"""Test accounts for a face-to-face run: generated wallets that own nothing, kept in one JSON
file outside the repository, and the two sign-ins built from them. Nothing here contacts a
server, and no key, identity or signature is printed: commands answer with labels and addresses.

  create <wallets.json> <label>
      Adds a generated account. The file holds {"accounts": [{label, address, privateKey}]}.
  unity-login <wallets.json> <label> [--slot 0] [--hours 168] [--prefs-dir DIR]
      With the Unity explorer closed, stores the account's identity in the client's prefs file
      (userdata_<slot>.json, key Web3Authentication.Identity), so the next launch starts signed
      in. An identity already stored there is saved first to <wallets.json>.unity-backup-<slot>.
  unity-restore <wallets.json> [--slot 0] [--prefs-dir DIR]
      Puts that saved identity back.
  web-login-line <wallets.json> <label> <out-file> --name N --look 1..160 --realm URL --position X,Y
      Writes the web client's console line `/hud_login <identity> ...` to <out-file>, for a
      driver to send. The file is a credential: delete it after sending.

<wallets.json> and <out-file> are refused when they lie inside this repository, where they could be
committed. The wallets file, the saved identity and the sign-in line are written for their owner
only (mode 0600).

Only the Windows prefs location was exercised; pass --prefs-dir elsewhere if the default is off."""
import argparse
import base64
import json
import os
import pathlib
import secrets
import subprocess
import sys
from datetime import datetime, timedelta, timezone

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import ethkeys  # noqa: E402

IDENTITY_KEY = 'Web3Authentication.Identity'
REPOSITORY = pathlib.Path(__file__).resolve().parents[4]


def outside_repository(value: str) -> pathlib.Path:
    path = pathlib.Path(value).resolve()
    if path.is_relative_to(REPOSITORY):
        raise argparse.ArgumentTypeError(f'{value} is inside the repository ({REPOSITORY}): keys and identities stay outside it')
    return path


def write_secret(path: pathlib.Path, text: str):
    """Writes a file that holds a key or an identity, for its owner only. On Windows the mode
    changes nothing: the file keeps the access list of its folder."""
    with open(os.open(path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600), 'w', encoding='utf-8') as file:
        file.write(text)
    os.chmod(path, 0o600)


def random_key() -> int:
    while True:
        key = int.from_bytes(secrets.token_bytes(32), 'big')
        if 0 < key < ethkeys.N:
            return key


def load(wallets: pathlib.Path) -> dict:
    return json.loads(wallets.read_text(encoding='utf-8')) if wallets.exists() else {'accounts': []}


def account_of(wallets: pathlib.Path, label: str) -> dict:
    for account in load(wallets)['accounts']:
        if account['label'] == label:
            return account
    sys.exit(f'no account {label!r} in {wallets}')


def build_identity(account: dict, hours: float) -> dict:
    """An AuthIdentity: a fresh ephemeral key the account delegates to by an EIP-191 signature."""
    ephemeral = random_key()
    public = ethkeys.public_key(ephemeral)
    address = ethkeys.address_of_public(public)
    expiration = (datetime.now(timezone.utc) + timedelta(hours=hours)).isoformat(timespec='milliseconds').replace('+00:00', 'Z')
    payload = f'Decentraland Login\nEphemeral address: {address}\nExpiration: {expiration}'
    signature = ethkeys.personal_sign(int(account['privateKey'], 16), payload)
    assert ethkeys.recover_personal_signer(payload, signature) == account['address']
    return {
        'ephemeralIdentity': {'address': address, 'publicKey': '0x' + public.hex(),
                              'privateKey': '0x' + ephemeral.to_bytes(32, 'big').hex()},
        'expiration': expiration,
        'authChain': [
            {'type': 'SIGNER', 'payload': account['address'], 'signature': ''},
            {'type': 'ECDSA_EPHEMERAL', 'payload': payload, 'signature': signature},
        ],
    }


def default_prefs_dir() -> pathlib.Path:
    if sys.platform == 'win32':
        return pathlib.Path(os.environ['USERPROFILE']) / 'AppData' / 'LocalLow' / 'Decentraland' / 'Explorer'
    if sys.platform == 'darwin':
        return pathlib.Path.home() / 'Library' / 'Application Support' / 'Decentraland' / 'Explorer'
    return pathlib.Path.home() / '.config' / 'unity3d' / 'Decentraland' / 'Explorer'


def refuse_while_unity_runs():
    if sys.platform == 'win32':
        listed = subprocess.run(['tasklist', '/FI', 'IMAGENAME eq Decentraland.exe', '/NH'],
                                capture_output=True, text=True).stdout
        running = 'Decentraland.exe' in listed
    else:
        running = subprocess.run(['pgrep', '-x', 'Decentraland'], capture_output=True).returncode == 0
    if running:
        sys.exit('the Unity explorer is running and would overwrite the prefs file: close it first')


def read_prefs(path: pathlib.Path) -> dict:
    return json.loads(path.read_text(encoding='utf-8-sig')) if path.exists() else {}


def write_prefs(path: pathlib.Path, prefs: dict):
    staging = path.with_name(path.name + '.new')
    write_secret(staging, json.dumps(prefs, separators=(',', ':')))
    os.replace(staging, path)


def backup_path(wallets: pathlib.Path, slot: int) -> pathlib.Path:
    return wallets.with_name(f'{wallets.name}.unity-backup-{slot}')


def create(args):
    data = load(args.wallets)
    if any(account['label'] == args.label for account in data['accounts']):
        sys.exit(f'{args.label!r} exists already')
    key = random_key()
    data['accounts'].append({'label': args.label, 'address': ethkeys.address_of(key),
                             'privateKey': '0x' + key.to_bytes(32, 'big').hex()})
    args.wallets.parent.mkdir(parents=True, exist_ok=True)
    write_secret(args.wallets, json.dumps(data, indent=1))
    print(f'{args.label} {data["accounts"][-1]["address"]}')


def unity_login(args):
    if args.hours <= 24:
        sys.exit('the client treats a stored identity with a day or less left as expired: use more than 24 hours')
    refuse_while_unity_runs()
    account = account_of(args.wallets, args.label)
    identity = build_identity(account, args.hours)
    stored = {
        'address': account['address'].lower(),
        'key': identity['ephemeralIdentity']['privateKey'],
        'expiration': identity['expiration'],
        'method': 'ANY',
        'ephemeralAuthChain': [{**link, 'payload': link['payload'].lower()} if link['type'] == 'SIGNER' else link
                               for link in identity['authChain']],
    }
    path = (args.prefs_dir or default_prefs_dir()) / f'userdata_{args.slot}.json'
    prefs = read_prefs(path)
    strings = prefs.setdefault('Strings', {})
    backup = backup_path(args.wallets, args.slot)
    note = 'no identity was stored'
    if IDENTITY_KEY in strings:
        note = 'replaced an identity'
        if not backup.exists():
            write_secret(backup, strings[IDENTITY_KEY])
            note = f'the identity stored before is saved in {backup.name}'
    strings[IDENTITY_KEY] = json.dumps(stored, separators=(',', ':'))
    path.parent.mkdir(parents=True, exist_ok=True)
    write_prefs(path, prefs)
    print(f'{path.name}: {args.label} {account["address"]} until {identity["expiration"]} ({note})')


def unity_restore(args):
    refuse_while_unity_runs()
    backup = backup_path(args.wallets, args.slot)
    if not backup.exists():
        sys.exit(f'no saved identity: {backup.name} is missing')
    path = (args.prefs_dir or default_prefs_dir()) / f'userdata_{args.slot}.json'
    prefs = read_prefs(path)
    prefs.setdefault('Strings', {})[IDENTITY_KEY] = backup.read_text(encoding='utf-8')
    write_prefs(path, prefs)
    backup.unlink()
    print(f'{path.name}: the saved identity is back')


def web_login_line(args):
    identity = build_identity(account_of(args.wallets, args.label), args.hours)
    encoded = base64.b64encode(json.dumps(identity, separators=(',', ':')).encode()).decode()
    write_secret(args.out, f'/hud_login {encoded} --name {args.name} --look {args.look} --realm {args.realm} '
                           f'--position {args.position}\n')
    print(f'{args.out.name}: the sign-in line of {args.label} {account_of(args.wallets, args.label)["address"]}')


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    commands = parser.add_subparsers(dest='command', required=True)

    command = commands.add_parser('create')
    command.add_argument('wallets', type=outside_repository)
    command.add_argument('label')
    command.set_defaults(run=create)

    command = commands.add_parser('unity-login')
    command.add_argument('wallets', type=outside_repository)
    command.add_argument('label')
    command.add_argument('--slot', type=int, default=0)
    command.add_argument('--hours', type=float, default=168)
    command.add_argument('--prefs-dir', type=pathlib.Path)
    command.set_defaults(run=unity_login)

    command = commands.add_parser('unity-restore')
    command.add_argument('wallets', type=outside_repository)
    command.add_argument('--slot', type=int, default=0)
    command.add_argument('--prefs-dir', type=pathlib.Path)
    command.set_defaults(run=unity_restore)

    command = commands.add_parser('web-login-line')
    command.add_argument('wallets', type=outside_repository)
    command.add_argument('label')
    command.add_argument('out', type=outside_repository)
    command.add_argument('--name', required=True)
    command.add_argument('--look', type=int, required=True)
    command.add_argument('--realm', required=True)
    command.add_argument('--position', required=True)
    command.add_argument('--hours', type=float, default=24)
    command.set_defaults(run=web_login_line)

    args = parser.parse_args()
    args.run(args)


if __name__ == '__main__':
    main()
