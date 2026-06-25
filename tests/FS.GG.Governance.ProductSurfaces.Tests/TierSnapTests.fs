module FS.GG.Governance.ProductSurfaces.Tests.TierSnapTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.ProductSurfaces
open FS.GG.Governance.ProductSurfaces.Model
open FS.GG.Governance.ProductSurfaces.Tests.Support

// US2 — snap-to-declared (FR-016) + cheaper-local alternative (FR-007). Among the winning domain's
// declared tiers: the deepest not exceeding the target; if none ≤ target, the cheapest declared; if no
// tiered check is declared, SelectedTier = target with TierIsDeclared = false.

let private facts = factsOf "product-surface-all-kinds"
let private report = classifyPaths "product-surface-all-kinds" "standard" [ "src/Api.fsi"; "docs/guide.md"; "release/notes.md" ]

let private get path =
    match forPath report path with
    | Some c -> c
    | None -> failtestf "expected a classification for '%s'" path

[<Tests>]
let tests =
    testList
        "ProductSurfaces.TierSnap.US2"
        [ test "deepest declared tier ≤ target is selected, TierIsDeclared = true" {
              // package-api declares scan/build/test (structuralScan/restoreBuild/focusedTests); target is
              // FocusedTests; the deepest declared ≤ target is FocusedTests.
              let c = get "src/Api.fsi"
              Expect.equal c.SelectedTier FocusedTests "deepest declared ≤ target"
              Expect.isTrue c.TierIsDeclared "tier is declared"
          }

          test "no tiered check declared ⇒ SelectedTier = target, TierIsDeclared = false (FR-016)" {
              // The docs domain declares no tiered checks; the target (baseline StructuralScan) stands and
              // the F24-pending non-error note is set.
              let c = get "docs/guide.md"
              Expect.equal c.SelectedTier StructuralScan "target stands"
              Expect.isFalse c.TierIsDeclared "no declared tier ⇒ the F24-pending note"
          }

          test "none ≤ target ⇒ the cheapest declared tier is selected" {
              // Inject a single FocusedTests check into the `samples` domain whose baseline target is
              // StructuralScan: no declared tier is ≤ the target, so the cheapest declared (FocusedTests) is
              // selected — over the real route, not a mock.
              let extra =
                  { Id = CheckId "samples-deep"
                    Domain = DomainId "samples"
                    Command = None
                    Owner = Owner "platform"
                    Cost = High
                    Environment = Ci
                    Maturity = BlockOnShip
                    Tier = Some FocusedTests }

              let facts' =
                  { facts with
                      Capabilities = { facts.Capabilities with Checks = extra :: facts.Capabilities.Checks } }

              let rep = ProductSurfaces.classify facts' (Routing.route facts' [ normalizePath "samples/App/Program.fs" ]) (ProfileId "standard")
              let c = rep.Classifications |> List.find (fun c -> c.Path = normalizePath "samples/App/Program.fs")
              Expect.equal c.SelectedTier FocusedTests "cheapest declared when none ≤ target"
              Expect.isTrue c.TierIsDeclared "tier is declared"
          }

          test "CheaperLocalTier names the cheapest strictly-cheaper, locally-runnable declared tier" {
              // package-api: scan (structuralScan, local-or-ci) and build (restoreBuild, local-or-ci) are
              // both local and below the selected FocusedTests; the cheapest is StructuralScan.
              let c = get "src/Api.fsi"
              Expect.equal c.Alternative (CheaperLocalTier StructuralScan) "cheapest cheaper-local tier"
          }

          test "NoCheaperLocalTier when no declared tier is local and strictly cheaper" {
              // release domain: verify (ci) and relgate (release) are neither local.
              let c = get "release/notes.md"
              Expect.equal c.Alternative NoCheaperLocalTier "no locally-runnable cheaper tier"
          } ]
