module FS.GG.Governance.ReferenceGateSet.Tests.ReferenceGateSetDerivationTests

// #386 / docs/decisions/0011 — THE DRIFT GATE.
//
// The decision made the embedded F# profile (`ReferenceProfile.checksFor`) the ONE authoritative
// source and the published `.fsgg/capabilities.yml` gameplay region a GENERATED artifact derived
// from it. A derivation nothing checks is exactly the convention the decision exists to end, so
// this is the check — and it FAILS CLOSED in every direction:
//
//   D1  the generated region's bytes equal the projection's bytes;
//   D2  the region, PARSED BACK by the real `Config.Loader`, yields `Check` records field-identical
//       to the authoritative table (so a wrong YAML token in the renderer cannot ship quietly);
//   D3  every bound profile has a region, and the file declares NO gameplay check the code does not
//       (so a second hand-authored authority cannot reappear beside the generated one);
//   D4  an absent, duplicated, or inverted marker pair is an ERROR, never a silent pass.
//
// The test list is named `ReferenceGateSetGuardDerivation` deliberately: `pack-reference-gate-set.fsx`
// runs `--filter FullyQualifiedName~ReferenceGateSetGuard` as its pre-pack gate, so a drifted
// derivation does not merely go red in CI — the pack REFUSES to produce the package at all.
//
// Real artifacts only (Principle V): the on-disk reference set and the real loader, no fixtures.

open System.IO
open Expecto
open FS.GG.Governance.Config
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Inheritance

/// The reference set under test. Default: the canonical samples dir. `pack-reference-gate-set.fsx`
/// redirects this to a `--source` temp copy through the same env var the G1–G7 guard honours, so a
/// candidate set is derivation-checked before it is packed, exactly as its invariants are.
let private referenceDir =
    match System.Environment.GetEnvironmentVariable "FSGG_REFERENCE_GATE_SET_DIR" with
    | null
    | "" -> Path.Combine(FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot, "samples", "sdd-reference-gate-set")
    | dir -> dir

let private capabilitiesPath = Path.Combine(referenceDir, ".fsgg", "capabilities.yml")

// #385: no test in this file names a single profile any more. D1, D2 and D3 all iterate
// `ReferenceProfile.boundProfiles`, so the set the derivation is GUARDED over and the set it is
// GENERATED from are one list, and a profile cannot be added to the org table while remaining
// outside the gate that checks it.

/// `BLESS_REFERENCE_GATE_SET=1` regenerates the region in place — the same deliberate, opt-in bless
/// idiom this repo already uses for the public-surface baselines (`BLESS_SURFACE=1`). It is not a
/// fallback: with the variable unset the gate only ever reports.
let private blessing =
    System.Environment.GetEnvironmentVariable "BLESS_REFERENCE_GATE_SET" = "1"

let private capabilitiesText () = File.ReadAllText capabilitiesPath

/// The authoritative checks for a profile, keyed by check id.
let private authoritativeById (profile: TemplateProfile) =
    ReferenceProfile.checksFor profile
    |> List.map (fun c ->
        let (CheckId id) = c.Id
        id, c)
    |> Map.ofList

/// The reference set's own `Check` records, loaded through the REAL config edge — the same
/// `Loader.loadAndValidate` an adopter and the CLI use. Parsing the generated file back is what
/// makes D2 evidence about the shipped artifact rather than a re-read of the renderer.
let private loadedChecks () : Check list =
    match Loader.loadAndValidate referenceDir with
    | Valid f -> f.Capabilities.Checks
    | Invalid diags -> failtestf "reference .fsgg did not load Valid: %A" diags

[<Tests>]
let derivationGuard =
    // Sequenced: under `BLESS_REFERENCE_GATE_SET=1`, D1 REWRITES the file the other three read.
    // Expecto parallelises a plain `testList`, so without this D2/D3 could read the file mid-rewrite
    // and fail (or pass) for reasons that have nothing to do with the derivation — observed exactly
    // once, on the first bless run.
    testSequenced
    <| testList
        "ReferenceGateSetGuardDerivation"
        [
          // ── D1 — the published region IS the projection, byte for byte, for EVERY bound profile ──
          // #385 gave the org profile a second bound profile (`fsharp-constitution`), and this test
          // used to name `game` in three places. It now iterates `boundProfiles`, so the set it
          // checks and the set D3 requires a region for are THE SAME LIST — a third profile is
          // covered by adding it to `boundProfiles`, with no second edit here that could be
          // forgotten. Bless likewise regenerates every bound region rather than only the first.
          //
          // What bless still cannot do, on purpose: CREATE a marker pair. `replaceRegion` fails
          // closed on an absent one, so a newly bound profile reds D1/D3 until a human places the
          // markers (and the surrounding hand-authored context) in `capabilities.yml`. That is the
          // right direction to fail — a generator that invented its own insertion point would be
          // choosing where an org gate set lands in a consumer's file.
          test "D1 the generated capabilities region equals the projection of the embedded profile" {
              if blessing then
                  for profile in ReferenceProfile.boundProfiles do
                      match ReferenceProfile.replaceRegion profile (capabilitiesText ()) with
                      | Ok rewritten -> File.WriteAllText(capabilitiesPath, rewritten)
                      | Error e ->
                          let (TemplateProfile name) = profile

                          failtestf
                              "BLESS_REFERENCE_GATE_SET=1 could not locate the region to rewrite for profile '%s': %s\nA newly bound profile needs its marker pair placed in %s by hand first — bless replaces a region, it never invents one."
                              name
                              e
                              capabilitiesPath

              // Re-read: under bless this asserts over what was just written, so a broken splice is
              // still caught rather than blessed away.
              for profile in ReferenceProfile.boundProfiles do
                  let (TemplateProfile name) = profile

                  match ReferenceProfile.extractRegion profile (capabilitiesText ()) with
                  | Error e ->
                      failtestf
                          "the generated region for profile '%s' could not be located in %s: %s\nThe published gate set must carry the marked, generated region — an unlocatable region is drift, not an exemption. Regenerate with BLESS_REFERENCE_GATE_SET=1 dotnet test tests/FS.GG.Governance.ReferenceGateSet.Tests"
                          name
                          capabilitiesPath
                          e
                  | Ok onDisk ->
                      Expect.equal
                          onDisk
                          (ReferenceProfile.capabilitiesRegion profile)
                          (sprintf
                              "the published capabilities region for profile '%s' drifted from the authoritative embedded F# profile (docs/decisions/0011, and docs/decisions/0012 for `fsharp-constitution`) — it is GENERATED; edit the authoritative table and regenerate with BLESS_REFERENCE_GATE_SET=1 dotnet test tests/FS.GG.Governance.ReferenceGateSet.Tests"
                              name)
          }

          // ── D2 — the round trip through the REAL loader agrees field for field ──
          // D1 compares the renderer against its own output, so on its own it could not catch a
          // renderer that emits a token `Config.Schema` parses differently (or not at all). This
          // one parses the SHIPPED file back and compares the resulting records to the authoritative
          // table, which is what makes the second token vocabulary in ReferenceProfile.fs safe to
          // have: a wrong token reds this test instead of publishing a quietly wrong profile.
          //
          // It iterates `boundProfiles` for the same reason D1 and the bless path do, and #385's
          // critic measured why it must: while this test bound `game` alone, mis-mapping the
          // `costToken` cases that ONLY the `fsharp-constitution` profile uses (`Cheap`/`Medium`)
          // left the whole suite green while shipping `cost: exhaustive` on three of four
          // `fsharp:*` gates. The control — mis-mapping `High`, a `game` token — did red. So the
          // guard covered the old profile and not the added one, which is precisely the claim
          // `ReferenceProfile.fs`'s token-renderer comment makes on this test's behalf. Binding the
          // same list D1 and D3 bind means a future profile is covered by adding one entry.
          test "D2 the loaded reference set reproduces the authoritative checks field-for-field" {
              let loadedById =
                  loadedChecks ()
                  |> List.map (fun c ->
                      let (CheckId id) = c.Id
                      id, c)
                  |> Map.ofList

              let mutable compared = 0

              for profile in ReferenceProfile.boundProfiles do
                  let (TemplateProfile name) = profile
                  let authoritative = authoritativeById profile

                  Expect.isNonEmpty
                      (Map.toList authoritative)
                      (sprintf
                          "profile '%s' is in boundProfiles but binds no check — this gate would have no subject for it"
                          name)

                  for KeyValue(id, expected) in authoritative do
                      match Map.tryFind id loadedById with
                      | None ->
                          failtestf
                              "the published reference set declares no check '%s', but the authoritative embedded profile '%s' binds it — the derived artifact is missing a rule it must carry"
                              id
                              name
                      | Some actual ->
                          // Compare the whole record. `Check` is a plain record of closed unions, so
                          // structural equality covers every field including ones added later — a new
                          // field cannot slip past this by not being named here.
                          Expect.equal
                              actual
                              expected
                              (sprintf
                                  "check '%s' of profile '%s' as PARSED from the published YAML must equal the authoritative embedded record exactly (a renderer/parser vocabulary mismatch shows up here)"
                                  id
                                  name)

                          compared <- compared + 1

              // A loop that compared nothing is a gate that passed by absence. `boundProfiles` being
              // empty, or every profile's checks silently vanishing, would otherwise read as green.
              Expect.isGreaterThan
                  compared
                  0
                  "D2 must actually compare at least one check, or it is asserting over an empty iteration"
          }

          // ── D3 — one authority, not two ──
          test "D3 every bound profile has a region, and the set declares no un-derived floor check" {
              // (a) A profile added to the code without regenerating the YAML fails closed.
              for profile in ReferenceProfile.boundProfiles do
                  match ReferenceProfile.extractRegion profile (capabilitiesText ()) with
                  | Ok _ -> ()
                  | Error e ->
                      let (TemplateProfile p) = profile

                      failtestf
                          "profile '%s' is bound by ReferenceProfile.checksFor but has no generated region in the published set: %s"
                          p
                          e

              // (b) The reverse direction — a hand-authored check in a floor-owned DOMAIN would be a
              // second authority reappearing beside the generated one, which is the exact end state
              // docs/decisions/0011 rules out. Scope the assertion to the domains the floor owns:
              // the reference set is free to declare its own build/test/evidence checks.
              let floorChecks =
                  ReferenceProfile.boundProfiles |> List.collect ReferenceProfile.checksFor

              let floorDomains = floorChecks |> List.map (fun c -> c.Domain) |> Set.ofList

              let floorIds =
                  floorChecks
                  |> List.map (fun c ->
                      let (CheckId id) = c.Id
                      id)
                  |> Set.ofList

              let undeclared =
                  loadedChecks ()
                  |> List.filter (fun c -> Set.contains c.Domain floorDomains)
                  |> List.map (fun c ->
                      let (CheckId id) = c.Id
                      id)
                  |> List.filter (fun id -> not (Set.contains id floorIds))

              Expect.isEmpty
                  undeclared
                  "the published set declares check(s) in a floor-owned domain that the authoritative embedded profile does not bind — that is a second, hand-maintained authority, which docs/decisions/0011 rules out. Add them to ReferenceProfile.checksFor and regenerate."
          }

          // ── D4 — the gate cannot pass by absence ──
          // Executed, not asserted in prose: a gate whose fail-closed behaviour is only claimed is
          // indistinguishable from one that quietly no-ops when its subject disappears (#266).
          test "D4 a missing, duplicated, or inverted marker pair is an error, never a silent pass" {
              let text = capabilitiesText ()

              // Per profile, for the same reason D1/D2/D3 iterate: the marker mechanics are what
              // make a NEWLY bound profile fail closed until its pair is hand-placed, so proving
              // them on one profile and not on the other would leave exactly the added case
              // unproven.
              for profile in ReferenceProfile.boundProfiles do
                  let (TemplateProfile name) = profile
                  let beginLine = ReferenceProfile.beginMarker profile
                  let endLine = ReferenceProfile.endMarker profile

                  let expectError (label: string) (candidate: string) =
                      match ReferenceProfile.extractRegion profile candidate with
                      | Ok _ -> failtestf "%s (profile '%s') must fail closed, but the region was located" label name
                      | Error _ -> ()

                  expectError "a removed BEGIN marker" (text.Replace(beginLine + "\n", ""))
                  expectError "a removed END marker" (text.Replace(endLine + "\n", ""))
                  expectError "a duplicated BEGIN marker" (text.Replace(beginLine, beginLine + "\n" + beginLine))
                  expectError "a duplicated END marker" (text.Replace(endLine, endLine + "\n" + endLine))

                  // An inverted pair: both markers present exactly once, in the wrong order. Built
                  // by SWAPPING THE TWO LINES BY INDEX rather than by a three-step string replace
                  // through a sentinel. #385: the sentinel this test used was a literal NUL
                  // (`"\u0000BEGIN\u0000"`), chosen so it could not collide with file content — it
                  // cannot, but it also made this source file binary to every ordinary tool
                  // (`file` reports `data`, `grep` refuses it as binary, and exact-string editors
                  // cannot match it). Swapping by index needs no sentinel at all, so the collision
                  // question does not arise. Behaviour is identical: markers present once each, in
                  // the wrong order.
                  let inverted =
                      let lines = (text.Replace("\r\n", "\n")).Split '\n'
                      let indexOf (marker: string) =
                          match lines |> Array.tryFindIndex (fun l -> l.TrimEnd() = marker) with
                          | Some i -> i
                          | None -> failtestf "profile '%s' must have a %s marker to invert" name marker

                      let bi = indexOf beginLine
                      let ei = indexOf endLine
                      let swapped = Array.copy lines
                      swapped.[bi] <- endLine
                      swapped.[ei] <- beginLine
                      String.concat "\n" swapped

                  expectError "an inverted marker pair" inverted

                  // …and the untouched file is located, so the five assertions above are not all
                  // passing for the trivial reason that nothing ever matches.
                  match ReferenceProfile.extractRegion profile text with
                  | Ok _ -> ()
                  | Error e ->
                      failtestf
                          "the unmodified reference set must locate profile '%s''s region (control case): %s"
                          name
                          e
          }
        ]
