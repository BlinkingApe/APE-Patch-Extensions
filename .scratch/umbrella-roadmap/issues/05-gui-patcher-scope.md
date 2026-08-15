# GUI patcher tool: scope

Type: grilling
Status: resolved

## Blocked by

None

## Question

Surfaced 2026-08-15 by the hull-repair scope resolution
(`03-hull-repair-patch-scope.md`), which chose a **shipped static patcher `.exe`
with a GUI in the style of hsieOptions** as APE's delivery mechanism — a checkbox
per patch, plus input values for patches that take them. This is a second APE
deliverable alongside `ape.act`, and it did not exist on the map before.

Pin down enough to spin up its own `.scratch/<slug>/` feature effort:

- **Scope of what it patches.** Only APE's own edits? Or does it also expose
  h.sie's existing flag table at `0x63CF00` (20 flags, `0xFF`/`0x00`) as
  checkboxes — i.e. does APE partially replace hsieOptions? Re-exposing h.sie's
  own toggles is now technically trivial (`docs/re-notes/03-*.md` §2), which
  makes this a product decision, not a feasibility one.
- **Relationship to hsieOptions and OAE.** OAE ships an already-patched
  `sh3.exe`. If a user runs hsieOptions *after* APE's patcher, do APE's edits
  survive? Does APE's patcher need to detect and refuse, or co-exist?
- **Language and toolchain.** The existing stack is C++/CMake targeting PE32
  (see the toolchain baseline). A GUI patcher could be Win32/Qt/C#/Python —
  and this tool does not need to be PE32 or even C++, since it is not injected.
- **What it writes into.** `sh3.exe` for h.sie-cave edits; `ape.act` for APE's
  own runtime constants (see 06). Both, from one UI.
- **Distribution.** JSGME-installable folder is APE's existing end-user shape.
  Does the patcher ship inside that, run before it, or replace it?

## Notes

The byte-level mechanics are already solved for the two known patch targets —
`docs/re-notes/03-utanker-hull-repair-signature.md` (flag table, hull-repair roll
operands). This ticket is about the tool, not the bytes.

`patch1337`'s format (`OFFSET:OLD->NEW`, verified before write) remains a useful
reference for the write layer — see
`docs/re-notes/02-hsie-patch-identification-method.md`, last section.

## Answer

Resolved 2026-08-15. The tool's **shape** is decided; its **v1 contents** are
deliberately deferred (see "What is not decided" below) and tracked as ticket 07.

Two of this ticket's five bullets turned out to be **facts, not decisions**, and
were settled by going to the install at `C:\games\OneAlex Extended 1.5.2` rather
than by grilling. See "Findings" below — they materially changed the answer.

### Decisions

1. **Scope: APE-only. The flag table is never presented.**
   The GUI lists only APE's own features. It freely edits h.sie *cave operands*
   where an APE feature requires it — hull repair is delivered by narrowing the
   roll operands at `0x633633`, so APE writes into h.sie's appended sections
   regardless — but it does **not** expose the 22-byte flag table at `0x63CF00`,
   not even read-only. `HsieOptionsSelector` remains the owner of h.sie's own
   toggles. APE does not replace hsieOptions, partially or otherwise.

2. **Anchoring: AOB signatures + verify-before-write. No build lockout.**
   Each site is located by wildcarded AOB signature rather than the absolute VAs
   recorded in the re-notes, then its expected old bytes are verified before the
   write (patch1337 style). A signature miss or a byte mismatch refuses **that
   one site** and reports it; it does not abort the whole run or lock out an
   unrecognised build. This is ADR-0003's discipline applied to the static side.
   **Consequence:** every patch site now owes a wildcarded signature with a
   uniqueness proof. This is currently done for *no* site — both
   `03-utanker-hull-repair-signature.md` and ticket 04 flag it as outstanding.

3. **No version gate.** h.sie's own version string (`v 1.6E1` at VA `0x63CC00`)
   is *not* adopted as a compatibility test, even though Stiebler's tool uses it.
   AOB + per-site byte verification are judged sufficient.
   **Requirement this creates:** because the gate's clear "your h.sie is newer
   than this APE build" message is given up, per-site failures must name the site
   and the reason precisely. Silent or generic failure is not acceptable here.

4. **Two write targets: `sh3.exe` and `ape.act`, from one UI.**
   This **confirms the premise ticket 06 flagged with a ⚠**. Dropping `ape.cfg`
   dropped the config *file*, not `ape.act`'s runtime logic: crush-depth damage is
   computed live and cannot become a static byte. So the patcher writes h.sie-cave
   edits into `sh3.exe` **and** bakes APE-owned constants into `ape.act`'s binary.
   Ticket 06 stays alive as the "how" and is **not** void.

5. **Toolchain: C# WinForms on .NET Framework 4.x.**
   The deliverable is a single small `.exe` needing no runtime download — the
   decisive property for a mod-community tool, since the 4.x runtime is present on
   every Win10/11 install (verified: `v4.0.30319` present on this machine, and its
   `csc.exe` is on disk; VS2022 also installed; **no `dotnet` SDK installed**).
   Rejected: C++/ImGui (GUI work far slower for a checkbox list; `CMakeLists.txt`
   hard-fails on non-x86, so it would need a separate build tree) and
   Python/PyInstaller (30MB+ and routine antivirus false positives — fatal for a
   tool whose job is byte-patching a game executable).
   **Cost accepted:** a third language in the repo, and a build path CMake does
   not cover. Note this discards only a stub — `src/patcher/main.cpp` is an
   unimplemented scaffold (usage text and a `TODO`), not working code.

6. **Patch definitions: a generated manifest, embedded at build time.**
   Signatures are *authored* in Python (`tools/re-static/`) but *consumed* in C#.
   The Python tooling emits a manifest (JSON), checked into the repo and embedded
   as a resource in the C# exe. One self-contained file ships; nothing for a user
   to desync or tamper with; and the Python→C# handoff stays mechanical rather
   than hand-transcribed, because a mistyped signature silently corrupts a binary.
   Rejected: an external data file (h.sie's `HSIEContents.txt` approach — its own
   readme shows this is where that scheme is most fragile) and hand-written C#
   constants.

7. **Distribution: standalone, game root, zero JSGME interaction — ever.**
   Both `sh3.exe` and `ape.act` live in the game root folder and are patched
   there. APE is **not** a JSGME mod. The GUI patcher subsumes ADR-0005's
   `LoadLibrary` call-site patcher, so all APE byte-writing lives in one tool.
   **This reverses documented decisions** — see "Follow-ups" below.

### What is not decided (deliberately)

**v1 scope is deferred** until we know which of the planned patches are actually
viable. Hull repair is proven; diesel damage is located but its knobs are not yet
confirmed expressible as patchable operands (ticket 04); the docking mod is still
fog. Ticket 07 holds this question, blocked by 04. The patcher effort therefore
**cannot yet spin up a full feature-scoped `.scratch/` effort** — its architecture
is settled, its feature list is not.

### Findings from the install (facts, not decisions)

Located at `C:\games\OneAlex Extended 1.5.2\Support\Stiebler4C_V16B1\`. Full
write-up folded into `docs/re-notes/03-utanker-hull-repair-signature.md` §2.

- **`HsieOptionsSelector` writes flag bytes only** — its readme says it "alters
  the ON-OFF switches in the patched SH3.exe". This answers the ticket's
  "do APE's edits survive if a user runs hsieOptions after?" bullet: **yes**.
  Given decision 1, the two tools touch strictly disjoint bytes. No detection,
  no refusal, no co-existence logic is needed.
- **`HSIEContents.txt` is an authoritative name→flag mapping for all 22 patches**,
  declaring the table offset (`0x14EF00`) and naming every patch in order. The
  three flags previously derived by variant-diffing (`0x63CF0A`, `0x63CF0C`,
  `0x63CF13`) match its ordering exactly, which validates it. **This largely
  dissolves the map's fog item** about needing a new variant exe per patch.
- **The table has 22 entries, not 20** — a correction to the re-note.
- **`0x63CF05` is Hydrophone Fix, not a second U-Tanker flag.** This retires the
  ticket-03 caveat that "the U-Tanker mod may span more than one flag": that cave
  merely *consumes* the same docking variable. U-Tanker is `0x63CF0C` alone.
- **Diesel Damage is `0x00` — disabled — in the live install.** Ticket 04 targets
  a patch the user is not currently running.
- **The live install hashes `51d14ca3…`, not the registered `4B6BEE20`**, at an
  identical 1,417,216 bytes. Two same-size, different-content builds now exist;
  ticket 03's "sibling build" caveat is real and has a second data point.
- **h.sie's own numeric parameters** are a float array at VA `0x63C074`, +4 bytes
  each — h.sie's precedent for value patching. They do **not** obviously match
  `hsie.ini`'s values, which is unresolved and noted in the re-note.

## Amendment (same day, 2026-08-15) — decisions 1, 2 and 3 revised

Reopened by the user immediately after resolution, on two proposals. A survey of
**four installs on the dev machine** settled both empirically. Data recorded in
`docs/re-notes/oae-builds.md` ("h.sie version survey").

### A-1. Locator: version allowlist + fixed offsets **replaces** AOB scanning

**Supersedes decision 2.** Runtime AOB scanning is dropped. The patcher now:

1. Reads the h.sie version string at VA `0x63CC00` (file `0x14EC00`).
2. Checks it against an **allowlist** — currently `v 1.6B1` and `v 1.6E1`,
   extensible — and refuses h.sie-cave writes with a clear message if unlisted.
3. Uses **fixed offsets from the manifest, keyed by version**.
4. **Verifies expected old bytes before every write** — retained from decision 2, and
   this is what makes dropping AOB safe.

AOB is **demoted to a Python authoring-time tool** used to prove an offset holds
across the supported build set. It is never shipped in the C# patcher.

**Why the reversal (evidence, not preference):**

- The **premise behind resisting a version gate was wrong.** It was not
  "GWX-KC lags OAE" — the split runs through OAE itself: OAE 1.5.2 is `v 1.6E1`
  while the *newer* OAE 1.5.3.7 is `v 1.6B1`, same as KC2.5. An allowlist is
  needed to support **OAE alone**, so this is not a concession to other suites.
- The two installs' `HSIEContents.txt` files **differ by exactly one line** — the
  version string. Flag-table offset, slider base, and all 22 patch names in order
  are identical across versions.
- The **hull-repair cave is at identical offset `0x145633`, matching exactly
  once, in all four binaries** across both h.sie versions. The assumption that
  caves move between versions — which was the main argument for AOB — is false
  for the one cave we have.
- Stock `.text` diverges ≤0.19% across all four suites, so even APE's own
  `LoadLibrary` call site (ADR-0002), which lands in stock code rather than
  h.sie's sections, is unlikely to need signature scanning.

**Cost accepted / what still argues for care:** h.sie's `.pcode` does differ
1.16–2.87% between `v 1.6B1` and `v 1.6E1`. Caves are mostly stable but not
provably all stable — hence verify-before-write is **not** optional, and an
offset must be proven per version before that version joins the allowlist.

**Requirement carried forward from decision 3 is now satisfied differently:** the
version gate exists after all, so the "your h.sie is newer than this APE build"
message is available. Per-site failures must *still* name site and reason.

### A-2. The flag table is read — read-only — for dependency checking

**Narrows decision 1.** APE still **never writes** the flag table and never
offers h.sie's toggles, so `HsieOptionsSelector` keeps that job and the scope
boundary holds. But APE now **reads** it: each manifest entry declares the h.sie
flag index it depends on, and if that byte is `0x00` the control is shown
**disabled with a specific explanation** — e.g. *"Requires h.sie's Diesel Damage
Fix, currently off. Enable it in HsieOptionsSelector, then reopen APE."*

Rejected: warning but allowing the write (a user can finish feeling done while
the feature does nothing — the exact confusion this prevents), and hiding the
entry (no explanation for a feature that appears missing).

**Why:** **Diesel Damage is `0x00`/off in all four installs surveyed**, both OAEs
included. Without this check APE would write hull-repair-style operands into a
patch that never executes, and the user would observe nothing and reasonably
conclude APE is broken.

This makes the earlier "not even read-only" phrasing of decision 1 obsolete.
The boundary is now precisely: **APE reads h.sie's flags, never writes them.**

### Follow-ups this resolution creates (documentation debt, not decisions)

- **`CONTEXT.md` glossary is now wrong.** Its JSGME entry states "APE packages its
  end-user deliverable as a JSGME-installable folder". Decision 7 reverses this.
- **ADR-0005 is now wrong.** Its entire premise is a JSGME-installable package
  plus a separate patcher. Needs superseding: no JSGME, one tool, game root.
  Its "never redistribute modified copyrighted binary bytes" rationale survives.
- **ADR needed for the toolchain split** (C# tool beside a C++ PE32 module) and
  for the manifest generation step.
- **Ticket 02's uninstall/revert bullet** is written on a JSGME premise
  ("JSGME expects a clean disable") that no longer exists. Revert is now entirely
  APE's own problem — re-read that bullet before resolving 02.
- **`src/patcher/main.cpp`** and its CMake target are superseded by the C# tool.
