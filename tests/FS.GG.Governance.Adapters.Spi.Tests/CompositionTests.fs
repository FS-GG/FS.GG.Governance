module FS.GG.Governance.Adapters.Spi.Tests.CompositionTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.Spi.Tests.ExampleAdapters

// US3/US4/US5 semantic tests, exercised THROUGH the built FS.GG.Governance.Adapters.Spi
// + FS.GG.Governance.Kernel libraries (Principle I). Every test that drives the two
// synthetic example domains carries the `Synthetic` token.

let private factIdStr (FactId s) = s

let private liftedDoc = Composition.lift (|DocP|_|) narrowDoc docAdapter
let private liftedTask = Composition.lift (|TaskP|_|) narrowTask taskAdapter

/// All permutations of a list (index-based, value-agnostic).
let rec private permutations xs =
    match xs with
    | [] -> [ [] ]
    | _ ->
        xs
        |> List.mapi (fun i x ->
            let rest =
                xs |> List.mapi (fun j y -> (j, y)) |> List.filter (fun (j, _) -> j <> i) |> List.map snd

            permutations rest |> List.map (fun p -> x :: p))
        |> List.concat

let private factKey (fs: FactSet<ProjectFact>) =
    fs |> List.map (fun f -> factIdStr f.Id, f.Value, f.Provenance) |> List.sortBy (fun (i, _, _) -> i)

let private routeKey (r: Route) =
    r.Stakes, (r.Blocking |> List.sortBy (fun e -> e.Id)), (r.Advisory |> List.sortBy (fun e -> e.Id))

[<Tests>]
let tests =
    testList
        "Composition"
        [
          // ── V66 (US3): the composed catalog runs through the UNCHANGED kernel (C1) ──
          test "V66 composed catalog evaluates via unchanged CheckRule.toRule + FixedPoint — Synthetic" {
              let composed = Composition.compose [ liftedDoc; liftedTask ] crossDomainRules

              // Composition.toRules is exactly `Catalog |> List.map (CheckRule.toRule bridge)`
              // — the kernel gains NO adapter-specific code (dependency direction adapters
              // -> kernel). The executable rules ARE the kernel's.
              let projRules = Composition.toRules projBridge composed
              Expect.equal projRules.Length composed.Catalog.Length "one executable rule per catalog entry"

              let supplied: FactSet<ProjectFact> =
                  [ { Id = FactId "d"; Value = Doc(HasTitle true); Provenance = [] }
                    { Id = FactId "t"; Value = Task(TaskClosed true); Provenance = [] } ]

              let result = FixedPoint.evaluate projIdentify projRules supplied
              let outcomes = result.Facts |> List.choose (fun f -> projBridge.Project f.Value)

              Expect.contains outcomes (Decided(RuleId "doc-titled", Pass)) "doc rule decided through the kernel"
              Expect.contains outcomes (Decided(RuleId "task-closed", Pass)) "task rule decided through the kernel"
          }

          // ── V67 (US3): a cross-domain Implies couples two domains; blocking wins (C3/C4) ──
          test "V67 cross-domain Implies couples two domains and a blocking result wins under F07 — Synthetic" {
              let composed = Composition.compose [ liftedDoc; liftedTask ] crossDomainRules

              // The single cross-domain rule (Deterministic, Blocking) couples doc -> task.
              // Eval: doc titled AND task governed ⇒ Pass.
              let supplied: FactSet<ProjectFact> =
                  [ { Id = FactId "d"; Value = Doc(HasTitle true); Provenance = [] }
                    { Id = FactId "t"; Value = Task(TaskClosed true); Provenance = [] } ]

              let result = FixedPoint.evaluate projIdentify (Composition.toRules projBridge composed) supplied
              let outcomes = result.Facts |> List.choose (fun f -> projBridge.Project f.Value)
              Expect.contains outcomes (Decided(RuleId "doc-implies-task-gov", Pass)) "cross-domain rule decides Pass when coupled"

              // F07 precedence: a fenced change at Gate makes the Blocking cross-domain rule
              // a blocking gate — a blocking result wins regardless of catalog position.
              let change = { Docs = set [ "doc.md" ]; Tasks = set [] }
              let route = Route.route composed.Fences composed.Catalog Gate change
              Expect.equal route.Stakes (Fenced "doc-body") "the change is fenced"

              Expect.isTrue
                  (route.Blocking |> List.exists (fun e -> e.Id = RuleId "doc-implies-task-gov"))
                  "the blocking cross-domain rule is a blocking gate"
          }

          // ── V68 (US3): order-independence — every permutation ⇒ identical LFP + route ──
          testProperty "V68 composition order-independence over LFP and route — Synthetic"
          <| fun (docTitled: bool) (taskClosed: bool) ->
              let supplied: FactSet<ProjectFact> =
                  [ { Id = FactId "d"; Value = Doc(HasTitle docTitled); Provenance = [] }
                    { Id = FactId "t"; Value = Task(TaskClosed taskClosed); Provenance = [] } ]

              let change = { Docs = set [ "doc.md" ]; Tasks = set [ "T-1" ] }

              // Evaluate + route for a given adapter-composition order.
              let evalOrder (adapters: Lifted<ProjectFact, ProjectChange> list) =
                  let composed = Composition.compose adapters crossDomainRules
                  let result = FixedPoint.evaluate projIdentify (Composition.toRules projBridge composed) supplied
                  let route = Route.route composed.Fences composed.Catalog Gate change
                  factKey result.Facts, routeKey route

              let results = permutations [ liftedDoc; liftedTask ] |> List.map evalOrder

              // Every permutation yields a byte-for-byte identical least fixed point AND an
              // identical merged route (order-free combination — law C2/C3).
              match results with
              | [] -> true
              | reference :: rest -> rest |> List.forall (fun r -> r = reference)

          // ── V73 (US3): composition edge cases (FR-011, law C5; trivial; minimal) ──
          test "V73 composition edge cases — duplicate fences, single adapter, minimal adapter — Synthetic" {
              // (a) DUPLICATE FENCES: two contributions naming the same surface dedup to ONE
              // fence; the stakes over that surface carry the single name (not double-counted).
              let fenceA: Fence<ProjectChange> = { Name = "shared"; Trips = fun c -> c.Docs.Contains "x" }
              let fenceB: Fence<ProjectChange> = { Name = "shared"; Trips = fun c -> c.Tasks.Contains "y" }
              let l1: Lifted<ProjectFact, ProjectChange> = { Rules = []; Fences = [ fenceA ] }
              let l2: Lifted<ProjectFact, ProjectChange> = { Rules = []; Fences = [ fenceB ] }
              let composedDup = Composition.compose [ l1; l2 ] []
              Expect.equal composedDup.Fences.Length 1 "duplicate fences dedup by name to one"

              let route = Route.route composedDup.Fences [] Gate { Docs = set [ "x" ]; Tasks = set [] }
              Expect.equal route.Stakes (Fenced "shared") "stakes carry the single deduped surface name"

              // (b) SINGLE ADAPTER / trivial coproduct: compose [one] [] governs identically
              // to that adapter standalone (composition adds no behaviour).
              let supplied: FactSet<DocFact> = [ { Id = FactId "f"; Value = HasTitle true; Provenance = [] } ]

              let stdOutcomes =
                  (FixedPoint.evaluate docAdapter.Identify (Adapter.toRules docAdapter) supplied).Facts
                  |> List.choose (fun f -> docBridge.Project f.Value)
                  |> List.sort

              let composedOne = Composition.compose [ liftedDoc ] []
              let bigSupplied: FactSet<ProjectFact> = [ { Id = FactId "f"; Value = Doc(HasTitle true); Provenance = [] } ]

              let oneOutcomes =
                  (FixedPoint.evaluate projIdentify (Composition.toRules projBridge composedOne) bigSupplied).Facts
                  |> List.choose (fun f -> projBridge.Project f.Value)
                  |> List.sort

              Expect.equal oneOutcomes stdOutcomes "compose [one] [] governs identically to standalone"

              // (c) MINIMAL ADAPTER: empty Rules/Fences composes without error.
              let emptyAdapter: Adapter<DocFact, DocArtifact, DocChange> =
                  { docAdapter with Rules = []; Fences = [] }

              let composedEmpty = Composition.compose [ Composition.lift (|DocP|_|) narrowDoc emptyAdapter ] []
              Expect.isEmpty composedEmpty.Catalog "minimal adapter contributes no rules"
              Expect.isEmpty composedEmpty.Fences "minimal adapter contributes no fences"
          }

          // ── V69 (US4): removing one adapter leaves the rest intact; cross rule goes inert ──
          test "V69 removal — drop one adapter; remaining intact; cross-domain rule inert — Synthetic" {
              let full = Composition.compose [ liftedDoc; liftedTask ] crossDomainRules
              let withoutDoc = Composition.compose [ liftedTask ] crossDomainRules

              // Dropping the Doc `Lifted` removes exactly its rules; nothing else references it.
              Expect.isLessThan withoutDoc.Catalog.Length full.Catalog.Length "dropping Doc removes its rules"

              Expect.isFalse
                  (withoutDoc.Catalog |> List.exists (fun r -> r.Id = RuleId "doc-titled"))
                  "the removed domain's rules are gone"

              // The remaining (task) adapter evaluates unchanged, and the cross-domain rule
              // whose ANTECEDENT domain (Doc) is gone goes INERT — its antecedent probe
              // reports Unmet, so the Implies is vacuously satisfied (Decided Pass), NOT an
              // error and NOT a silent fail (law R2/R3, FR-009, SC-004).
              let supplied: FactSet<ProjectFact> = [ { Id = FactId "t"; Value = Task(TaskClosed true); Provenance = [] } ]
              let result = FixedPoint.evaluate projIdentify (Composition.toRules projBridge withoutDoc) supplied
              let outcomes = result.Facts |> List.choose (fun f -> projBridge.Project f.Value)

              Expect.contains outcomes (Decided(RuleId "task-closed", Pass)) "the surviving adapter governs unchanged"
              Expect.contains outcomes (Decided(RuleId "doc-implies-task-gov", Pass)) "cross-domain rule is inert (vacuous Pass), not an error"
          }

          // ── V70 (US5): two UNRELATED domains each govern themselves; zero cross-copying ──
          test "V70 two unrelated domains each govern themselves with their own vocabulary — Synthetic" {
              // Structural review note (FR-010, SC-005): `DocFact` and `TaskFact` are DISTINCT
              // closed unions with distinct artifacts/probes/fences. The type system makes
              // cross-copying impossible — neither adapter can name the other's facts. The
              // "zero cross-copying" claim is therefore this type fact, verified by each
              // domain governing itself below with no reference to the other.
              let docResult =
                  FixedPoint.evaluate
                      docAdapter.Identify
                      (Adapter.toRules docAdapter)
                      [ { Id = FactId "d"; Value = HasTitle true; Provenance = [] } ]

              Expect.isTrue
                  (docResult.Facts |> List.exists (fun f -> f.Value = DocGov(Decided(RuleId "doc-titled", Pass))))
                  "the document domain governs itself"

              let taskResult =
                  FixedPoint.evaluate
                      taskAdapter.Identify
                      (Adapter.toRules taskAdapter)
                      [ { Id = FactId "t"; Value = TaskClosed true; Provenance = [] } ]

              Expect.isTrue
                  (taskResult.Facts |> List.exists (fun f -> f.Value = TaskGov(Decided(RuleId "task-closed", Pass))))
                  "the unrelated task domain governs itself"
          }

          // ── V71 (US5): the two unrelated domains compose without either being reshaped ──
          test "V71 two unrelated domains compose at one root without reshaping — Synthetic" {
              // No cross-domain rule: each domain composes carrying ONLY its own lifted
              // vocabulary (each lift re-targets the fact channel; nothing is reshaped).
              let composed = Composition.compose [ liftedDoc; liftedTask ] []

              let supplied: FactSet<ProjectFact> =
                  [ { Id = FactId "d"; Value = Doc(HasTitle true); Provenance = [] }
                    { Id = FactId "t"; Value = Task(TaskClosed true); Provenance = [] } ]

              let result = FixedPoint.evaluate projIdentify (Composition.toRules projBridge composed) supplied
              let outcomes = result.Facts |> List.choose (fun f -> projBridge.Project f.Value)

              Expect.contains outcomes (Decided(RuleId "doc-titled", Pass)) "the document rule fires in the composed root"
              Expect.contains outcomes (Decided(RuleId "task-closed", Pass)) "the task rule fires in the composed root"
          } ]
