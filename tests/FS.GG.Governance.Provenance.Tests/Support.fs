module FS.GG.Governance.Provenance.Tests.Support

open System
open System.IO
open Expecto
open FsCheck
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.Provenance
open FS.GG.Governance.Provenance.Model

// Shared REAL-input builders + FsCheck generators for the F033 tests (Principle V — every value below is a
// real, literally-constructible typed build fact, never a mock; no process is spawned). The operations are
// pure, so no upstream chain is needed: the nine facts a host would sense are handed in as literals
// (including real F032 `CommandRecord`s assembled via `CommandRecord.build`) — the core's contract. No I/O
// beyond repo-root resolution.

// ── A real, complete command record the provenance carries (the F032 worked example) ──

/// One real F032 command record, built through the PUBLIC `CommandRecord.build` (never a record literal).
/// This is the contracts/provenance-identity-format.md worked-example record: gcc -c main.c in /work, one
/// added env var CI=1, timeout 30, exit 0, stdout/stderr digests, no captured-output file.
let makeCommandRecord (duration: int64) : CommandRecord =
    CommandRecord.build
        (Executable "gcc")
        [ Argument "-c"; Argument "main.c" ]
        (WorkingDirectory "/work")
        { Added = [ { Name = EnvVarName "CI"; Value = EnvVarValue "1" } ]; Changed = []; Removed = [] }
        (TimeoutLimit 30)
        (ExitCode 0)
        (OutputDigest "sha-out")
        (OutputDigest "sha-err")
        NoCapturedOutput
        (SensedDuration duration)

let baseCommandRecord = makeCommandRecord 123_456L

// ── The nine literal facts of a representative build (the worked example) ──
// Every fact is present and distinct so a single-field perturbation is unambiguous.

let baseSourceCommit = Revision "c0ffee"
let baseBase = Revision "base1"
let baseHead = Revision "head2"
let baseRuleHash = RuleHash "rule-x"
let baseGeneratorVersion = GeneratorVersion "gen-1"
let baseArtifactDigests = [ ArtifactHash "a1"; ArtifactHash "a2" ]
let baseCommandRecords = [ baseCommandRecord ]
let baseEnvironment = Local
let baseBuilder = BuilderIdentity "ci-runner"

/// The complete base `Provenance`, assembled through the PUBLIC `build` (never a record literal — the tests
/// exercise the real assembly point).
let baseProvenance: Provenance =
    Provenance.build
        baseSourceCommit
        baseBase
        baseHead
        baseRuleHash
        baseGeneratorVersion
        baseArtifactDigests
        baseCommandRecords
        baseEnvironment
        baseBuilder

/// Rebuild a provenance from a (possibly perturbed) `Provenance` value, via the public `build` — so every
/// test perturbs exactly one fact and re-assembles through the real entry point.
let rebuild (p: Provenance) : Provenance =
    Provenance.build
        p.SourceCommit
        p.Base
        p.Head
        p.RuleHash
        p.GeneratorVersion
        p.ArtifactDigests
        p.CommandRecords
        p.Environment
        p.Builder

// ── One representative single-field variant per reproducible fact ──
// Each takes a `Provenance` and changes EXACTLY the named fact to a distinct value, for table-driven
// per-field-sensitivity tests (SC-004). The artifact-digest set adds a new digest; the command records are
// reordered (order is significant — D4).

let variantSourceCommit (p: Provenance) = { p with SourceCommit = Revision "deadbeef" }
let variantBase (p: Provenance) = { p with Base = Revision "base9" }
let variantHead (p: Provenance) = { p with Head = Revision "head9" }
let variantRuleHash (p: Provenance) = { p with RuleHash = RuleHash "rule-y" }
let variantGeneratorVersion (p: Provenance) = { p with GeneratorVersion = GeneratorVersion "gen-2" }
let variantArtifactAdded (p: Provenance) = { p with ArtifactDigests = p.ArtifactDigests @ [ ArtifactHash "a3" ] }
let variantCommandRecordFact (p: Provenance) =
    let other =
        CommandRecord.build
            (Executable "clang")
            [ Argument "-c"; Argument "main.c" ]
            (WorkingDirectory "/work")
            { Added = [ { Name = EnvVarName "CI"; Value = EnvVarValue "1" } ]; Changed = []; Removed = [] }
            (TimeoutLimit 30)
            (ExitCode 0)
            (OutputDigest "sha-out")
            (OutputDigest "sha-err")
            NoCapturedOutput
            (SensedDuration 123_456L)
    { p with CommandRecords = [ other ] }
let variantCommandRecordOrder (p: Provenance) =
    // Two distinct records; reversing them changes the (order-significant) cmds segment.
    let r2 =
        CommandRecord.build
            (Executable "ld")
            []
            (WorkingDirectory "/work")
            { Added = []; Changed = []; Removed = [] }
            (TimeoutLimit 30)
            (ExitCode 0)
            (OutputDigest "o2")
            (OutputDigest "e2")
            NoCapturedOutput
            (SensedDuration 1L)
    { p with CommandRecords = [ p.CommandRecords.Head; r2 ] |> List.rev }
let variantEnvironment (p: Provenance) = { p with Environment = Ci }
let variantBuilder (p: Provenance) = { p with Builder = BuilderIdentity "other-agent" }

/// Every reproducible fact paired with a single-field variation, each labelled. Table-driven sensitivity
/// tests iterate this so EVERY reproducible fact (including a command record's reproducible facts AND the
/// command-record order) is covered (SC-004).
let allReproducibleVariants: (string * (Provenance -> Provenance)) list =
    [ "source commit", variantSourceCommit
      "base", variantBase
      "head", variantHead
      "rule hash", variantRuleHash
      "generator version", variantGeneratorVersion
      "artifact digest added", variantArtifactAdded
      "command record fact", variantCommandRecordFact
      "command record order", variantCommandRecordOrder
      "environment", variantEnvironment
      "builder", variantBuilder ]

// ── FsCheck generators (real values, no mocks) ──

let private shortStringGen: Gen<string> =
    Gen.elements [ ""; "a"; "b"; "c0ffee"; "base1"; "head2"; "rule-x"; "gen-1"; "ci-runner"; "héllo"; "x:y=z;|" ]

let private genEnvironmentClass: Gen<EnvironmentClass> =
    Gen.elements [ Local; Ci; LocalOrCi; Release ]

let private genArtifactHash: Gen<ArtifactHash> = shortStringGen |> Gen.map ArtifactHash

let private genCommandRecord: Gen<CommandRecord> =
    gen {
        let! exe = shortStringGen
        let! arg = shortStringGen
        let! cwd = shortStringGen
        let! out = shortStringGen
        let! err = shortStringGen
        let! d = Gen.choose (0, 1_000_000_000)

        return
            CommandRecord.build
                (Executable exe)
                [ Argument arg ]
                (WorkingDirectory cwd)
                { Added = []; Changed = []; Removed = [] }
                (TimeoutLimit 30)
                (ExitCode 0)
                (OutputDigest out)
                (OutputDigest err)
                NoCapturedOutput
                (SensedDuration(int64 d))
    }

let private genProvenance: Gen<Provenance> =
    gen {
        let! sc = shortStringGen
        let! b = shortStringGen
        let! h = shortStringGen
        let! rh = shortStringGen
        let! gv = shortStringGen
        let! digests = Gen.listOf genArtifactHash
        let! records = Gen.listOf genCommandRecord
        let! env = genEnvironmentClass
        let! bld = shortStringGen

        return
            Provenance.build
                (Revision sc)
                (Revision b)
                (Revision h)
                (RuleHash rh)
                (GeneratorVersion gv)
                digests
                records
                env
                (BuilderIdentity bld)
    }

type Generators =
    static member Provenance() : Arbitrary<Provenance> = Arb.fromGen genProvenance
    static member CommandRecord() : Arbitrary<CommandRecord> = Arb.fromGen genCommandRecord

/// FsCheck config registering the real F033 generators.
let fscheckConfig =
    { FsCheckConfig.defaultConfig with arbitrary = [ typeof<Generators> ] }

/// A same-SET permutation+duplication of an artifact-digest list: reverse it and duplicate the head, so the
/// underlying set is preserved while order and multiplicity change (for the order/dup-invariance properties).
let permuteAndDuplicateDigests (digests: ArtifactHash list) : ArtifactHash list =
    match List.rev digests with
    | [] -> []
    | head :: _ as reversed -> reversed @ [ head ]

// ── Repo root (for the surface baseline path) ──

/// Locate the repo root (the dir holding the solution) by walking up from the test binary.
let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then d.FullName
        else findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))
