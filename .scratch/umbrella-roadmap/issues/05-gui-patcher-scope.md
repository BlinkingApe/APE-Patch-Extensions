# GUI patcher tool: scope

Type: grilling
Status: open

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
