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
TEXT = [(sva, vsize, raw, rsize) for (n, sva, vsize, raw, rsize) in secs if n == ".text"]
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
md.detail = True

# Find every 'mov reg,[0x5449B0]' then within a short window a 'mov reg2,[reg+0xD8]',
# then see what offset is accessed off reg2. Also just tally: after loading
# [[0x5449B0]+0xD8] into a reg, what immediate offsets get dereferenced?
def load_5449B0_sites():
    needle = (0x005449B0).to_bytes(4, "little")
    sites = []
    for (sva, vsize, raw, rsize) in TEXT:
        seg = DATA[raw:raw + rsize]
        start = 0
        while True:
            i = seg.find(needle, start)
            if i < 0:
                break
            start = i + 1
            # candidate instruction starts 1 or 2 bytes before (A1 / 8B modrm)
            for back in (1, 2, 3):
                a = sva + i - back
                soff = va_to_off(a)
                ins = next(md.disasm(DATA[soff:soff + back + 4], a), None)
                if ins and ins.size == back + 4 and ins.address + ins.size == sva + i + 4:
                    if ins.mnemonic == "mov" and "0x5449b0" in ins.op_str and ins.op_str.split(",")[1].strip().startswith("dword"):
                        sites.append(ins)
                    break
    return sites

sites = load_5449B0_sites()
print("real 'mov reg,[0x5449B0]' loads:", len(sites))

# For each, disasm forward ~10 insns, track a register set to [base+0xD8], then
# report subsequent [that_reg + off] accesses.
from collections import Counter
off_after_d8 = Counter()
examples = []
for s in sites:
    dst = s.reg_name(s.operands[0].reg)  # reg loaded with the singleton ptr
    off = va_to_off(s.address)
    window = list(md.disasm(DATA[off:off + 64], s.address))
    d8reg = None
    for ins in window[1:]:
        # mov d8reg, [dst + 0xD8]
        if ins.mnemonic == "mov" and len(ins.operands) == 2:
            o0, o1 = ins.operands
            if (o1.type == capstone.x86.X86_OP_MEM and o1.mem.disp == 0xD8
                    and o1.mem.base != 0 and md.reg_name(o1.mem.base) == dst):
                d8reg = md.reg_name(o0.reg) if o0.type == capstone.x86.X86_OP_REG else None
                continue
            if d8reg and o1.type == capstone.x86.X86_OP_MEM and o1.mem.base != 0 \
                    and md.reg_name(o1.mem.base) == d8reg:
                off_after_d8[o1.mem.disp] += 1
                if len(examples) < 12:
                    examples.append("%08X %s %s" % (ins.address, ins.mnemonic, ins.op_str))
        # also fld/fcomp accessing d8reg
        if d8reg:
            for o in ins.operands:
                if o.type == capstone.x86.X86_OP_MEM and o.mem.base != 0 \
                        and md.reg_name(o.mem.base) == d8reg and ins.mnemonic != "mov":
                    off_after_d8[o.mem.disp] += 1

print("\noffsets accessed off [[0x5449B0]+0xD8]:")
for k, v in sorted(off_after_d8.items()):
    tag = ""
    if k == 0x54: tag = "  <-- DEPTH field!"
    if k == 0xB8: tag = "  <-- HULL HP field!"
    if k == 0x58: tag = "  <-- depth mirror/other"
    print("   +0x%-4X : %d%s" % (k, v, tag))

print("\nexamples:")
for e in examples:
    print("  ", e)
