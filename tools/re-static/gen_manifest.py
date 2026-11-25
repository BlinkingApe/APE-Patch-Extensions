#!/usr/bin/env python3
"""Generate APE's patch manifest (umbrella ticket 08's schema).

The manifest is the authoring-time record of every site APE writes: where it is,
what is invariant about it, what its complete original byte run / ini line is, and
how its value is derived from the GUI's fields.  It is checked into git as the
reviewable source of truth and embedded into the patcher exe as a resource.

Two modes:

    python gen_manifest.py                 write data/manifest/ape-manifest.json
    python gen_manifest.py --check         fail if the checked-in file has drifted
    python gen_manifest.py --ape-act PATH  measure ape.act offsets from PATH

`--check` is the build-time stale-manifest guard from ticket 08.  The C# build
runs it before compiling and fails on any difference.

Byte runs below were authored from docs/re-notes/{03,04}-*.md and then read back
out of real binaries.  Whenever a reference binary is present under
data/exe_with_hsie_patches/ this script re-reads every run from it and fails on a
mismatch, so the literals here cannot silently rot.
"""

import argparse
import hashlib
import json
import os
import struct
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "data", "manifest", "ape-manifest.json")
REF_DIR = os.path.join(ROOT, "data", "exe_with_hsie_patches")

# The ape.act the patcher embeds as its template (ADR-0006): the release build.
# Its struct offsets are measured from this file, so the patcher must embed
# exactly this build. A Release build re-runs this script on itself
# (CMakeLists.txt), so the manifest follows the binary.
APE_ACT_TEMPLATE = "build/x86-release/ape.act"

# Depth stress's constants struct (src/mods/depth_stress/constants.hpp). The
# layout here is the contract with that header: change the two together, and
# bump the version.
DEPTH_STRESS_MAGIC = b"APEDPTH1"
DEPTH_STRESS_VERSION = 2  # 2: motor flooding damage replaced wear (ticket 31)
DEPTH_STRESS_FIELDS = [
    # (name, offset in the struct, what the patcher writes there)
    ("tier2_depth_m", 12, "metres"),
    ("flood_threshold", 16, "flooding threshold % / 100"),
    ("motor_damage_per_s", 20, "motor damage %/game-s / 100"),
    ("t1_seep_per_s", 24, "leakage %/min / 6000"),
    ("t1_hull_mult", 28, "multiplier"),
    ("t2_seep_per_s", 32, "leakage %/min / 6000"),
    ("t2_hull_mult", 36, "multiplier"),
]
DEPTH_STRESS_SIZE = 40

# h.sie appends .pcode at VA 0x633000 / raw 0x145000 and .pconst at VA 0x63C000 /
# raw 0x14E000 -- the same delta for both (docs/re-notes/03, sec.1).
VA_TO_FILE = 0x4EE000


def off(va):
    return va - VA_TO_FILE


# Ticket 13: APE no longer identifies a specific sh3.exe build. Every site's
# offset below was proven against OAE 1.5.2 (h.sie v 1.6E1) and re-confirmed
# byte-identical in KC 2.5 (h.sie v 1.6B1) -- docs/re-notes/05-kc25-admission-
# evidence.md -- so there is one offset per site, not one per build. APE reads
# and displays whatever h.sie version stamp it finds, but never matches it
# against a list: h.sie's own installer already refused to run against an
# sh3.exe it does not support, so APE inherits that gate instead of
# duplicating it. A new h.sie version that relocates a site is still caught,
# just later -- Verify()'s per-site context-byte check refuses the write, not
# a whole-file identity check refuses the open.

# hsie.act rewrites these 24 .pconst addresses from hsie.ini at load, so a static
# byte written to any of them is clobbered before the game reads it (ticket 09).
# Recorded so the generator can refuse to emit an sh3.exe site inside the region.
CLOBBERED = [
    0x63C000, 0x63C084, 0x63C088, 0x63C08C, 0x63C090, 0x63C094, 0x63C098,
    0x63C0A4, 0x63C0A8, 0x63C0AC, 0x63C0B0, 0x63C0B4, 0x63C0B8, 0x63C0BC,
    0x63C0C0, 0x63C0C4, 0x63C0C8, 0x63C0CC, 0x63C0D0, 0x63C0D4, 0x63C0D8,
    0x63C0DC, 0x63C0E4, 0x63C0E8,
]

# APE's replacement for h.sie's time-compression helper: p*TC, clamped to 1.0.
# 21 bytes into the original's 44-byte body, then ticket 14's reverse-telegraph
# trampoline (23 bytes, see below) fills the padding out to 0x635844 -- there
# is no int3 padding left once both live here.
# (docs/re-notes/04-diesel-damage-signature.md sec.3b)
HELPER_APE = (
    "558bec"      # push ebp; mov ebp, esp
    "d94508"      # fld  dword [ebp+8]        ; p
    "d84d0c"      # fmul dword [ebp+0xc]      ; p * TC
    "d9e8"        # fld1
    "dbf1"        # fcomi   st(1)
    "dbc1"        # fcmovnb st(0), st(1)      ; st0 := min(1.0, p*TC)
    "ddd9"        # fstp    st(1)
    "c9"          # leave
    "c20800"      # ret 8
) + (
    # Ticket 14's reverse-telegraph skip trampoline, landing at 0x63582D
    # (docs/re-notes/04-diesel-damage-signature.md sec.6). Entered via a jmp
    # spliced into the pow()-arg push at 0x635111 (site
    # reverse-telegraph-skip-jmp, below). eax is dead here -- both exit paths
    # (the pow() call at 0x635117 and the cave's shared "no damage this tick"
    # exit at 0x635204) immediately overwrite it from a fresh call return, and
    # nothing between here and there reads it.
    "a1d4e16300"    # mov  eax, [0x63E1D4]    ; the telegraph value, as bits
    "85c0"          # test eax, eax           ; SF := its sign bit
    "7806"          # js   +6                 ; negative (reverse) -> skip
    "50"            # push eax                ; positive/zero: same push the
                     #                        ; removed instruction would
                     #                        ; have done
    "e9dbf8ffff"    # jmp  0x635117           ; resume at the pow() call
    "58"            # pop  eax                ; [skip] discard the exponent
                     #                        ; already pushed at 0x63510A,
                     #                        ; which the aborted call would
                     #                        ; otherwise have consumed
    "e9c2f9ffff"    # jmp  0x635204           ; the cave's shared no-damage
                     #                        ; exit
    + "cc" * 2      # unreached padding, out to 0x635844
)


# Ticket 15's pow() resolver, in the empty .pcode tail at 0x63B000. h.sie's
# thunk at 0x6371A8 reached pow as [atol] - 0xDD50, which only lands on pow in
# Microsoft's msvcr71 7.10.3052.4; under Wine's builtin msvcr71 it jumps into
# arbitrary code. This asks the loader instead, through two KERNEL32 imports
# sh3.exe already has. Both calls are stdcall, so the stack is back to the
# thunk's two marshalled doubles on ret; only eax/ecx/edx are clobbered, as
# the CRT call that follows clobbers them anyway. Stateless on purpose -- .pcode
# is not writable, and two lookups per diesel tick (~1/real s) cost nothing.
# (docs/re-notes/04-diesel-damage-signature.md sec.3c)
POW_RESOLVER = (
    "682cb06300"    # push 0x63B02C           ; "pow"
    "6820b06300"    # push 0x63B020           ; "MSVCR71.dll"
    "ff1548d25100"  # call [0x51D248]         ; KERNEL32!GetModuleHandleA
    "50"            # push eax                ; hModule
    "ff15dcd15100"  # call [0x51D1DC]         ; KERNEL32!GetProcAddress
                     #                        ; -> eax = msvcr71!pow
    "c3"            # ret
    + "cc" * 8      # padding, out to 0x63B020
    + "MSVCR71.dll\0".encode("ascii").hex()   # 0x63B020
    + "pow\0".encode("ascii").hex()           # 0x63B02C
)
# The cave site runs 16 bytes past the resolver's end (0x63B030): they stay
# zero in both runs, so they are the only context Verify can check. That
# refuses a cave something else has since filled past 0x63B030, but not one
# whose foreign bytes all fit inside the resolver's own 48 -- the same blind
# spot every no-context fixed-rewrite site has.
POW_CAVE_TAIL_LEN = 16

# h.sie's pow() thunk at 0x6371A8, either side of the 10 bytes APE rewrites:
# marshal the two float args to doubles on the stack, and call eax / return.
POW_THUNK_HEAD = "558bec83ec10d9450cdd5c2408d94508dd1c24"
POW_THUNK_TAIL = "ffd083c410c9c20800"


def exe_site(sid, va, original, context, field=None, source=None,
             replacement=None, encoding=None, vmin=None, vmax=None, default=None,
             note=None):
    site = {
        "id": sid,
        "store": "sh3.exe",
        "encoding": encoding,
        "va": "0x%06X" % va,
        "offset": off(va),
        "original": original,
        "context": [[at, b] for at, b in context],
    }
    if field is not None:
        site["field"] = field
    if replacement is not None:
        site["replacement"] = replacement
    if source is not None:
        site["source"] = source
    if vmin is not None:
        site["min"] = vmin
        site["max"] = vmax
        site["default"] = default
    if note:
        site["note"] = note
    return site


def ini_site(sid, key, original, ape, note=None):
    site = {
        "id": sid,
        "store": "hsie.ini",
        "encoding": "ini-value",
        "key": key,
        "original": original,
        "ape": ape,
    }
    if note:
        site["note"] = note
    return site


def menu_site(sid, key, original, ape, note=None):
    site = {
        "id": sid,
        "store": "en_menu.txt",
        "encoding": "ini-value",
        "key": key,
        "original": original,
        "ape": ape,
    }
    if note:
        site["note"] = note
    return site


def pe32_sections(blob):
    """(name, raw offset, raw size) per section of a PE32/x86 image.

    Raises ValueError if the file isn't one: ape.act must be PE32 (x86), since
    OAE's sh3.exe is 32-bit.
    """
    if blob[:2] != b"MZ":
        raise ValueError("not a PE file (no MZ header)")
    pe = struct.unpack_from("<I", blob, 0x3C)[0]
    if blob[pe:pe + 4] != b"PE\0\0":
        raise ValueError("not a PE file (no PE signature)")
    machine, nsec = struct.unpack_from("<HH", blob, pe + 4)
    opt_size = struct.unpack_from("<H", blob, pe + 20)[0]
    magic = struct.unpack_from("<H", blob, pe + 24)[0]
    if machine != 0x14C or magic != 0x10B:
        raise ValueError("not PE32/x86 (machine 0x%X, optional header 0x%X)"
                         % (machine, magic))
    sections = []
    at = pe + 24 + opt_size
    for i in range(nsec):
        name = blob[at:at + 8].rstrip(b"\0").decode("ascii", "replace")
        raw_size, raw_ptr = struct.unpack_from("<II", blob, at + 16)
        sections.append((name, raw_ptr, raw_size))
        at += 40
    return sections


def locate_depth_stress(blob):
    """File offset of the depth-stress constants struct in a built ape.act.

    The marker and version together must occur exactly once, inside .data:
    the bare marker also appears in .rdata, where verify()'s compare keeps
    its copy, so the marker alone is not enough.
    """
    sections = pe32_sections(blob)
    head = DEPTH_STRESS_MAGIC + struct.pack("<I", DEPTH_STRESS_VERSION)
    hits = []
    at = blob.find(head)
    while at != -1:
        hits.append(at)
        at = blob.find(head, at + 1)
    if len(hits) != 1:
        raise ValueError("depth-stress marker+version found %d times, want 1"
                         % len(hits))
    at = hits[0]
    for name, ptr, size in sections:
        if name == ".data" and ptr <= at and at + DEPTH_STRESS_SIZE <= ptr + size:
            return at
    raise ValueError("depth-stress struct at 0x%X is not inside .data" % at)


def ape_act_block(path, previous):
    """The manifest's ape.act store: where each constants struct sits.

    With the template built, the offset is measured from it. Without it, only
    the previously recorded offset is carried over (with a warning), so the
    sh3.exe half of the manifest can still be regenerated and checked; the
    rest of the block is still rebuilt from this file, so a layout change here
    still shows up as drift.
    """
    if os.path.exists(path):
        offset = locate_depth_stress(open(path, "rb").read())
    elif previous is not None:
        print("manifest: %s not built; reusing the recorded ape.act offset, "
              "unmeasured" % path, file=sys.stderr)
        offset = previous["structs"][0]["offset"]
    else:
        raise ValueError("%s not built and no ape.act offsets recorded; build "
                         "the x86-release preset first" % path)
    return {
        "store": "ape.act",
        "template": APE_ACT_TEMPLATE,
        "structs": [{
            "id": "depth-stress",
            "offset": offset,
            "size": DEPTH_STRESS_SIZE,
            "magic": DEPTH_STRESS_MAGIC.hex(),
            "version": DEPTH_STRESS_VERSION,
            "fields": [{"id": n, "at": a, "type": "f32", "note": note}
                       for n, a, note in DEPTH_STRESS_FIELDS],
        }],
    }


def build(ape_act):
    hull = {
        "id": "hull-repair",
        "name": "Hull repair at sea",
        # No h.sie flag dependency: U-Tanker (flag 13) is on in every surveyed
        # install and ticket 07 ships hull repair ungated.
        "flagIndex": None,
        "sites": [
            exe_site(
                "hull-roll-width", 0x63364B,
                original="6810000000",              # push 0x10   -> rand(0..15)
                context=[(0, "68")],
                field={"at": 1, "length": 4, "type": "u32"},
                encoding="operand-immediate",
                source="hull_width",
                vmin=1, vmax=101, default=16,
                note="roll width; max - min + 1",
            ),
            exe_site(
                "hull-roll-floor", 0x633655,
                original="83c005",                  # add eax, 5  -> 5..20
                context=[(0, "83c0")],
                field={"at": 2, "length": 1, "type": "u8"},
                encoding="byte",
                source="hull_floor",
                vmin=0, vmax=100, default=5,
                note="roll floor; the minimum percentage",
            ),
        ],
    }

    diesel = {
        "id": "diesel-damage",
        "name": "Diesel damage",
        # Flag table entry #20, "Diesel Damage Fix".  APE reads it and never
        # writes it (ticket 05, decision A-2).
        "flagIndex": 20,
        "sites": [
            # -- hsie.ini: the rate and the flattened model (ticket 04) --------
            ini_site("val9-p-dso", "Val9", "Val9=2e-4",
                     {"kind": "derived", "source": "p_dso"},
                     note="base probability per game-second"),
            ini_site("val10-threshold", "Val10", "Val10=1e-6",
                     {"kind": "derived", "source": "threshold"},
                     note="derived idle cutoff, p_dso * 0.977237; never user-facing"),
            ini_site("val11-factor-0", "Val11", "Val11=1.0",
                     {"kind": "literal", "value": "1.0"}),
            ini_site("val12-factor-1", "Val12", "Val12=4.0",
                     {"kind": "literal", "value": "1.0"}),
            ini_site("val13-factor-2", "Val13", "Val13=8.0\t",
                     {"kind": "literal", "value": "1.0"}),
            ini_site("val14-exponent-0", "Val14", "Val14=200.0\t",
                     {"kind": "literal", "value": "0.01"}),
            ini_site("val15-exponent-1", "Val15", "Val15=20.0\t",
                     {"kind": "literal", "value": "0.01"}),
            ini_site("val16-exponent-2", "Val16", "Val16=10.0",
                     {"kind": "literal", "value": "0.01"}),
            # -- sh3.exe .pcode: the damage band ------------------------------
            exe_site(
                "band-first-hi", 0x6351B0,
                original="683d0a173f",              # push 0.59
                context=[(0, "68")],
                field={"at": 1, "length": 4, "type": "f32"},
                encoding="operand-immediate", source="band_hi",
                vmin=0.01, vmax=0.99, default=0.55,
            ),
            exe_site(
                "band-first-lo", 0x6351B5,
                original="680ad7233c",              # push 0.01
                context=[(0, "68")],
                field={"at": 1, "length": 4, "type": "f32"},
                encoding="operand-immediate", source="band_lo",
                vmin=0.01, vmax=0.99, default=0.15,
            ),
            exe_site(
                "band-escalated-hi", 0x6351BF,
                original="c74424040000803f",        # mov [esp+4], 1.0
                context=[(0, "c7442404")],
                field={"at": 4, "length": 4, "type": "f32"},
                encoding="operand-immediate", source="band_hi",
                vmin=0.01, vmax=0.99, default=0.55,
                note="written to the same band as the first-hit pair; stays "
                     "addressable should the two bands ever be re-split",
            ),
            exe_site(
                "band-escalated-lo", 0x6351C7,
                original="c704249a99993e",          # mov [esp], 0.30
                context=[(0, "c70424")],
                field={"at": 3, "length": 4, "type": "f32"},
                encoding="operand-immediate", source="band_lo",
                vmin=0.01, vmax=0.99, default=0.15,
            ),
            # -- sh3.exe .pcode: the reverse-telegraph NaN skip (ticket 14) ---
            exe_site(
                "reverse-telegraph-skip-jmp", 0x635111,
                original="ff35d4e16300",           # push dword [0x63E1D4]
                                                    # (the telegraph value, the
                                                    # 2nd pow() argument)
                # No invariant byte survives the rewrite -- same tradeoff
                # diesel-message-gate already pays (see its note above): the
                # push becomes a jmp, so every byte changes.
                context=[],
                replacement="e917070000cc",         # jmp to the trampoline
                                                     # landed in tc-helper-
                                                     # rewrite's padding, then
                                                     # 1 unreached byte
                encoding="fixed-rewrite",
                note="splices in the Speed < 0.0 skip gate: reversing feeds "
                     "the damage-chance calc's pow() a negative telegraph "
                     "value, which returns NaN and satisfies both downstream "
                     "branches toward 'apply damage' every real-second tick "
                     "regardless of the roll (umbrella ticket 04's 3rd-pass "
                     "answer). The trampoline itself lives in tc-helper-"
                     "rewrite's replacement, below -- see "
                     "docs/re-notes/04-diesel-damage-signature.md sec.6.",
            ),
            # -- sh3.exe .pcode: the time-compression helper ------------------
            exe_site(
                "tc-helper-rewrite", 0x635818,
                original="558bec505152ff750cd9e8d8650883ec04d91c24e877190000"
                         "d9e0d9e8dec15a5958c9c20800cccccccccccc",
                # push ebp / mov ebp, esp opens both bodies. Ticket 14's
                # trampoline now fills the rest of APE's replacement, so only
                # the last 2 int3s (of h.sie's original 6) still read as
                # padding in both.
                context=[(0, "558bec"), (42, "cccc")],
                replacement=HELPER_APE,
                encoding="fixed-rewrite",
                note="1-(1-p)^TC quantizes p onto a float32 lattice with a "
                     "silent inert floor at 2.98e-8; APE replaces it with a "
                     "clamped p*TC",
            ),
            # -- sh3.exe .pcode: pow() by name, not by offset (ticket 15) -----
            exe_site(
                "pow-thunk-call-resolver", 0x6371A8,
                original=POW_THUNK_HEAD
                         + "a178d651002d50dd0000"   # mov eax, [atol]
                                                     # sub eax, 0xDD50
                         + POW_THUNK_TAIL,
                # The rewritten 10 bytes share none with h.sie's, so the site
                # is widened to the whole thunk and anchors on the unchanged
                # prologue/marshalling and the call-eax tail.
                context=[(0, POW_THUNK_HEAD),
                         (len(POW_THUNK_HEAD) // 2 + 10, POW_THUNK_TAIL)],
                replacement=POW_THUNK_HEAD
                            + "e8403e0000"          # call 0x63B000 (resolver)
                            + "9090909090"
                            + POW_THUNK_TAIL,       # call eax -> pow, as before
                encoding="fixed-rewrite",
                note="h.sie reached the CRT's pow as [atol] - 0xDD50, which "
                     "only holds for Microsoft's msvcr71 7.10.3052.4 and "
                     "crashes under Wine's builtin; APE resolves pow by name "
                     "instead (pow-resolver-cave)",
            ),
            exe_site(
                "pow-resolver-cave", 0x63B000,
                original="00" * (len(POW_RESOLVER) // 2 + POW_CAVE_TAIL_LEN),
                context=[(len(POW_RESOLVER) // 2, "00" * POW_CAVE_TAIL_LEN)],
                replacement=POW_RESOLVER + "00" * POW_CAVE_TAIL_LEN,
                encoding="fixed-rewrite",
                note="GetProcAddress(GetModuleHandleA(\"MSVCR71.dll\"), "
                     "\"pow\") in the empty .pcode tail (0x63948C-0x63C000, "
                     "all zero and unreferenced in every surveyed install); "
                     "same address on Windows, Wine's own pow under Wine",
            ),
            # -- sh3.exe .pcode: the diesel damage report (tickets 09-10) -----
            exe_site(
                "diesel-message-gate", 0x6351F4,
                original="740e",                     # je +0xe -> skips the
                                                       # string call on full
                                                       # damage/destruction
                # No invariant byte survives the rewrite -- both bytes of a
                # 2-byte je change, unlike tc-helper-rewrite's padded body --
                # so this site carries no context. The re-notes discuss the
                # same one-byte-opcode tradeoff for the sibling gate at
                # 0x6351BD (never shipped as a site there); this is the first
                # site that actually pays it.
                context=[],
                replacement="9090",                   # nop nop: message now
                                                       # fires unconditionally
                encoding="fixed-rewrite",
                note="drops h.sie's partial-damage-only gate on string 4893's "
                     "call site, so the Chief's report fires on every hit, "
                     "including the destruction tail (ticket 09)",
            ),
            # -- data/Menu/en_menu.txt: the report's text (ticket 10) ---------
            menu_site(
                "diesel-message-text", "4893",
                "4893=If we don't cut power now, we're risking a breakdown!",
                {"kind": "literal",
                 "value": "Diesel trouble, Kaleun - normal wear and tear."},
                note="string 4893 is referenced exactly once in the whole "
                     "game (ticket 10) -- safe to overwrite in place, not "
                     "relocate",
            ),
        ],
    }

    return {
        "schema": 2,
        "generator": "tools/re-static/gen_manifest.py",
        "flagTableOffset": off(0x63CF00),
        "flagCount": 22,
        "versionStringOffset": off(0x63CC00),
        "versionStringLength": 7,
        "clobberedAddresses": ["0x%06X" % a for a in CLOBBERED],
        "patches": [hull, diesel],
        "apeAct": ape_act,
    }


def verify_against_reference(manifest):
    """Re-read every sh3.exe site out of a reference binary, if one is present.

    Ticket 13: there is one offset per site, not one per admitted build, so
    every reference binary under data/exe_with_hsie_patches/ is checked
    against it unconditionally -- no version match to gate on. The h.sie
    version stamp is read and reported for visibility only; it is never
    compared against an expected value, since APE no longer keeps one.
    """
    if not os.path.isdir(REF_DIR):
        return []
    refs = sorted(f for f in os.listdir(REF_DIR) if f.lower().endswith(".exe"))
    if not refs:
        return []
    problems = []
    for name in refs:
        blob = open(os.path.join(REF_DIR, name), "rb").read()

        stamp = blob[manifest["versionStringOffset"]:
                     manifest["versionStringOffset"] + manifest["versionStringLength"]]
        print("manifest: %s: h.sie stamp %r" % (name, stamp), file=sys.stderr)

        for patch in manifest["patches"]:
            for site in patch["sites"]:
                if site["store"] != "sh3.exe":
                    continue
                at = site["offset"]
                want = bytes.fromhex(site["original"])
                got = blob[at:at + len(want)]
                if got != want:
                    problems.append(
                        "%s: site %s at 0x%X holds %s, manifest says %s"
                        % (name, site["id"], at, got.hex(), site["original"]))
    return problems


def check_invariants(manifest):
    problems = []
    for s in manifest["apeAct"]["structs"]:
        head = len(bytes.fromhex(s["magic"])) + 4   # marker, then u32 version
        for f in s["fields"]:
            if f["at"] < head or f["at"] + 4 > s["size"]:
                problems.append("ape.act struct %s: field %s at %d is outside "
                                "the %d-byte struct's fields"
                                % (s["id"], f["id"], f["at"], s["size"]))
    clobber = set(int(a, 16) for a in manifest["clobberedAddresses"])
    for patch in manifest["patches"]:
        for site in patch["sites"]:
            if site["store"] != "sh3.exe":
                continue
            run = bytes.fromhex(site["original"])
            va = int(site["va"], 16)
            for i in range(len(run)):
                if va + i in clobber:
                    problems.append("site %s covers clobbered address 0x%X"
                                    % (site["id"], va + i))
            for at, hexbytes in site["context"]:
                want = bytes.fromhex(hexbytes)
                if run[at:at + len(want)] != want:
                    problems.append("site %s: context at %d does not match its "
                                    "own original run" % (site["id"], at))
            if site["encoding"] == "fixed-rewrite":
                rep = bytes.fromhex(site["replacement"])
                if len(rep) != len(run):
                    problems.append("site %s: replacement is %d bytes, original "
                                    "is %d" % (site["id"], len(rep), len(run)))
                for at, hexbytes in site["context"]:
                    want = bytes.fromhex(hexbytes)
                    if rep[at:at + len(want)] != want:
                        problems.append("site %s: context at %d does not hold in "
                                        "the replacement" % (site["id"], at))
            else:
                f = site["field"]
                if f["at"] + f["length"] > len(run):
                    problems.append("site %s: field runs past its original"
                                    % site["id"])
    return problems


def render(manifest):
    return json.dumps(manifest, indent=2, sort_keys=False) + "\n"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true",
                    help="fail if the checked-in manifest differs from this "
                         "generator's output")
    ap.add_argument("--ape-act", default=os.path.join(ROOT, APE_ACT_TEMPLATE),
                    help="the built ape.act template to measure struct "
                         "offsets from (default: %s)" % APE_ACT_TEMPLATE)
    args = ap.parse_args()

    previous = None
    if os.path.exists(OUT):
        previous = json.load(open(OUT, "r", encoding="utf-8")).get("apeAct")
    try:
        ape_act = ape_act_block(args.ape_act, previous)
    except ValueError as e:
        print("manifest: ape.act: %s" % e, file=sys.stderr)
        return 2

    manifest = build(ape_act)

    problems = check_invariants(manifest) + verify_against_reference(manifest)
    if problems:
        for p in problems:
            print("manifest: " + p, file=sys.stderr)
        return 2

    text = render(manifest)

    if args.check:
        if not os.path.exists(OUT):
            print("manifest: %s is missing; run gen_manifest.py" % OUT,
                  file=sys.stderr)
            return 1
        current = open(OUT, "r", encoding="utf-8", newline="").read()
        if current != text:
            print("manifest: %s has drifted from tools/re-static/gen_manifest.py."
                  % OUT, file=sys.stderr)
            print("          Re-run gen_manifest.py and review the diff.",
                  file=sys.stderr)
            return 1
        print("manifest: up to date (%s)" %
              hashlib.sha256(text.encode()).hexdigest()[:12])
        return 0

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        fh.write(text)
    print("manifest: wrote %s" % OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
