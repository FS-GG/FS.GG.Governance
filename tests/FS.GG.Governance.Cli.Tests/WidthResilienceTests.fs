module FS.GG.Governance.Cli.Tests.WidthResilienceTests

open System
open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanText.ReportView
open FS.GG.Governance.HumanRender
open FS.GG.Governance.Cli.Tests.RenderSupport

// T029 [US2]: rich render at a range of widths (incl. very narrow) reflows/truncates cleanly without
// throwing or corrupting layout; the safe default is used when width is unknown (FR-006, SC-004).
//
// Spec 091 [US3]: the per-line assertion encodes the renderer's REAL folding contract. Spectre folds
// on word/segment boundaries and never mid-token, so when the forced width is narrower than the
// longest unbreakable token a wrapped line MAY legitimately extend to that token's boundary (plus the
// fixed table chrome the line carries). The bound is `max(width, longestUnbreakableToken + chrome)`:
//  - at fit-widths (200/80/40) it collapses to `width` — identical to the original strict check;
//  - at narrow widths (10/20) it tolerates the token boundary while still rejecting runaway/corrupted
//    layout (a line longer than any token could justify) (FR-003, FR-007; contract C1).

/// Every textual leaf the renderer receives, walked from the SAME `ReportView` the rich table renders
/// (title, section labels/details, group titles, exit status) plus the plain degrade projection. The
/// longest unbreakable token is DERIVED from these — never hardcoded (spec 091, T006).
let private viewStrings (view: ReportView) : string list =
    let rec ofNode node =
        match node with
        | Leaf(label, detail) ->
            match detail with
            | Some d -> [ label; d ]
            | None -> [ label ]
        | Group(title, children) -> title :: List.collect ofNode children

    [ view.Title; view.ExitStatus ]
    @ List.collect ofNode view.Sections

let private tokensOf (s: string) =
    s.Split([| ' '; '\t'; '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)

/// The longest whitespace-delimited (i.e. unbreakable) token across everything the renderer is fed.
let private longestUnbreakableToken =
    (blockedPlain :: viewStrings blockedView)
    |> List.collect (tokensOf >> List.ofArray)
    |> List.map String.length
    |> List.max

// Fixed columns a wrapped table cell line carries beyond its content, for the 2-column Rounded table
// in `RichRender.emitRich`. Under-counting tightens the bound and reintroduces the spurious overflow;
// over-counting masks genuine runaway layout (spec 091, T006).
let private borderColumns = 3 // Rounded vertical rules for 2 columns: │ … │ … │
let private paddingColumns = 4 // default cell padding (1 left + 1 right) × 2 columns
let private indentColumns = 0 // the table is not nested in a panel / indented block

let private chromeColumns = borderColumns + paddingColumns + indentColumns

[<Tests>]
let tests =
    testList
        "WidthResilience"
        [ for width in [ 200; 80; 40; 20; 10 ] do
              yield
                  test (sprintf "rich render at width %d produces output without throwing" width) {
                      let console, sw = plainConsole width
                      RichRender.emit RenderMode.Rich blockedView blockedPlain console
                      let out = sw.ToString()
                      Expect.isGreaterThan out.Length 0 "render produced output"
                      // Each emitted line fits the folding contract: at a forced width narrower than the
                      // longest unbreakable token, a line MAY reach that token's boundary (plus chrome);
                      // anything beyond it is runaway/corrupted layout and MUST fail (contract C1).
                      let bound = max width (longestUnbreakableToken + chromeColumns)

                      for line in out.Replace("\r\n", "\n").Split('\n') do
                          Expect.isLessThanOrEqual
                              line.Length
                              bound
                              (sprintf "line within folding bound %d (forced width %d)" bound width)
                  }

          yield
              test "the safe default width is a sane positive value" {
                  Expect.isGreaterThan RichRender.defaultWidth 0 "default width is positive"
              } ]
