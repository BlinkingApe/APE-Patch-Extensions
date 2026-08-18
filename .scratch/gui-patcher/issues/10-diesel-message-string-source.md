# Where does the new diesel damage message's text get sourced from?

Type: research
Status: resolved

Map: [v1.1 scope map](../map.md)

## Blocked by

None. Not blocking on [ticket 09](09-diesel-damage-report-message.md) — the
wording is already decided there; this is the mechanism to land it.

## Question

Ticket 09 settled the diesel damage message's new text and dropped the
partial-only gate, but not how the replacement string actually gets into
the game. The stock call (`docs/re-notes/04-diesel-damage-signature.md:390`)
is `push 0x131D; call 0x6371D4` — a lookup of string-table entry 4893, not
inline text.

Investigate:

- Is string 4893 ("If we don't cut power now...") referenced anywhere else
  in the game? If not, overwriting its text in place is simplest. If it is,
  APE needs a different, unused entry instead.
- Where does SH3's string table actually live on disk (a `Menu*.txt` /
  localization file under `data/Menu/`, most likely) — is it something APE
  can patch/ship an override for the way it patches `sh3.exe` and
  `hsie.ini`, or does it need a different mechanism entirely?
- Does the game support German text (`kein Stress`) in that table without
  encoding issues, given SH3's normal localization pipeline?

## Notes

Research ticket — produce a written recommendation as a linked asset, not
code. Blocks *building* ticket 09's decision, not scoping v1.1.

## Answer

Resolved 2026-08-18 via live disassembly and file inspection against the
real install at `C:\games\OneAlex Extended 1.5.2\` (`sh3.exe` sha256
`eeef8595…`, matching the build already surveyed in
`docs/re-notes/06-flooding-model-signature.md`; Diesel Damage applied,
APE disabled), using `pefile`+`capstone`. An earlier working assumption
(the string might live inside h.sie's own appended `.pconst` section,
since the call target `0x6371D4` sits inside h.sie's `.pcode`, not stock
code) turned out wrong on inspection — recorded here because it changed
what got checked:

1. **`0x6371D4` is h.sie's own shared trampoline, not a string source.**
   Disassembly shows a 9-instruction wrapper: takes 3 args, adds a literal
   `-1`, and tail-calls stock function `0x516B20` (inside the *original*
   `.text` section, well below h.sie's appended sections). `0x516B20` is
   called from 7 other sites in stock `.text` — it's SH3's own generic
   message-display routine, reused by h.sie, not something h.sie added
   string data for. Searching the entire file for the message text as
   both ASCII and UTF-16LE found **zero occurrences** — the text isn't
   baked into `sh3.exe` anywhere. It lives outside the binary, exactly as
   originally suspected before the section-based worry.
2. **String 4893 is referenced exactly once, confirmed two ways.**
   `push 0x131D` (the literal ID) occurs at exactly one file offset in the
   whole exe (the diesel cave's own push, `0x6351F8`) — h.sie's trampoline
   at `0x6371D4` itself is shared (13 call sites across different caves),
   but only one of them pushes ID 4893. And the real install's
   `data/Menu/en_menu.txt` has exactly one `4893=` line (line 2508:
   `4893=If we don't cut power now, we're risking a breakdown!`).
   **Overwriting it in place is safe** — nothing else in the game reads
   this ID.
3. **Mechanism: a genuine third site type, not `Sh3Exe` or `HsieIni`.**
   `data/Menu/en_menu.txt` is a native game data file (`[STRINGS]`
   section, `MaxStrings=5000`, plain `ID=text` lines, CRLF), present at
   that exact path in the install root — not part of `sh3.exe`, not
   `hsie.act` (h.sie's patches live appended inside `sh3.exe` itself, per
   `docs/re-notes/03-utanker-hull-repair-signature.md`, but this string
   isn't one of them). APE needs a new site kind — a keyed line inside an
   external text file — alongside `Sh3Exe` and `HsieIni` in
   `src/patcher_gui/Manifest.cs`'s `SiteStore` enum, following the same
   round-trip `hsie.ini` sites already use: capture the live original
   `4893=…` line at apply time, write APE's replacement, restore the
   captured original on uncheck.
4. **Encoding: moot for this string.** The file overall is "Non-ISO
   extended-ASCII" (other lines carry non-ASCII bytes for accented
   characters), but both the stock text and APE's decided replacement
   (`"Diesel trouble, Kaleun. Normal wear and tear, we'll get her
   running, kein Stress."`) are 100% plain ASCII — no diacritics, no `ß`.
   Round-trips cleanly regardless of the file's codepage.
