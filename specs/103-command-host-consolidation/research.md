# Phase 0 Research: Command-host second extraction pass (#49)

All findings below are from a read-only survey of the current `main`-based branch (three parallel code investigations + the 2026-07-02 review report `docs/reports/2026-07-02-141008-code-quality-architecture-review.md`). Line numbers are as-surveyed and indicative.

## D1 — Impure-leaf duplication map (what moves into `CommandHost`)

**Decision**: Relocate five impure leaves to `src/FS.GG.Governance.CommandHost/CommandHost.fs` and have every host call the shared version.

| Helper | Copies (defining sites) | Equivalence |
|---|---|---|
| `writeAtomic` | Route:46, Ship:57, Verify:62, Release:48, Evidence:448, Refresh:189, CacheEligibility:56 (**7**) | byte-identical (temp write + `File.Move(..,true)`) |
| `realHandoffs` | Route:149, Ship:179, Verify:284 (**3**) + divergent mirror `Cli/ArtifactReading.locateHandoffs:347` | 3 identical (`String.CompareOrdinal`); mirror uses `Array.sortBy` — see D3 |
| `senseEnvironmentReal` | Ship:167, Verify:196, Release:67 (**3**) | equivalent; Release uses unqualified `EnvironmentClass.*` to dodge a `Snapshot.Model.CiEnvironment` name clash — preserve that qualification when shared |
| `senseBuilderReal` | Ship:173, Verify:202, Release:74 (**3**) | identical (`BuilderIdentity "fsgg"`) |
| snapshot/catalog `step` arms (`SenseScope`+`LoadCatalog`) | Route:60, Ship:71, Verify:76, CacheEligibility:217 (**4**) | code-identical; Cache trimmed comments only |

**Rationale**: one definition per concern removes the drift mechanism (Principle III). None of these five appear in any `.fsi` today — they are internal `let`s — so relocating them is **zero surface** on the host side (only adds `val`s to `CommandHost.fsi`).

**Alternatives considered**: a new `CommandHost.Ports` module — rejected as over-engineering; the existing `CommandHost` leaf (post-first-pass, already holds `guard`/`drive`) is the right home.

**Watch-outs**: (a) Release's `senseEnvironmentReal` name-clash qualification; (b) the shared `step`-arm helper must be a function `step` *calls*, so `step`'s public signature (`ports -> effect -> msg`, in 9 `.fsi`s) is untouched.

## D2 — M-CLI-3: argv value-swallow guard (the biggest correctness win)

**Decision**: Add one shared value-consuming helper that rejects a `--`-prefixed next token as `MissingValue`, and route every single-value option arm through it (all 7 host `Loop.fs` parsers + `Cli.requireValue`).

**Evidence**: every host uses `"--repo" :: v :: more -> … Some v` with no guard; `fsgg verify --repo --json` binds `Repo="--json"` and drops JSON. The fix idiom **already exists** in the same files for the positional `--paths` consumer:
```fsharp
| t :: more when not (t.StartsWith "--") -> takePaths (t :: acc) more
```
`Cli.requireValue` (`Cli.fs:194`) has the same defect (`value :: tail` accepts anything).

**Rationale**: a single shared guard both fixes the bug uniformly and removes seven near-identical parse idioms. Parsing lives in `Loop.fs` (not `Interpreter.fs`).

**Divergence to reconcile while here**: `MissingValue` is spelled differently per host (`MissingValue "--flag"` vs Release/Refresh string records vs `MissingOptionValue`). Converge the *guard*, not necessarily the error type — keep each host's existing error DU to avoid gratuitous surface churn; the shared helper is parameterized over "how to signal missing".

**Alternatives**: a full shared argv parser replacing every host's `go`/`loop` — rejected for this pass (larger surface risk; the hosts' option *sets* genuinely differ). Extract only the value-guard now; a full parser is a possible future item.

## D3 — `realHandoffs` sort: convergence, not a bug fix

**Decision**: Fold the lone `Array.sortBy Path.GetFileName` in `Cli/ArtifactReading.locateHandoffs` into the shared `String.CompareOrdinal` `realHandoffs`. Output is unchanged.

**Rationale**: F#'s structural comparison of `System.String` dispatches to `String.CompareOrdinal`, and the readiness-dir keys are unique, so `sortBy` and `sortWith (CompareOrdinal)` produce identical order today. The value is *robustness* — a maintainer reading `sortBy` can't see the ordinal guarantee and might port it to a culture-sensitive form. This is why the spec was corrected: it is **not** an observable defect (SC-001 treats it as unchanged-output + single-definition, not RED→GREEN).

**Scope note**: `locateHandoffs` lives in the `Cli` project, outside the seven hosts. Pulling it into the shared sort is in-scope because it is literally the drifted copy the review named.

## D4 — M-CLI-7: Evidence `--plain` is a dead field, not an override

**Decision**: Make EvidenceCommand's `--plain` a documented no-op consistent with the additive-ANSI-free convention; do not change any other host (they are already correct).

**Evidence**: `HumanText/RenderMode.selectMode` checks `explicitJson` first, so `--plain` can never override JSON anywhere. Route/Ship/Verify force `explicitPlain=false` on the Json arm. Evidence parses `ExplicitPlain` (`Loop.fs:108`) but `render` switches only on `format` — the field is consumed by nothing, and Evidence has no HumanText/Spectre dependency (no Rich/Plain path exists). The review's "silently overrides" phrasing is imprecise; the code reality is "accepted-but-inert".

**Options**:
- **(A, chosen)** Keep accepting `--plain` but document it in help/usage as a no-op for Evidence (Evidence emits no ANSI), and delete the misleading unused field plumbing OR wire it to the (nonexistent) ANSI path — since there is no ANSI path, the honest fix is: keep the flag accepted for CLI-uniformity, mark it explicitly inert in usage text, and drop the dead `ExplicitPlain` model field if it drives nothing.
- (B) Reject `--plain` in Evidence as unsupported — rejected: breaks CLI-surface uniformity across hosts and would be a behavior change for scripts passing it.

**Rationale**: (A) removes the "silently dead" property the review flagged without a surface-breaking behavior change; matches the convention already documented in `Cli.fs:222-224` and the existing Evidence comment at `Loop.fs:106-108`.

## D5 — F2 `ExitDecision`: adopt canonical or delete (Tier-1)

**Decision (default)**: Adopt the canonical `CommandHost.ExitDecision`/`exitCode` across all hosts + `Cli.fs`, deleting the seven+one re-declared DUs and their `.fsi` entries; update each host's surface baseline. If adoption proves to cause disproportionate `.fsi` churn or cross-module naming friction during implementation, fall back to **deleting** the dead canonical (zero call sites today) and leaving hosts as-is — either way the "dead duplicate" state is resolved.

**Evidence**: `CommandHost.ExitDecision`/`exitCode` has **zero** call sites (grep-confirmed); each host + `Cli.fs` declares an identical DU **in its `.fsi`** (Ship `Loop.fs:88/166`, Verify `:92/190`, Route `:74/143`, Evidence `:36/289`, Release `:53/115`, Cache `:58/117`, Cli `:48/347`). The `.fsproj` essay still advertises the canonical as authoritative.

**Rationale/Tier**: This is the item with the most `.fsi`/baseline churn — hence isolated in Phase C and last. Removing per-host `ExitDecision` from `.fsi` is a Tier-1 surface change; the API-compat gate will (correctly) report the delta, and surface baselines are updated in the same commit. Because the DUs are identical, adoption is behavior-preserving.

## D6 — EvidenceCommand ArtifactReading dedup (Tier-1, biggest LOC win)

**Decision**: Widen `Cli/ArtifactReading.fsi` minimally to cover EvidenceCommand's needs, rewire Evidence to call it, and delete the ~325-line copy (`EvidenceCommand/Interpreter.fs:33-357`).

**Evidence**: `.fsproj` already `ProjectReference`s Cli and the file `open`s `FS.GG.Governance.Cli`. The copy has **already diverged** — a dead `"present"` check exists only in the Cli original (`ArtifactReading.fs:257-259`), absent from the Evidence copy — proving the drift the review warns about. Current `ArtifactReading.fsi` exposes only `optionsFor: RunRequest -> …`, `readArtifact`, `loadSnapshot: RunRequest -> …`; Evidence's local shapes are `optionsFor ()` (unit) and `loadSnapshot (rawRoot: string)`.

**Options**:
- **(A, chosen)** Add the small number of additional `val`s / a `RunRequest`-shaped entry that Evidence needs, so Evidence composes Cli's functions instead of copying their internals. Keep the widening minimal (only what Evidence consumes).
- (B) Move the whole reader into a new shared project — rejected: heavier, and Cli is already the reference reader.

**Rationale**: single source for artifact reading; ~325 lines deleted; the divergence (dead `"present"` check) is resolved by construction. Tier-1: `ArtifactReading.fsi` grows deliberately and its baseline updates.

**Watch-out**: verify the `"present"` semantics — the Cli original's check is described as *dead* (`_ -> ()` both arms), so adopting Cli's behavior is safe; confirm during implementation that no Evidence test depends on the (absent) copy's behavior.

## D7 — F15 `Wrote(Ok)` micro-drift

**Decision**: Align Ship to Verify's form: bind the post-update model first, then pass it to `emitEffect`.

**Evidence**: Ship `Loop.fs:739-742` returns `{ model with Phase = Persisted }` but passes the **pre-update** `model` (still `Phase = Rolled`) to `emitEffect`; Verify `:830-834` rebinds `model` first so `emitEffect` sees `Persisted`. Latent iff `emitEffect`/`renderText` reads `model.Phase`. Converge to one form (Verify's is the intended one) so the two hosts can't drift and any Phase-dependent rendering is consistent.

**Rationale**: tiny, behavior-aligning; add a test asserting the emitted summary reflects the post-update phase.

## D8 — F13 Evidence Done-inertness guard

**Decision**: Add `if model.Phase = Done then model, []` as the first line of EvidenceCommand's `update`, matching Route/Ship/Verify.

**Evidence**: Route `:414`, Ship `:635`, Verify `:695` all short-circuit once `Phase = Done`; Evidence `update` (`Loop.fs:188`) has no such prelude, so a duplicate `Wrote`/`Reported`/`Emitted` after `Done` still mutates Phase and re-schedules effects. `Done` exists in Evidence's Phase DU but is never used as a guard.

**Rationale**: makes post-decision messages provably inert (Principle VI, FR-013 as siblings document it); RED→GREEN test: feed a post-`Done` Msg, assert `model, []`.

## Cross-cutting decisions

- **Phasing** (A: Tier-2 leaves+fixes → B: ArtifactReading → C: ExitDecision+F9): isolates surface risk and lets a time-box stop after A/B with real value banked. See plan.md Structure Decision.
- **Evidence discipline**: every behavior fix (D2, D4, D7, D8) gets a RED→GREEN test through the real parsed/host surface; consolidations (D1, D3, D5, D6) get single-definition + unchanged-output assertions plus the full suite as the net.
- **No new dependency, no new CI check.** SurfaceDrift + deterministic + API-compat gates are the guardrails; the API-compat gate is expected to flag the deliberate D5/D6 surface deltas.
