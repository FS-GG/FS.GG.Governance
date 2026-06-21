# Golden Enforcement Truth Table

<!-- GENERATED — do not edit by hand; regenerate with `BLESS_FIXTURES=1 dotnet test tests/FS.GG.Governance.EnforcementFixtures.Tests`. -->

Every value below comes verbatim from the merged enforcement cores (F023 `deriveEffectiveSeverity`,
F015 `Routing.route`, F017 `findUnknownGovernedPaths`). This file is a coverage/evidence artifact —
it computes no new semantics. A byte-equality drift guard regenerates it from the live cores.

## Primary cross-product (base severity × maturity × run mode × profile)

| base | maturity | mode | profile | effective | reason |
|---|---|---|---|---|---|
| advisory | observe | sandbox | light | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | sandbox | standard | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | sandbox | strict | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | sandbox | release | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | inner | light | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | inner | standard | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | inner | strict | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | inner | release | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | focused | light | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | focused | standard | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | focused | strict | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | focused | release | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | verify | light | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | verify | standard | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | verify | strict | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | verify | release | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | gate | light | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | gate | standard | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | gate | strict | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | gate | release | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | release | light | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | release | standard | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | release | strict | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | observe | release | release | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| advisory | warn | sandbox | light | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | sandbox | standard | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | sandbox | strict | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | sandbox | release | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | inner | light | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | inner | standard | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | inner | strict | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | inner | release | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | focused | light | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | focused | standard | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | focused | strict | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | focused | release | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | verify | light | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | verify | standard | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | verify | strict | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | verify | release | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | gate | light | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | gate | standard | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | gate | strict | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | gate | release | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | release | light | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | release | standard | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | release | strict | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | warn | release | release | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| advisory | block-on-pr | sandbox | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | sandbox | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | sandbox | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | sandbox | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | inner | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | inner | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | inner | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | inner | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | focused | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | focused | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | focused | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | focused | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | verify | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | verify | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | verify | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | verify | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | gate | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | gate | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | gate | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | gate | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | release | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | release | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | release | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-pr | release | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | sandbox | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | sandbox | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | sandbox | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | sandbox | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | inner | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | inner | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | inner | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | inner | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | focused | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | focused | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | focused | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | focused | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | verify | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | verify | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | verify | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | verify | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | gate | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | gate | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | gate | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | gate | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | release | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | release | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | release | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-ship | release | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | sandbox | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | sandbox | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | sandbox | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | sandbox | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | inner | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | inner | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | inner | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | inner | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | focused | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | focused | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | focused | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | focused | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | verify | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | verify | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | verify | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | verify | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | gate | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | gate | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | gate | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | gate | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | release | light | advisory | base severity is advisory; 'light' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | release | standard | advisory | base severity is advisory; 'standard' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | release | strict | advisory | base severity is advisory; 'strict' profile does not escalate it (per-class strictness dials deferred) |
| advisory | block-on-release | release | release | advisory | base severity is advisory; 'release' profile does not escalate it (per-class strictness dials deferred) |
| blocking | observe | sandbox | light | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | sandbox | standard | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | sandbox | strict | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | sandbox | release | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | inner | light | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | inner | standard | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | inner | strict | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | inner | release | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | focused | light | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | focused | standard | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | focused | strict | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | focused | release | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | verify | light | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | verify | standard | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | verify | strict | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | verify | release | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | gate | light | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | gate | standard | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | gate | strict | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | gate | release | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | release | light | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | release | standard | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | release | strict | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | observe | release | release | advisory | maturity 'observe' withholds blocking; no run mode or profile can make it block |
| blocking | warn | sandbox | light | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | sandbox | standard | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | sandbox | strict | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | sandbox | release | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | inner | light | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | inner | standard | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | inner | strict | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | inner | release | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | focused | light | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | focused | standard | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | focused | strict | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | focused | release | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | verify | light | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | verify | standard | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | verify | strict | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | verify | release | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | gate | light | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | gate | standard | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | gate | strict | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | gate | release | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | release | light | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | release | standard | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | release | strict | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | warn | release | release | advisory | maturity 'warn' withholds blocking; no run mode or profile can make it block |
| blocking | block-on-pr | sandbox | light | advisory | 'light' profile does not block this 'block-on-pr' finding outside the 'gate' boundary (run mode 'sandbox') |
| blocking | block-on-pr | sandbox | standard | advisory | 'standard' profile does not block this 'block-on-pr' finding outside the 'gate' boundary (run mode 'sandbox') |
| blocking | block-on-pr | sandbox | strict | advisory | 'strict' profile does not block this 'block-on-pr' finding outside the 'verify' boundary (run mode 'sandbox') |
| blocking | block-on-pr | sandbox | release | advisory | 'release' profile does not block this 'block-on-pr' finding outside the 'focused' boundary (run mode 'sandbox') |
| blocking | block-on-pr | inner | light | advisory | 'light' profile does not block this 'block-on-pr' finding outside the 'gate' boundary (run mode 'inner') |
| blocking | block-on-pr | inner | standard | advisory | 'standard' profile does not block this 'block-on-pr' finding outside the 'gate' boundary (run mode 'inner') |
| blocking | block-on-pr | inner | strict | advisory | 'strict' profile does not block this 'block-on-pr' finding outside the 'verify' boundary (run mode 'inner') |
| blocking | block-on-pr | inner | release | advisory | 'release' profile does not block this 'block-on-pr' finding outside the 'focused' boundary (run mode 'inner') |
| blocking | block-on-pr | focused | light | advisory | 'light' profile does not block this 'block-on-pr' finding outside the 'gate' boundary (run mode 'focused') |
| blocking | block-on-pr | focused | standard | advisory | 'standard' profile does not block this 'block-on-pr' finding outside the 'gate' boundary (run mode 'focused') |
| blocking | block-on-pr | focused | strict | advisory | 'strict' profile does not block this 'block-on-pr' finding outside the 'verify' boundary (run mode 'focused') |
| blocking | block-on-pr | focused | release | blocking | run mode 'focused' reaches the 'focused' blocking boundary for maturity 'block-on-pr' under 'release' profile |
| blocking | block-on-pr | verify | light | advisory | 'light' profile does not block this 'block-on-pr' finding outside the 'gate' boundary (run mode 'verify') |
| blocking | block-on-pr | verify | standard | advisory | 'standard' profile does not block this 'block-on-pr' finding outside the 'gate' boundary (run mode 'verify') |
| blocking | block-on-pr | verify | strict | blocking | run mode 'verify' reaches the 'verify' blocking boundary for maturity 'block-on-pr' under 'strict' profile |
| blocking | block-on-pr | verify | release | blocking | run mode 'verify' reaches the 'focused' blocking boundary for maturity 'block-on-pr' under 'release' profile |
| blocking | block-on-pr | gate | light | blocking | run mode 'gate' reaches the 'gate' blocking boundary for maturity 'block-on-pr' under 'light' profile |
| blocking | block-on-pr | gate | standard | blocking | run mode 'gate' reaches the 'gate' blocking boundary for maturity 'block-on-pr' under 'standard' profile |
| blocking | block-on-pr | gate | strict | blocking | run mode 'gate' reaches the 'verify' blocking boundary for maturity 'block-on-pr' under 'strict' profile |
| blocking | block-on-pr | gate | release | blocking | run mode 'gate' reaches the 'focused' blocking boundary for maturity 'block-on-pr' under 'release' profile |
| blocking | block-on-pr | release | light | blocking | run mode 'release' reaches the 'gate' blocking boundary for maturity 'block-on-pr' under 'light' profile |
| blocking | block-on-pr | release | standard | blocking | run mode 'release' reaches the 'gate' blocking boundary for maturity 'block-on-pr' under 'standard' profile |
| blocking | block-on-pr | release | strict | blocking | run mode 'release' reaches the 'verify' blocking boundary for maturity 'block-on-pr' under 'strict' profile |
| blocking | block-on-pr | release | release | blocking | run mode 'release' reaches the 'focused' blocking boundary for maturity 'block-on-pr' under 'release' profile |
| blocking | block-on-ship | sandbox | light | advisory | 'light' profile does not block this 'block-on-ship' finding outside the 'gate' boundary (run mode 'sandbox') |
| blocking | block-on-ship | sandbox | standard | advisory | 'standard' profile does not block this 'block-on-ship' finding outside the 'gate' boundary (run mode 'sandbox') |
| blocking | block-on-ship | sandbox | strict | advisory | 'strict' profile does not block this 'block-on-ship' finding outside the 'verify' boundary (run mode 'sandbox') |
| blocking | block-on-ship | sandbox | release | advisory | 'release' profile does not block this 'block-on-ship' finding outside the 'focused' boundary (run mode 'sandbox') |
| blocking | block-on-ship | inner | light | advisory | 'light' profile does not block this 'block-on-ship' finding outside the 'gate' boundary (run mode 'inner') |
| blocking | block-on-ship | inner | standard | advisory | 'standard' profile does not block this 'block-on-ship' finding outside the 'gate' boundary (run mode 'inner') |
| blocking | block-on-ship | inner | strict | advisory | 'strict' profile does not block this 'block-on-ship' finding outside the 'verify' boundary (run mode 'inner') |
| blocking | block-on-ship | inner | release | advisory | 'release' profile does not block this 'block-on-ship' finding outside the 'focused' boundary (run mode 'inner') |
| blocking | block-on-ship | focused | light | advisory | 'light' profile does not block this 'block-on-ship' finding outside the 'gate' boundary (run mode 'focused') |
| blocking | block-on-ship | focused | standard | advisory | 'standard' profile does not block this 'block-on-ship' finding outside the 'gate' boundary (run mode 'focused') |
| blocking | block-on-ship | focused | strict | advisory | 'strict' profile does not block this 'block-on-ship' finding outside the 'verify' boundary (run mode 'focused') |
| blocking | block-on-ship | focused | release | blocking | run mode 'focused' reaches the 'focused' blocking boundary for maturity 'block-on-ship' under 'release' profile |
| blocking | block-on-ship | verify | light | advisory | 'light' profile does not block this 'block-on-ship' finding outside the 'gate' boundary (run mode 'verify') |
| blocking | block-on-ship | verify | standard | advisory | 'standard' profile does not block this 'block-on-ship' finding outside the 'gate' boundary (run mode 'verify') |
| blocking | block-on-ship | verify | strict | blocking | run mode 'verify' reaches the 'verify' blocking boundary for maturity 'block-on-ship' under 'strict' profile |
| blocking | block-on-ship | verify | release | blocking | run mode 'verify' reaches the 'focused' blocking boundary for maturity 'block-on-ship' under 'release' profile |
| blocking | block-on-ship | gate | light | blocking | run mode 'gate' reaches the 'gate' blocking boundary for maturity 'block-on-ship' under 'light' profile |
| blocking | block-on-ship | gate | standard | blocking | run mode 'gate' reaches the 'gate' blocking boundary for maturity 'block-on-ship' under 'standard' profile |
| blocking | block-on-ship | gate | strict | blocking | run mode 'gate' reaches the 'verify' blocking boundary for maturity 'block-on-ship' under 'strict' profile |
| blocking | block-on-ship | gate | release | blocking | run mode 'gate' reaches the 'focused' blocking boundary for maturity 'block-on-ship' under 'release' profile |
| blocking | block-on-ship | release | light | blocking | run mode 'release' reaches the 'gate' blocking boundary for maturity 'block-on-ship' under 'light' profile |
| blocking | block-on-ship | release | standard | blocking | run mode 'release' reaches the 'gate' blocking boundary for maturity 'block-on-ship' under 'standard' profile |
| blocking | block-on-ship | release | strict | blocking | run mode 'release' reaches the 'verify' blocking boundary for maturity 'block-on-ship' under 'strict' profile |
| blocking | block-on-ship | release | release | blocking | run mode 'release' reaches the 'focused' blocking boundary for maturity 'block-on-ship' under 'release' profile |
| blocking | block-on-release | sandbox | light | advisory | 'light' profile does not block this 'block-on-release' finding outside the 'release' boundary (run mode 'sandbox') |
| blocking | block-on-release | sandbox | standard | advisory | 'standard' profile does not block this 'block-on-release' finding outside the 'release' boundary (run mode 'sandbox') |
| blocking | block-on-release | sandbox | strict | advisory | 'strict' profile does not block this 'block-on-release' finding outside the 'gate' boundary (run mode 'sandbox') |
| blocking | block-on-release | sandbox | release | advisory | 'release' profile does not block this 'block-on-release' finding outside the 'verify' boundary (run mode 'sandbox') |
| blocking | block-on-release | inner | light | advisory | 'light' profile does not block this 'block-on-release' finding outside the 'release' boundary (run mode 'inner') |
| blocking | block-on-release | inner | standard | advisory | 'standard' profile does not block this 'block-on-release' finding outside the 'release' boundary (run mode 'inner') |
| blocking | block-on-release | inner | strict | advisory | 'strict' profile does not block this 'block-on-release' finding outside the 'gate' boundary (run mode 'inner') |
| blocking | block-on-release | inner | release | advisory | 'release' profile does not block this 'block-on-release' finding outside the 'verify' boundary (run mode 'inner') |
| blocking | block-on-release | focused | light | advisory | 'light' profile does not block this 'block-on-release' finding outside the 'release' boundary (run mode 'focused') |
| blocking | block-on-release | focused | standard | advisory | 'standard' profile does not block this 'block-on-release' finding outside the 'release' boundary (run mode 'focused') |
| blocking | block-on-release | focused | strict | advisory | 'strict' profile does not block this 'block-on-release' finding outside the 'gate' boundary (run mode 'focused') |
| blocking | block-on-release | focused | release | advisory | 'release' profile does not block this 'block-on-release' finding outside the 'verify' boundary (run mode 'focused') |
| blocking | block-on-release | verify | light | advisory | 'light' profile does not block this 'block-on-release' finding outside the 'release' boundary (run mode 'verify') |
| blocking | block-on-release | verify | standard | advisory | 'standard' profile does not block this 'block-on-release' finding outside the 'release' boundary (run mode 'verify') |
| blocking | block-on-release | verify | strict | advisory | 'strict' profile does not block this 'block-on-release' finding outside the 'gate' boundary (run mode 'verify') |
| blocking | block-on-release | verify | release | blocking | run mode 'verify' reaches the 'verify' blocking boundary for maturity 'block-on-release' under 'release' profile |
| blocking | block-on-release | gate | light | advisory | 'light' profile does not block this 'block-on-release' finding outside the 'release' boundary (run mode 'gate') |
| blocking | block-on-release | gate | standard | advisory | 'standard' profile does not block this 'block-on-release' finding outside the 'release' boundary (run mode 'gate') |
| blocking | block-on-release | gate | strict | blocking | run mode 'gate' reaches the 'gate' blocking boundary for maturity 'block-on-release' under 'strict' profile |
| blocking | block-on-release | gate | release | blocking | run mode 'gate' reaches the 'verify' blocking boundary for maturity 'block-on-release' under 'release' profile |
| blocking | block-on-release | release | light | blocking | run mode 'release' reaches the 'release' blocking boundary for maturity 'block-on-release' under 'light' profile |
| blocking | block-on-release | release | standard | blocking | run mode 'release' reaches the 'release' blocking boundary for maturity 'block-on-release' under 'standard' profile |
| blocking | block-on-release | release | strict | blocking | run mode 'release' reaches the 'gate' blocking boundary for maturity 'block-on-release' under 'strict' profile |
| blocking | block-on-release | release | release | blocking | run mode 'release' reaches the 'verify' blocking boundary for maturity 'block-on-release' under 'release' profile |

## Route classes (routine vs fenced vs unknown governed path)

| class | example path | route outcome | finding | note |
|---|---|---|---|---|
| routine | docs/readme.md | out-of-scope | (none) | selects nothing; never default-denies, even under the strictest dials |
| fenced | src/build/Main.fs | routed:build | (none) | routes into the domain's gates |
| unknown-governed-path | src/new/Thing.fs | unmatched-in-root | unknownGovernedPath | explicit finding; never a silent default-deny |
| protected-surface-unknown | src/boundary/Api.fs | unmatched-in-root | unknownProtectedBoundaryPath | escalated finding on a declared protected boundary |
