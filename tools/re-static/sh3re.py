import pefile, capstone

EXE = r"C:\games\OneAlex Extended 1.5.2_pre-release\sh3.exe"
pe = pefile.PE(EXE, fast_load=True)
IB = pe.OPTIONAL_HEADER.ImageBase
with open(EXE, "rb") as f:
    DATA = f.read()

secs = []
for s in pe.sections:
    name = s.Name.rstrip(b"\x00").decode("latin1")
    secs.append((name, IB + s.VirtualAddress, s.Misc_VirtualSize,
                 s.PointerToRawData, s.SizeOfRawData))

def va_to_off(va):
    for name, sva, vsize, raw, rsize in secs:
        if sva <= va < sva + max(vsize, rsize):
            return raw + (va - sva)
    return None

# primary code section
TEXT = [(sva, vsize, raw, rsize) for (n, sva, vsize, raw, rsize) in secs
        if n == ".text"]
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
md.detail = True


def find_sig(pattern_bytes, mask=None):
    hits = []
    for (sva, vsize, raw, rsize) in TEXT:
        seg = DATA[raw:raw + rsize]
        n = len(pattern_bytes)
        if mask is None:
            start = 0
            while True:
                i = seg.find(pattern_bytes, start)
                if i < 0:
                    break
                hits.append(sva + i)
                start = i + 1
        else:
            for i in range(0, len(seg) - n + 1):
                ok = True
                for j in range(n):
                    if mask[j] and seg[i + j] != pattern_bytes[j]:
                        ok = False
                        break
                if ok:
                    hits.append(sva + i)
    return hits


def sig_str(pattern_bytes, mask):
    return " ".join(("%02X" % b) if m else "??"
                     for b, m in zip(pattern_bytes, mask))


print("=== AOB signatures + uniqueness ===")

off = va_to_off(0x004E1C99)
b = DATA[off:off + 11]
mask = [True] * 11
print("\n#1 hull getter @004E1C99 (11 bytes, literal):")
print("  bytes:", b.hex())
print("  sig  :", sig_str(b, mask))
print("  hits :", ["%08X" % h for h in find_sig(b, mask)])

off = va_to_off(0x00513FFD)
b = DATA[off:off + (0x51400F - 0x513FFD)]
mask = [True] * len(b)
for k in range(len(b) - 4, len(b)):
    mask[k] = False
print("\n#2 tight depth-compare sig @00513FFD (jne rel32 wildcarded):")
print("  sig  :", sig_str(b, mask))
print("  hits :", ["%08X" % h for h in find_sig(b, mask)])

b = bytes.fromhex("894154")
print("\n#2b bare 'mov [ecx+0x54],eax' (89 41 54) occurrences:",
      len(find_sig(b, [True] * 3)))

# ---- xref discovery: who reads static globals used as chain roots ----
def find_abs_refs(target_va):
    """Find instructions whose 4-byte imm/disp equals target_va (little-endian)."""
    needle = target_va.to_bytes(4, "little")
    hits = []
    for (sva, vsize, raw, rsize) in TEXT:
        seg = DATA[raw:raw + rsize]
        start = 0
        while True:
            i = seg.find(needle, start)
            if i < 0:
                break
            hits.append(sva + i)  # VA of the imm bytes (instruction starts a bit before)
            start = i + 1
    return hits


for g in (0x00566A28, 0x005449B0, 0x0055408C):
    refs = find_abs_refs(g)
    print("\n=== refs to static 0x%08X (%d) ===" % (g, len(refs)))
    for r in refs:
        # disassemble the instruction that ends with this imm: back up to find start
        for back in range(1, 8):
            soff = va_to_off(r - back)
            code = DATA[soff:soff + back + 6]
            ins = next(md.disasm(code, r - back), None)
            if ins and ins.address + ins.size >= r + 4 and ins.address <= r:
                print("   %08X  %-22s %s %s" %
                      (ins.address, ins.bytes.hex(), ins.mnemonic, ins.op_str))
                break
