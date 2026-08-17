# KC 2.5 (`v 1.6B1`): allowlist admission evidence

Spike run 2026-08-17 against the real install at `C:\games\KC2.5`, to answer
ticket 08's admission bar — *every site's offset independently re-proven against
a real binary of that version* — before
`.scratch/gui-patcher/issues/01-beta-feedback-fixes.md` folds KC into the
manifest.

Method: read every manifest `sh3.exe` site out of KC's binary **at the offsets
authored for OAE 1.5.2**, then diff whole cave regions rather than only the
seven sites, since the patches' semantics depend on the surrounding code. Script
kept out of tree (throwaway); every number below is reproducible from
`data/manifest/ape-manifest.json` plus the install.

## Verdict: clean. All seven sites hold h.sie's original bytes at the OAE offsets.

| Site | VA | File offset | KC 2.5 |
|---|---|---|---|
| `hull-roll-width` | `0x63364B` | `0x14564B` | exact match |
| `hull-roll-floor` | `0x633655` | `0x145655` | exact match |
| `band-first-hi` | `0x6351B0` | `0x1471B0` | exact match |
| `band-first-lo` | `0x6351B5` | `0x1471B5` | exact match |
| `band-escalated-hi` | `0x6351BF` | `0x1471BF` | exact match |
| `band-escalated-lo` | `0x6351C7` | `0x1471C7` | exact match |
| `tc-helper-rewrite` | `0x635818` | `0x147818` | exact match (all 38 bytes) |

"Exact match" is stronger than the bar asks for: not merely context bytes
holding, but the complete original run identical, including the mutable field.
KC ships h.sie's stock tuning untouched.

`hsie.ini`'s eight diesel keys are likewise byte-identical to the manifest's
stored original lines, **including the trailing tabs** on `Val13`/`Val14`/`Val15`
that ticket 08's exact-text revert depends on.

## Cave regions, KC 2.5 vs OAE 1.5.2 (APE's sites reset to original in both)

| Region | Result |
|---|---|
| U-Tanker resupply cave `0x6335A1`–`0x6336E0` | **identical** |
|   block 1 (fuel/second resource) | identical |
|   block 2 (**hull repair**) | identical |
|   block 3 (torpedo reload) | identical |
| U-Tanker docking gate cave `0x633743` | identical |
| `jmp` hook site `0x00455CB4` | identical |
| Diesel cave `0x6350A0`–`0x635260` | **identical** |
|   surfaced gate `0x6350CA` | identical |
|   threshold test `0x635129` | identical |
|   damage band block `0x6351A0`–`0x6351E0` | identical |
| TC helper `0x635818` | identical |
| whole `.pcode` `0x145000`–`0x14E000` | 1057/36864 differ (**2.87%**) |
| stock `.text` `0x1000`–`0x145000` | 2530/1327104 differ (0.19%) |

The 2.87% figure is the same divergence `oae-builds.md` recorded in the
2026-08-15 survey — and **none of it lands in any cave APE reads or writes**.
Every region either patch depends on is byte-for-byte the same code as the build
the RE was done against.

## The U-Tanker docking code the hull-repair patch targets is present

Asked explicitly, because hull repair would have been OAE-only regardless of
offsets had KC shipped without it:

- Mission-order match strings `"Dock at: U-"` (`0x14EC0A`) and
  `"Anlegen in: U-"` (`0x14EC18`) — both present, same offsets as OAE.
- Docking gate cave at `0x633743`, resupply cave at `0x6335A1`, and the
  `jmp` hook at `0x00455CB4` — all present and byte-identical.
- **U-Tanker Fix (flag #13) is ON** in KC.

## Build identity

`identitySha256` for KC 2.5 is

```
361eaaa088b71b103580a9e7101009c8ddd43387749cc54fdd5bb0d9e115926c
```

⚠ **`a7b5fc03…`, quoted in ADR-0007 and in ticket 01, is not this number.** It is
KC's *plain* file hash from the 2026-08-15 survey, recorded before the identity
model existed. Anything writing an allowlist entry must use the normalised hash
above.

The two are related exactly as the identity model predicts. KC's plain hash is
now `ab85d6e2…`, and it was proven — by toggling flag bytes and re-hashing —
that `a7b5fc03…` is **today's binary with flag #20 (Diesel Damage Fix) toggled**,
and nothing else. The flag flip changed the file hash and left the build identity
untouched, which is the whole point of zeroing the flag table before hashing
(`gen_manifest.py:47-53`).

## OAE 1.5.3.7 is a genuinely separate build, as ticket 01 assumed

Surveyed in the same pass, since it shares KC's h.sie version *and its exact
file size* (1,384,448 bytes):

| Build | h.sie | Plain sha256 | Identity | Diesel flag |
|---|---|---|---|---|
| KC 2.5 | `v 1.6B1` | `ab85d6e2…` | `361eaaa0…` | **ON** |
| OAE 1.5.3.7 | `v 1.6B1` | `1b6a5823…` | `6769bc4f…` | off |

Different identities. Admitting KC does not admit OAE 1.5.3.7 — the behaviour
ticket 01's story 19 asks a test to assert. Note OAE 1.5.3.7's seven sites also
all read as exact matches, so it is admissible on the same evidence whenever
someone wants it; this note does not admit it.

## Correction to the survey

`oae-builds.md` says *"Diesel Damage is disabled in every install surveyed."*
**No longer true of KC** — flag #20 is ON as of 2026-08-17 (the file's mtime
matches; the maintainer flipped it ahead of this spike, as the KC test plan
required). Nothing upstream needs enabling before APE can offer the feature on
KC. The claim still holds for OAE 1.5.3.7 and GWX.

## What this does not establish

- **Nothing was written.** This is a read-only survey; no KC file was modified
  by the spike, and no round-trip apply/revert was run against KC. Ticket 01's
  `v 1.6B1` fixture round-trip test is still owed.
- **The h.sie version stamp is not the identity.** KC reads `v 1.6B1` at
  `0x14EC00` as expected, but per ADR-0007 that string is a label, not the check.
- **Only the caves listed were diffed.** 2.87% of `.pcode` differs somewhere; a
  future site outside these regions gets no free ride from this note.
