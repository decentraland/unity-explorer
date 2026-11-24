"""Ethereum keys with no third-party packages: Keccak-256, secp256k1 addresses, EIP-191
personal_sign (RFC 6979, low-s, v = 27/28) and recovery. `python -I ethkeys.py` checks the vectors."""
import hashlib
import hmac

_RC = [0x0000000000000001, 0x0000000000008082, 0x800000000000808A, 0x8000000080008000,
       0x000000000000808B, 0x0000000080000001, 0x8000000080008081, 0x8000000000008009,
       0x000000000000008A, 0x0000000000000088, 0x0000000080008009, 0x000000008000000A,
       0x000000008000808B, 0x800000000000008B, 0x8000000000008089, 0x8000000000008003,
       0x8000000000008002, 0x8000000000000080, 0x000000000000800A, 0x800000008000000A,
       0x8000000080008081, 0x8000000000008080, 0x0000000080000001, 0x8000000080008008]
_ROT = [[0, 36, 3, 41, 18], [1, 44, 10, 45, 2], [62, 6, 43, 15, 61], [28, 55, 25, 21, 56], [27, 20, 39, 8, 14]]
_M = (1 << 64) - 1


def _rol(v, n):
    return ((v << n) | (v >> (64 - n))) & _M if n else v


def _f(a):
    for rc in _RC:
        c = [a[x][0] ^ a[x][1] ^ a[x][2] ^ a[x][3] ^ a[x][4] for x in range(5)]
        d = [c[(x - 1) % 5] ^ _rol(c[(x + 1) % 5], 1) for x in range(5)]
        a = [[a[x][y] ^ d[x] for y in range(5)] for x in range(5)]
        b = [[0] * 5 for _ in range(5)]
        for x in range(5):
            for y in range(5):
                b[y][(2 * x + 3 * y) % 5] = _rol(a[x][y], _ROT[x][y])
        a = [[b[x][y] ^ ((~b[(x + 1) % 5][y]) & b[(x + 2) % 5][y]) for y in range(5)] for x in range(5)]
        a[0][0] ^= rc
    return a


def keccak256(data: bytes) -> bytes:
    rate = 136
    msg = bytearray(data) + b'\x01'
    msg += b'\x00' * ((-len(msg)) % rate)
    msg[-1] |= 0x80
    a = [[0] * 5 for _ in range(5)]
    for off in range(0, len(msg), rate):
        block = msg[off:off + rate]
        for i in range(rate // 8):
            a[i % 5][i // 5] ^= int.from_bytes(block[8 * i:8 * i + 8], 'little')
        a = _f(a)
    return b''.join(a[i % 5][i // 5].to_bytes(8, 'little') for i in range(4))


P = 2 ** 256 - 2 ** 32 - 977
N = 0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141
G = (0x79BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798,
     0x483ADA7726A3C4655DA4FBFC0E1108A8FD17B448A68554199C47D08FFB10D4B8)


def _add(p, q):
    if p is None:
        return q
    if q is None:
        return p
    if p[0] == q[0] and (p[1] + q[1]) % P == 0:
        return None
    if p == q:
        lam = 3 * p[0] * p[0] * pow(2 * p[1], -1, P) % P
    else:
        lam = (q[1] - p[1]) * pow(q[0] - p[0], -1, P) % P
    x = (lam * lam - p[0] - q[0]) % P
    return x, (lam * (p[0] - x) - p[1]) % P


def _mul(k, point=G):
    result = None
    while k:
        if k & 1:
            result = _add(result, point)
        point = _add(point, point)
        k >>= 1
    return result


def public_key(private_key: int) -> bytes:
    x, y = _mul(private_key)
    return b'\x04' + x.to_bytes(32, 'big') + y.to_bytes(32, 'big')


def _checksum(raw_hex: str) -> str:
    digest = keccak256(raw_hex.encode()).hex()
    return '0x' + ''.join(c.upper() if int(digest[i], 16) >= 8 else c for i, c in enumerate(raw_hex))


def address_of_public(public: bytes) -> str:
    return _checksum(keccak256(public[1:])[-20:].hex())


def address_of(private_key: int) -> str:
    return address_of_public(public_key(private_key))


def _rfc6979_k(private_key: int, digest: bytes) -> int:
    x = private_key.to_bytes(32, 'big')
    h = (int.from_bytes(digest, 'big') % N).to_bytes(32, 'big')
    v, k = b'\x01' * 32, b'\x00' * 32
    k = hmac.new(k, v + b'\x00' + x + h, hashlib.sha256).digest()
    v = hmac.new(k, v, hashlib.sha256).digest()
    k = hmac.new(k, v + b'\x01' + x + h, hashlib.sha256).digest()
    v = hmac.new(k, v, hashlib.sha256).digest()
    while True:
        v = hmac.new(k, v, hashlib.sha256).digest()
        candidate = int.from_bytes(v, 'big')
        if 0 < candidate < N:
            return candidate
        k = hmac.new(k, v + b'\x00', hashlib.sha256).digest()
        v = hmac.new(k, v, hashlib.sha256).digest()


def _eip191(message: str) -> bytes:
    body = message.encode('utf-8')
    return keccak256(b'\x19Ethereum Signed Message:\n' + str(len(body)).encode() + body)


def personal_sign(private_key: int, message: str) -> str:
    digest = _eip191(message)
    z = int.from_bytes(digest, 'big')
    k = _rfc6979_k(private_key, digest)
    rx, ry = _mul(k)
    r = rx % N
    s = pow(k, -1, N) * (z + r * private_key) % N
    recovery = (ry & 1) | (2 if rx >= N else 0)
    if s > N // 2:
        s, recovery = N - s, recovery ^ 1
    return '0x' + r.to_bytes(32, 'big').hex() + s.to_bytes(32, 'big').hex() + format(27 + recovery, '02x')


def recover_personal_signer(message: str, signature: str) -> str:
    raw = bytes.fromhex(signature[2:] if signature.startswith('0x') else signature)
    r, s, v = int.from_bytes(raw[:32], 'big'), int.from_bytes(raw[32:64], 'big'), raw[64] - 27
    x = r + (N if v & 2 else 0)
    y_sq = (pow(x, 3, P) + 7) % P
    y = pow(y_sq, (P + 1) // 4, P)
    if (y & 1) != (v & 1):
        y = P - y
    z = int.from_bytes(_eip191(message), 'big')
    r_inv = pow(r, -1, N)
    q = _add(_mul(s * r_inv % N, (x, y)), _mul((-z * r_inv) % N))
    return address_of_public(b'\x04' + q[0].to_bytes(32, 'big') + q[1].to_bytes(32, 'big'))


if __name__ == '__main__':
    assert keccak256(b'').hex() == 'c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470'
    assert address_of(1) == '0x7E5F4552091A69125d5DfCb7b8C2659029395Bdf'
    # web3.js docs' published vector for eth.accounts.sign('Some data', key)
    key = 0x4c0883a69102937d6231471b5dbb6204fe5129617082792ae468d01a3f362318
    expected = ('0xb91467e570a6466aa9e9876cbcd013baba02900b8979d43fe208a4a4f339f5fd'
                '6007e74cd82e037b800186422fc2da167c747ef045e5d18a5f5d4300f8e1a0291c')
    assert personal_sign(key, 'Some data') == expected, personal_sign(key, 'Some data')
    assert recover_personal_signer('Some data', expected) == address_of(key)
    print('vectors ok')
