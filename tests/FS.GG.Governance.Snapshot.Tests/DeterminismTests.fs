module FS.GG.Governance.Snapshot.Tests.DeterminismTests

open System
open Expecto
open FS.GG.Governance.Snapshot
open FS.GG.Governance.Snapshot.Model
open FS.GG.Governance.Snapshot.Tests.Support

// FR-009/FR-010, SC-002/SC-003: assemble is byte-stable for identical input, and permuting the
// order of the raw `-z` records never changes the resulting snapshot (every collection is sorted).

let private richRaw =
    { baseRaw with
        DiffRaw = Ok(znul [ "M"; "src/b.fs" ] + znul [ "A"; "src/a.fs" ] + znul [ "R100"; "old.fs"; "z.fs" ])
        StatusRaw = Ok(znul [ "?? u.fs" ] + znul [ "M  d.fs" ])
        BranchRaw = Ok "main"
        RawCi = Some { Environment = CiEnvironment.Ci; PrLabels = [ "b"; "a" ]; RequiredStatusChecks = [ "y"; "x" ] } }

/// Keep only generated strings that reduce to a safe, distinct path segment.
let private safeSegments (raw: string list) : string list =
    raw
    |> List.choose (fun s ->
        if String.IsNullOrEmpty s then
            None
        else
            let cleaned = String(s |> Seq.filter Char.IsLetterOrDigit |> Seq.toArray)
            if cleaned = "" then None else Some cleaned)
    |> List.distinct

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "assembling the same RawSensing twice yields a structurally identical snapshot (SC-002)" {
              Expect.equal (Snapshot.assemble richRaw) (Snapshot.assemble richRaw) "pure ⇒ identical"
          }

          testProperty "permuting diff records yields an identical snapshot (SC-003)"
          <| fun (raw: string list) ->
              let segs = safeSegments raw

              let diffOf (order: string list) =
                  order |> List.map (fun p -> znul [ "A"; "src/" + p + ".fs" ]) |> String.concat ""

              let asm order = Snapshot.assemble { baseRaw with DiffRaw = Ok(diffOf order) }
              asm segs = asm (List.rev segs)

          testProperty "permuting status records yields an identical snapshot (SC-003)"
          <| fun (raw: string list) ->
              let segs = safeSegments raw

              let statusOf (order: string list) =
                  order |> List.map (fun p -> znul [ "?? " + p + ".fs" ]) |> String.concat ""

              let asm order = Snapshot.assemble { baseRaw with StatusRaw = Ok(statusOf order) }
              asm segs = asm (List.rev segs) ]
