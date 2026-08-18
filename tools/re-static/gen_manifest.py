#!/usr/bin/env python3
"""Generate APE's patch manifest (umbrella ticket 08's schema).

The manifest is the authoring-time record of every site APE writes: where it is,
what is invariant about it, what its complete original byte run / ini line is, and
how its value is derived from the GUI's fields.  It is checked into git as the
reviewable source of truth and embedded into the patcher exe as a resource.

Two modes:

    python gen_manifest.py                 write data/manifest/ape-manifest.json
    python gen_manifest.py --check         fail if the checked-in file has drifted

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
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "data", "manifest", "ape-manifest.json")
REF_DIR = os.path.join(ROOT, "data", "exe_with_hsie_patches")

# h.sie appends .pcode at VA 0x633000 / raw 0x145000 and .pconst at VA 0x63C000 /
# raw 0x14E000 -- the same delta for both (docs/re-notes/03, sec.1).
VA_TO_FILE = 0x4EE000


def off(va):
    return va - VA_TO_FILE


# The build APE v1 shipped against.  Ticket 07: OAE 1.5.2 with h.sie v 1.6E1.
# Kept as the id every exe_site() call is authored under -- the file offsets
# below all come from RE against this build. Adding a version means re-proving
# every site's offset against a real binary of that version first (ticket 08's
# admission bar); once proven, add it to VERSIONS below and every exe_site()
# picks up an offset entry for it automatically (see ALL_VERSION_IDS, derived
# from VERSIONS once it exists, a few lines down -- one list, not two to keep
# in sync).
VERSION_ID = "oae-1.5.2-hsie-1.6E1"
KC25_VERSION_ID = "kc-2.5-hsie-1.6B1"

# Build identity is a *normalised* hash, not the file's own: every byte run APE
# owns is reset to its manifest original and h.sie's 22-byte flag table is zeroed
# before hashing.  Two consequences, both required:
#
#   - an install APE has already patched still identifies as the same build, so
#     the tool can reopen showing what it wrote;
#   - a HsieOptionsSelector run, which rewrites only flag bytes, does not change
#     the build's identity either.
#
# `referenceSha256` is the plain file hash of the pristine binary this was
# authored against, kept for the record and shown in the header.
#
# ⚠ KC 2.5's plain file hash moves on every flag flip (ADR-0007) and the value
# quoted in ADR-0007/ticket 01 (`a7b5fc03...`) is a stale one, captured before a
# flag flip and before the identity model existed -- it is not usable as either
# field here. `referenceSha256` for KC 2.5 is left unset: only its normalised
# identity was ever recorded in full (docs/re-notes/05-kc25-admission-evidence.md);
# the plain hash is on record only truncated (`ab85d6e2...`) and this field is
# documentary only, never used to decide admission.
VERSIONS = [
    {
        "id": VERSION_ID,
        "buildName": "OAE 1.5.2",
        "hsieVersion": "v 1.6E1",
        "identitySha256":
            "36d66176f5be49f4708f05b6c076e2e7abf8e819ff81a38bbf85ede2a448d12c",
        "referenceSha256":
            "51d14ca3c08f42c9d315a9ec8e7df8948e97e14453cfa9857e33b820d233a87f",
    },
    {
        "id": KC25_VERSION_ID,
        "buildName": "KC 2.5",
        "hsieVersion": "v 1.6B1",
        "identitySha256":
            "361eaaa088b71b103580a9e7101009c8ddd43387749cc54fdd5bb0d9e115926c",
        "referenceSha256": None,
    },
]

# Every admitted build's offsets are authored here, not per-site: ticket 08's
# spike (docs/re-notes/05-kc25-admission-evidence.md) re-read all seven sites
# out of a real KC 2.5 binary at the OAE 1.5.2 offsets and found exact matches,
# so KC 2.5 shares OAE 1.5.2's offsets rather than carrying its own. Derived
# from VERSIONS so the two cannot drift apart.
ALL_VERSION_IDS = [v["id"] for v in VERSIONS]

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
# 21 bytes into the original's 38-byte body, int3-padded out to 0x635844.
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
) + "cc" * 23


def exe_site(sid, va, original, context, field=None, source=None,
             replacement=None, encoding=None, vmin=None, vmax=None, default=None,
             note=None):
    site = {
        "id": sid,
        "store": "sh3.exe",
        "encoding": encoding,
        "va": "0x%06X" % va,
        "offsets": dict((vid, off(va)) for vid in ALL_VERSION_IDS),
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


def build():
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
            # -- sh3.exe .pcode: the time-compression helper ------------------
            exe_site(
                "tc-helper-rewrite", 0x635818,
                original="558bec505152ff750cd9e8d8650883ec04d91c24e877190000"
                         "d9e0d9e8dec15a5958c9c20800cccccccccccc",
                # push ebp / mov ebp, esp opens both bodies; the last three
                # int3s close the padded region in both.
                context=[(0, "558bec"), (41, "cccccc")],
                replacement=HELPER_APE,
                encoding="fixed-rewrite",
                note="1-(1-p)^TC quantizes p onto a float32 lattice with a "
                     "silent inert floor at 2.98e-8; APE replaces it with a "
                     "clamped p*TC",
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
        "schema": 1,
        "generator": "tools/re-static/gen_manifest.py",
        "versions": VERSIONS,
        "flagTableOffset": off(0x63CF00),
        "flagCount": 22,
        "versionStringOffset": off(0x63CC00),
        "versionStringLength": 7,
        "clobberedAddresses": ["0x%06X" % a for a in CLOBBERED],
        "patches": [hull, diesel],
    }


def normalised_hash(manifest, blob, version_id):
    """The build identity: APE's own sites reset to their originals and h.sie's
    flag table zeroed, so neither an APE patch nor a HsieOptionsSelector run
    changes which build this is."""
    b = bytearray(blob)
    for patch in manifest["patches"]:
        for site in patch["sites"]:
            if site["store"] != "sh3.exe":
                continue
            at = site["offsets"][version_id]
            original = bytes.fromhex(site["original"])
            b[at:at + len(original)] = original
    at = manifest["flagTableOffset"]
    b[at:at + manifest["flagCount"]] = b"\0" * manifest["flagCount"]
    return hashlib.sha256(bytes(b)).hexdigest()


def verify_against_reference(manifest):
    """Re-read every sh3.exe site out of a reference binary, if one is present.

    Which offsets and which h.sie version stamp to expect depend on which
    admitted *build* the binary's plain hash matches -- there is no longer one
    global answer now that more than one version is admitted. A reference
    binary that matches no version's referenceSha256 is skipped entirely: the
    sibling variant set under data/exe_with_hsie_patches/ is not any admitted
    build, so this is usually a no-op that exists to catch a checked-in install
    binary.
    """
    if not os.path.isdir(REF_DIR):
        return []
    refs = sorted(f for f in os.listdir(REF_DIR) if f.lower().endswith(".exe"))
    if not refs:
        return []
    problems = []
    for name in refs:
        blob = open(os.path.join(REF_DIR, name), "rb").read()
        plain = hashlib.sha256(blob).hexdigest()

        matched = [v for v in manifest["versions"] if v.get("referenceSha256") == plain]
        for version in matched:
            vid = version["id"]

            got = normalised_hash(manifest, blob, vid)
            if got != version["identitySha256"]:
                problems.append("%s: normalised identity is %s, manifest says %s"
                                % (name, got, version["identitySha256"]))

            want_stamp = version["hsieVersion"].encode("ascii")
            stamp = blob[manifest["versionStringOffset"]:
                         manifest["versionStringOffset"] + manifest["versionStringLength"]]
            if stamp != want_stamp:
                problems.append("%s: h.sie version stamp is %r, expected %r (%s)"
                                % (name, stamp, want_stamp, version["buildName"]))

            for patch in manifest["patches"]:
                for site in patch["sites"]:
                    if site["store"] != "sh3.exe":
                        continue
                    at = site["offsets"][vid]
                    want = bytes.fromhex(site["original"])
                    got = blob[at:at + len(want)]
                    if got != want:
                        problems.append(
                            "%s: site %s at 0x%X holds %s, manifest says %s (%s)"
                            % (name, site["id"], at, got.hex(), site["original"],
                               version["buildName"]))
    return problems


def check_invariants(manifest):
    problems = []
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
    args = ap.parse_args()

    manifest = build()

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
