# Diesel damage message: shorten the wording

Type: task
Status: resolved

Map: [v1.1 scope map](../map.md)

## Blocked by

None. Supersedes the wording [ticket 09](09-diesel-damage-report-message.md) landed
and ticket 10/the built `diesel-message-text` site currently ship.

## Question

The user wants the diesel damage report's text shortened, raised 2026-08-18 while
reviewing ticket 09 in-editor.

## Answer

New text, replacing what ticket 09 decided and what is currently live in
`tools/re-static/gen_manifest.py`'s `diesel-message-text` site and
`data/manifest/ape-manifest.json`:

> Diesel trouble, Kaleun - normal wear and tear.

## Not resolved here (build)

The site already exists (`SiteStore.MenuText`, ticket 10's mechanism) — this is a
one-line text swap in `menu_site("diesel-message-text", ...)`'s `ape` value, a
manifest regeneration, and a test-string update in
`tests/patcher_gui/PatchSessionTests.cs` (`Test_ApplyingDieselDamageRewordsTheReportAndDropsTheGate`),
not new mechanism.
