module FS.GG.Governance.CurrencySensing.Tests.SenseRepoTests

// senseRepo against a REAL temp repo: real `.fsgg/refresh.yml`, real `.fsgg/refresh.lock.json`, real source
// files. This is the genuine edge-sensing I/O path (parse → read lock → digest sources → decide → gate),
// never mocked. Proves: a source-drifted view under `block-on-ship` becomes a Blocking finding; a fresh view
// produces none; unconfigured produces none (byte-identity); a missing lock is undeterminable (never a pass).

open System
open System.IO
open System.Security.Cryptography
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement
open FS.GG.Governance.CurrencySensing.CurrencySensing

let private sha (s: string) =
    use h = SHA256.Create()
    h.ComputeHash(Text.Encoding.UTF8.GetBytes s) |> Array.map (fun b -> b.ToString("x2")) |> String.concat ""

let private withTempRepo (body: string -> 'a) : 'a =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-cs-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(Path.Combine(dir, ".fsgg")) |> ignore

    try
        body dir
    finally
        try
            Directory.Delete(dir, true)
        with _ ->
            ()

let private writeRel (dir: string) (rel: string) (content: string) =
    let full = Path.Combine(dir, rel)

    match Path.GetDirectoryName full with
    | null -> ()
    | d -> Directory.CreateDirectory d |> ignore

    File.WriteAllText(full, content)

let private refreshYml (dial: string option) =
    (match dial with
     | Some d -> sprintf "currency-enforcement: %s\n" d
     | None -> "")
    + "views:\n  - id: v\n    kind: route-projection\n    output: out.json\n    sources:\n      - src.txt\n    generator: [\"cp\"]\n    generatorBasis: g1\n"

let private lockJson (sourceHashes: string list) (gen: string) =
    let arr = sourceHashes |> List.map (sprintf "\"%s\"") |> String.concat ","
    sprintf "{\"schemaVersion\":\"fsgg.refresh-lock/v1\",\"views\":{\"v\":{\"sources\":[%s],\"generatorVersion\":\"%s\",\"output\":\"x\"}}}" arr gen

[<Tests>]
let tests =
    testList
        "CurrencySensing.senseRepo"
        [ test "a source-drifted view under block-on-ship ⇒ one Blocking finding naming the view (SC-001/SC-005)" {
              withTempRepo (fun dir ->
                  writeRel dir "src.txt" "current\n"
                  writeRel dir ".fsgg/refresh.yml" (refreshYml (Some "block-on-ship"))
                  // recorded lock disagrees with the live source digest ⇒ stale (source drift).
                  writeRel dir ".fsgg/refresh.lock.json" (lockJson [ sha "OLD-STALE\n" ] "g1")

                  match senseRepo dir with
                  | [ f ] ->
                      Expect.equal f.ViewId "v" "names the stale view"
                      Expect.equal f.BaseSeverity Blocking "base severity Blocking (so the dial can block)"
                      Expect.equal f.Maturity BlockOnShip "maturity = the configured dial"

                      match f.Cause with
                      | SourceDrift drifted -> Expect.equal drifted [ CoveredArtifactsCat ] "covered-artifacts drift"
                      | other -> failtestf "expected SourceDrift, got %A" other
                  | other -> failtestf "expected exactly one finding, got %A" other)
          }

          test "a fresh view (lock matches the live digest) ⇒ no finding (SC-006, no false positive)" {
              withTempRepo (fun dir ->
                  writeRel dir "src.txt" "current\n"
                  writeRel dir ".fsgg/refresh.yml" (refreshYml (Some "block-on-ship"))
                  writeRel dir ".fsgg/refresh.lock.json" (lockJson [ sha "current\n" ] "g1")

                  Expect.equal (senseRepo dir) [] "a current view produces no finding")
          }

          test "unconfigured (no dial) ⇒ no finding even when stale (FR-004 byte-identity)" {
              withTempRepo (fun dir ->
                  writeRel dir "src.txt" "current\n"
                  writeRel dir ".fsgg/refresh.yml" (refreshYml None)
                  writeRel dir ".fsgg/refresh.lock.json" (lockJson [ sha "OLD-STALE\n" ] "g1")

                  Expect.equal (senseRepo dir) [] "no dial ⇒ no findings ⇒ byte-identical")
          }

          test "a missing provenance lock under a dial ⇒ Undeterminable finding, never a silent pass (FR-008)" {
              withTempRepo (fun dir ->
                  writeRel dir "src.txt" "current\n"
                  writeRel dir ".fsgg/refresh.yml" (refreshYml (Some "block-on-ship"))
                  // NO refresh.lock.json written.

                  match senseRepo dir with
                  | [ f ] ->
                      match f.Cause with
                      | Undeterminable _ -> Expect.equal f.BaseSeverity Blocking "undeterminable still blocks when configured"
                      | other -> failtestf "expected Undeterminable, got %A" other
                  | other -> failtestf "expected one undeterminable finding, got %A" other)
          }

          test "an absent refresh.yml ⇒ no finding (byte-identity)" {
              withTempRepo (fun dir -> Expect.equal (senseRepo dir) [] "no manifest ⇒ no findings")
          } ]
