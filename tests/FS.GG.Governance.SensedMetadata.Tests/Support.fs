module FS.GG.Governance.SensedMetadata.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.SensedMetadata
open FS.GG.Governance.SensedMetadata.Model

// Shared REAL-input builders + FsCheck generators for the F034 tests (Principle V — every value below is a
// real, literally-constructible typed measured value, never a mock; no clock is read, no process is spawned).
// The operations are pure, so no upstream chain is needed: the values a host would sense (the wall-clock
// instant, the elapsed duration) are handed in as literals (incl. real F032 `SensedDuration`s) — the core's
// contract. No I/O beyond repo-root resolution.

// ── Real literal builders ──

/// A `SensedLabel` from a literal (incl. empty string and marker-containing text).
let label (s: string) : SensedLabel = SensedLabel s

/// A `SensedTimestamp` from a literal (incl. empty string and marker-containing text).
let timestamp (s: string) : SensedTimestamp = SensedTimestamp s

/// A real F032 `SensedDuration` from an `int64` literal (incl. `0L` and large/negative magnitudes).
let duration (ns: int64) : SensedDuration = SensedDuration ns

/// Convenience: mark a duration metadatum with a label, through the PUBLIC `markDuration`.
let markDur (l: string) (ns: int64) : SensedMetadatum = SensedMetadata.markDuration (label l) (duration ns)

/// Convenience: mark a timestamp metadatum with a label, through the PUBLIC `markTimestamp`.
let markTs (l: string) (s: string) : SensedMetadatum = SensedMetadata.markTimestamp (label l) (timestamp s)

// ── The worked-example metadata (contracts/sensed-metadata-format.md) ──

/// A timestamp labelled `at`, value `2026-06-21T12:00:00Z`.
let workedTimestamp: SensedMetadatum = markTs "at" "2026-06-21T12:00:00Z"

/// A duration labelled `elapsed`, value `SensedDuration 1830000000L`.
let workedDuration: SensedMetadatum = markDur "elapsed" 1_830_000_000L

// ── FsCheck generators (real values, no mocks) ──
// The label/value strings are drawn to INCLUDE the marker characters `!`, `;`, `:`, `=` so the
// unspoofability/injectivity law is actually exercised (not just clean inputs).

let private spoofyStringGen: Gen<string> =
    Gen.elements
        [ ""
          "a"
          "at"
          "elapsed"
          "!sensed!"
          "!sensed-section!"
          ";"
          ":"
          "="
          "0:"
          "x:y=z;|"
          "!sensed!=timestamp;2:at"
          "héllo"
          "2026-06-21T12:00:00Z" ]

let private genLabel: Gen<SensedLabel> = spoofyStringGen |> Gen.map SensedLabel

let private genTimestamp: Gen<SensedTimestamp> = spoofyStringGen |> Gen.map SensedTimestamp

let private genDuration: Gen<SensedDuration> =
    Gen.elements [ 0L; 1L; -1L; 1_830_000_000L; Int64.MaxValue; Int64.MinValue; 123_456L ]
    |> Gen.map SensedDuration

let private genSensedValue: Gen<SensedValue> =
    Gen.oneof
        [ genTimestamp |> Gen.map TimestampValue
          genDuration |> Gen.map DurationValue ]

let private genMetadatum: Gen<SensedMetadatum> =
    gen {
        let! l = genLabel
        let! v = genSensedValue
        return { Label = l; Value = v }
    }

let private genMetadatumList: Gen<SensedMetadatum list> = Gen.listOf genMetadatum

type Generators =
    static member SensedLabel() : Arbitrary<SensedLabel> = Arb.fromGen genLabel
    static member SensedTimestamp() : Arbitrary<SensedTimestamp> = Arb.fromGen genTimestamp
    static member SensedDuration() : Arbitrary<SensedDuration> = Arb.fromGen genDuration
    static member SensedValue() : Arbitrary<SensedValue> = Arb.fromGen genSensedValue
    static member SensedMetadatum() : Arbitrary<SensedMetadatum> = Arb.fromGen genMetadatum
    static member SensedMetadatumList() : Arbitrary<SensedMetadatum list> = Arb.fromGen genMetadatumList

/// FsCheck config registering the real F034 generators.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<Generators> ] }
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
