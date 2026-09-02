"""Print the PDB signature (GUID + age) each module in a minidump was built against.

Step 5 of the dmp-files runbook compares this against `llvm-pdbutil dump -summary <pdb>`.
Works on any OS -- no WinDbg required.

Usage: python3 pdbid.py <dump.dmp|dump.zip> [module-name-filter]
"""
import struct, sys, zipfile, io

def load(path):
    if path.endswith('.zip'):
        with zipfile.ZipFile(path) as zf:
            name = [n for n in zf.namelist() if n.endswith('.dmp')][0]
            return zf.read(name)
    return open(path, 'rb').read()

data = load(sys.argv[1])
want = sys.argv[2].lower() if len(sys.argv) > 2 else None

sig, ver, nstreams, dirrva = struct.unpack_from('<4sIII', data, 0)
assert sig == b'MDMP', 'not a minidump'

streams = {}
for i in range(nstreams):
    st, sz, rva = struct.unpack_from('<III', data, dirrva + i * 12)
    streams[st] = (sz, rva)

def mdstring(rva):
    ln = struct.unpack_from('<I', data, rva)[0]
    return data[rva + 4: rva + 4 + ln].decode('utf-16-le', 'replace')

MODULE_LIST = 4
sz, rva = streams[MODULE_LIST]
n = struct.unpack_from('<I', data, rva)[0]

print(f"{'module':28s} {'pdb signature (GUID)':40s} age  pdb")
print('-' * 110)

for i in range(n):
    o = rva + 4 + i * 108
    base, size = struct.unpack_from('<QI', data, o)
    namerva = struct.unpack_from('<I', data, o + 20)[0]
    # MINIDUMP_MODULE: BaseOfImage(8) SizeOfImage(4) CheckSum(4) TimeDateStamp(4)
    # ModuleNameRva(4) VS_FIXEDFILEINFO(52) -> CvRecord at 0x4C
    cv_size, cv_rva = struct.unpack_from('<II', data, o + 0x4C)

    name = mdstring(namerva).split('\\')[-1]
    if want and want not in name.lower():
        continue

    guid, age, pdb = '-', '-', '-'
    if cv_size >= 24 and cv_rva + cv_size <= len(data):
        cv = data[cv_rva: cv_rva + cv_size]
        if cv[:4] == b'RSDS':
            d1, d2, d3 = struct.unpack_from('<IHH', cv, 4)
            d4 = cv[12:20]
            guid = ('{%08X-%04X-%04X-%s-%s}' % (
                d1, d2, d3, d4[:2].hex().upper(), d4[2:].hex().upper()))
            age = struct.unpack_from('<I', cv, 20)[0]
            pdb = cv[24:].split(b'\x00')[0].decode('utf-8', 'replace').split('\\')[-1]

    print(f'{name[:28]:28s} {guid:40s} {str(age):4s} {pdb}')
