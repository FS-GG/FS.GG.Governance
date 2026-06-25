# Contract: Docs/examples checks — link + reference currency (F24, P2)

**Library**: `FS.GG.Governance.DocsChecks` | **Story**: US2 | **FR**: 004, 009, 010, 012

Scans FsDocs / literate scripts / public-API docs for link currency and reference currency. Scope is
**currency of the declared docs sources**, not a full FsDocs site build (spec Assumption "Scope of FsDocs").

## C1 — Link currency (FR-004, SC-002)

- `LinkResolves` ⇒ no finding.
- `LinkDangling target` ⇒ one **Blocking** `docs.link-currency` finding whose `Location.File` = the docs
  source, `Location.Detail` = the link text, and `Message` names the unresolved target (acceptance 2.2).

## C2 — Reference currency (FR-004, SC-002)

- `ReferenceResolves` ⇒ no finding.
- `ReferenceStale symbol` ⇒ one **Blocking** `docs.reference-currency` finding naming the stale
  symbol/anchor and its source location (acceptance 2.3).

## C3 — Example freshness (acceptance 2.4, advisory boundary)

- A docs example whose freshness reduces to compile/evaluate is checked by the **package transcript**
  machinery (it is deterministic). A docs example whose "match the current product surface" verdict requires
  prose/intent judgement is emitted **Advisory** (`BaseSeverity = Advisory`) and never blocks (FR-011, US5).

## C4 — Clean pass + determinism (SC-002, SC-005, FR-010)

- A docs surface where every link and reference resolves ⇒ **zero** findings (zero false positives,
  acceptance 2.1).
- `evaluate` sorts by `(Source, locus)`; identical `DocsFacts` ⇒ byte-identical findings.

## C5 — Input vs defect & seam (FR-012, FR-007)

- `Unreadable` sources ⇒ `IsInputState` `docs.source-unreadable` findings naming the source, never a
  fabricated pass.
- The only filesystem seam is `Interpreter.DocsPort` (`ReadSource` / `ResolveTarget` / `ResolveSymbol`);
  `evaluate` is pure/no-I/O.

## Acceptance (maps to spec US2 scenarios)

1. All links + references resolve ⇒ pass, evidence recorded, zero findings.
2. Dangling link ⇒ `docs.link-currency` naming file + link + target.
3. Removed/renamed symbol/anchor ⇒ `docs.reference-currency` naming the stale reference.
4. Judgement-heavy example staleness ⇒ Advisory finding (US5); compile/evaluate staleness ⇒ deterministic
   (via package transcripts).
