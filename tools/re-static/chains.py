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
TEXT = [(sva, vsize, raw, rsize) for (n, sva, vsize, raw, rsize) in secs
        if n == ".text"]
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
md.detail = True

def real_mem_refs(target_va):
    """Instructions that actually reference [target_va] as a memory operand."""
    out = []
    for (sva, vsize, raw, rsize) in TEXT:
        seg = DATA[raw:raw + rsize]
        needle = target_va.to_bytes(4, "little")
        start = 0
        while True:
            i = seg.find(needle, start)
            if i < 0:
                break
            start = i + 1
            # decode a few instructions ending near here
            for back in range(1, 9):
                a = sva + i - back
                soff = va_to_off(a)
                if soff is None:
                    continue
                ins = next(md.disasm(DATA[soff:soff + back + 6], a), None)
                if not ins or ins.size != back + 4:
                    continue
                if ins.address + ins.size != sva + i + 4:
                    continue
                # does it have a memory operand with disp == target?
                hit = False
                for op in ins.operands:
                    if op.type == capstone.x86.X86_OP_MEM:
                        if (op.mem.disp & 0xFFFFFFFF) == target_va and op.mem.base == 0 and op.mem.index == 0:
                            hit = True
                if hit:
                    out.append((ins.address, ins.bytes.hex(), ins.mnemonic, ins.op_str))
                break
    return out

def func_start(va):
    """Scan back to previous ret/int3 boundary; next insn is func start."""
    a = va
    # walk back byte by byte until we find a 0xC3/0xC2/0xCC then align forward
    off = va_to_off(va)
    # find previous 0xCC padding or ret
    p = off
    while p > 0:
        p -= 1
        if DATA[p] in (0xCC,):
            # function likely starts right after run of 0xCC
            start_off = p + 1
            while DATA[start_off] == 0xCC:
                start_off += 1
            # convert
            for (n, sva, vsize, raw, rsize) in secs:
                if raw <= start_off < raw + rsize:
                    return sva + (start_off - raw)
    return None

def callers(func_va):
    """Find E8 rel32 call sites targeting func_va."""
    out = []
    for (sva, vsize, raw, rsize) in TEXT:
        seg = DATA[raw:raw + rsize]
        for i in range(len(seg) - 5):
            if seg[i] == 0xE8:
                rel = int.from_bytes(seg[i+1:i+5], "little", signed=True)
                tgt = (sva + i + 5 + rel) & 0xFFFFFFFF
                if tgt == func_va:
                    out.append(sva + i)
    return out

print("=== HP mirror 0x566A28 real refs ===")
for r in real_mem_refs(0x00566A28):
    print("  %08X  %-22s %s %s" % (r[0], r[1], r[2], r[3]))

print("\n=== getter func start & callers (getter @004E1C99) ===")
fs = func_start(0x004E1C99)
print("  func start:", "%08X" % fs if fs else None)
if fs:
    cs = callers(fs)
    print("  %d callers:" % len(cs), ["%08X" % c for c in cs])

print("\n=== depth setter func starts & callers ===")
for site in (0x00513FE9, 0x00514150, 0x00514164):
    fs = func_start(site)
    print("  site %08X -> func start %s" % (site, "%08X" % fs if fs else None))
    if fs:
        cs = callers(fs)
        print("     %d callers:" % len(cs), ["%08X" % c for c in cs][:20])

print("\n=== disasm the depth-object resolver @00514180 region ===")
off = va_to_off(0x00514180)
for ins in md.disasm(DATA[off:off + 0x70], 0x00514180):
    print("  %08X  %-22s %s %s" % (ins.address, ins.bytes.hex(), ins.mnemonic, ins.op_str))
