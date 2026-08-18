# Reword CONTEXT.md's anchor-resolution glossary to say "runtime anchors" specifically

Type: task
Status: resolved

Map: [v1.1 scope map](../map.md)

## Blocked by

None

## Question

[Ticket 08](08-adr-fixed-offset-vs-adr0003.md) found that ADR-0007's "Scope"
section already flags this as an undone follow-up: `CONTEXT.md`'s glossary
states, as a project-wide absolute, that "APE still resolves its value
anchors dynamically rather than trusting fixed addresses" (line 8) and that
`hardcoded offset` is "the alternative APE deliberately avoids for its
anchors" (**AOB pattern scan** entry, lines 23-25). Both now overclaim: the
GUI patcher legitimately uses fixed, per-build offsets for its static edits
(ADR-0007), and only `ape.act`'s runtime resolution of live values is bound
by ADR-0003's AOB-scanning rule.

Nothing to decide — ADR-0007 already states the correct boundary. This is a
mechanical task: reword the two `CONTEXT.md` passages (and the "Avoid" note)
to scope the claim to *runtime* anchors specifically, consistent with
ADR-0007's Scope section, so the glossary no longer misleads a contributor
reading it about what the static patcher does.

## Answer

Done 2026-08-18. Reworded both `CONTEXT.md` passages:

- **OAE sh3.exe** entry: scoped "resolves its value anchors dynamically" to
  `ape.act`'s runtime resolution specifically, with a pointer to ADR-0007 for
  the GUI patcher's separate, legitimate fixed-offset model.
- **AOB pattern scan** entry: scoped the "avoid hardcoded offset" note to
  `ape.act`'s runtime anchors, explicitly noting it's not a project-wide rule
  and pointing at ADR-0007 for the static patcher's identity-hash-verified
  fixed offsets.

No behavior change — documentation only.
