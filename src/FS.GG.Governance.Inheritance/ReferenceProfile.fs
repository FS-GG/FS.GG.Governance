namespace FS.GG.Governance.Inheritance

open System
open FS.GG.Governance.Config.Model

// docs/decisions/0011: the authoritative reference profile + the YAML projection derived from it.
// Visibility lives in ReferenceProfile.fsi (Principle II) — the token renderers and the marker
// helpers below are hidden by their ABSENCE from the .fsi. PURE and TOTAL: no I/O, no clock, no
// exceptions.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ReferenceProfile =

    // ── The authoritative table ──────────────────────────────────────────────────────────────
    // This is THE org profile. It moved here from `Inheritance.referenceChecks` (which now calls
    // it) so the single source has a name a decision record, a projection, and a drift gate can
    // all cite. Enforcement still reaches it exactly as before, through
    // `Inheritance.referenceGatesFor -> Gates.buildRegistry`.

    let boundProfiles: TemplateProfile list =
        [ FS.GG.Governance.SurfaceChecks.Profile.profileKey
          TemplateProfile "game" ]

    let checksFor (profile: TemplateProfile) : Check list =
        let (TemplateProfile p) = profile
        let (TemplateProfile constitution) = FS.GG.Governance.SurfaceChecks.Profile.profileKey

        match p with
        // The composed F# constitution profile (FS-GG/FS.GG.Governance#385, docs/decisions/0012):
        // epic #367's four rule packs — #366, #368, #369, #370 — as ONE named gate set. The table
        // itself is NOT restated here. It lives in `SurfaceChecks.Profile`, which is where the
        // packs themselves read their declared maturity from, so the composition has exactly one
        // authority — the same property docs/decisions/0011 gave the `game` floor, applied to the
        // constitution packs. This case is the binding, not a second copy.
        | _ when p = constitution -> FS.GG.Governance.SurfaceChecks.Profile.checks
        | "game" ->
            // The per-FR gameplay-obligation gate every `game` product inherits as a floor (epic
            // FS-GG/.github#1190). Command left unbound (no `tooling.yml` dependency). WI-8
            // (FS-GG/FS.GG.Governance#276) flipped the maturity from `warn` to `block-on-ship`, the
            // ADR-0049 profile binding at full teeth: every `game` product inherits this gate as a
            // NON-LOWERABLE block-on-ship floor.
            [ { Id = CheckId "fr-covered"
                Domain = DomainId "gameplay"
                Command = None
                Owner = Owner "platform"
                Cost = High
                Environment = Ci
                Maturity = BlockOnShip
                Tier = None }
              // Stronger than generic observed gameplay coverage: when SDD classifies a requirement
              // as a production journey, only a compatible runner-issued boot/input/update receipt
              // satisfies it (docs/decisions/0010).
              { Id = CheckId "production-journey"
                Domain = DomainId "gameplay"
                Command = None
                Owner = Owner "platform"
                Cost = High
                Environment = Ci
                Maturity = BlockOnShip
                Tier = None } ]
        | _ -> []

    // ── YAML token renderers ─────────────────────────────────────────────────────────────────
    // These mirror `Config.Schema`'s parse vocabulary. They are a SECOND mapping of the same
    // closed sets, which is exactly the kind of duplicate this decision exists to distrust — so it
    // is not trusted: the derivation gate loads the GENERATED file back through the real
    // `Config.Loader` and compares the parsed `Check` records field-by-field against `checksFor`.
    // A wrong token here does not produce a quietly-wrong published profile; it produces a red
    // gate, and `pack-reference-gate-set.fsx` then refuses to pack. (Config exposes render tokens
    // for `SurfaceClass` and `GeneratedProductTier` only, so `generatedProductTierToken` below IS
    // the shared one; cost/environment/maturity have no public renderer to reuse.)

    let private costToken =
        function
        | Cheap -> "cheap"
        | Medium -> "medium"
        | High -> "high"
        | Exhaustive -> "exhaustive"

    let private environmentToken =
        function
        | Local -> "local"
        | Ci -> "ci"
        | LocalOrCi -> "local-or-ci"
        | Release -> "release"

    let private maturityToken =
        function
        | Observe -> "observe"
        | Warn -> "warn"
        | BlockOnPr -> "block-on-pr"
        | BlockOnShip -> "block-on-ship"
        | BlockOnRelease -> "block-on-release"

    // ── Region markers ───────────────────────────────────────────────────────────────────────
    // Indented two spaces so the whole region sits at the `checks:` list-item level and the file
    // stays valid YAML with the region spliced in place.

    let private indent = "  "

    let private profileName (TemplateProfile p) = p

    let beginMarker (profile: TemplateProfile) : string =
        sprintf "%s# BEGIN GENERATED: fsgg-reference-profile:%s" indent (profileName profile)

    let endMarker (profile: TemplateProfile) : string =
        sprintf "%s# END GENERATED: fsgg-reference-profile:%s" indent (profileName profile)

    // ── Projection ───────────────────────────────────────────────────────────────────────────

    /// One check as `checks:` list-item lines. Field order matches the hand-authored order the
    /// reference set already used, so the derivation's first landing is a minimal diff.
    let private renderCheck (c: Check) : string list =
        let (CheckId id) = c.Id
        let (DomainId domain) = c.Domain
        let (Owner owner) = c.Owner

        // The entry's mapping opens at `- id:`, so every sibling field aligns with `id` — two
        // columns past the list dash, NOT past `indent`. Six spaces would be a deeper block and a
        // YAML parse error, not a cosmetic difference.
        let field = indent + "  "

        [ yield sprintf "%s- id: %s" indent id
          yield sprintf "%sdomain: %s" field domain
          match c.Command with
          | Some(CommandId cmd) -> yield sprintf "%scommand: %s" field cmd
          | None -> ()
          yield sprintf "%sowner: %s" field owner
          yield sprintf "%scost: %s" field (costToken c.Cost)
          yield sprintf "%senvironment: %s" field (environmentToken c.Environment)
          yield sprintf "%smaturity: %s" field (maturityToken c.Maturity)
          match c.Tier with
          | Some tier -> yield sprintf "%stier: %s" field (generatedProductTierToken tier)
          | None -> () ]

    /// Fixed provenance banner. Profile-independent by design: it names the SOURCE and the
    /// regenerate/gate route, both of which are the same for every profile.
    let private provenance () : string list =
        [ sprintf "%s# DO NOT EDIT — GENERATED from the authoritative embedded F# reference profile" indent
          sprintf
              "%s# (src/FS.GG.Governance.Inheritance/ReferenceProfile.fs, `ReferenceProfile.checksFor`) by"
              indent
          sprintf "%s# `ReferenceProfile.capabilitiesRegion`. Decisions: docs/decisions/0011 (the" indent
          sprintf "%s# derivation) and docs/decisions/0012 (the composed F# constitution profile)." indent
          sprintf "%s# Regenerate:" indent
          sprintf
              "%s#   BLESS_REFERENCE_GATE_SET=1 dotnet test tests/FS.GG.Governance.ReferenceGateSet.Tests"
              indent
          sprintf "%s# A hand edit is caught by the `ReferenceGateSetGuard` derivation gate, which" indent
          sprintf "%s# fails closed — and pack-reference-gate-set.fsx refuses to pack while it is red." indent ]

    let capabilitiesRegion (profile: TemplateProfile) : string =
        [ yield beginMarker profile
          yield! provenance ()
          yield! (checksFor profile |> List.collect renderCheck)
          yield endMarker profile ]
        |> String.concat "\n"

    // ── Region location (fail closed) ────────────────────────────────────────────────────────

    /// Index every line equal (after trailing-whitespace trim) to `marker`. Exact-line matching,
    /// not `Contains`: a marker quoted inside prose elsewhere in the file must not be mistaken for
    /// the region's own boundary.
    let private markerLines (lines: string[]) (marker: string) : int list =
        lines
        |> Array.indexed
        |> Array.filter (fun (_, l) -> l.TrimEnd() = marker)
        |> Array.map fst
        |> List.ofArray

    let private locate (profile: TemplateProfile) (lines: string[]) : Result<int * int, string> =
        let b = beginMarker profile
        let e = endMarker profile

        match markerLines lines b, markerLines lines e with
        | [], _ -> Error(sprintf "generated-region begin marker not found: %s" (b.Trim()))
        | _, [] -> Error(sprintf "generated-region end marker not found: %s" (e.Trim()))
        | [ bi ], [ ei ] when bi < ei -> Ok(bi, ei)
        | [ bi ], [ ei ] -> Error(sprintf "generated-region end marker (line %d) precedes its begin marker (line %d)" (ei + 1) (bi + 1))
        | bs, es ->
            Error(
                sprintf
                    "generated-region markers must appear exactly once each; found %d begin and %d end marker(s) for profile '%s'"
                    (List.length bs)
                    (List.length es)
                    (profileName profile)
            )

    /// Split on `\n` after normalizing `\r\n`, remembering whether the text used CRLF so a
    /// replacement can put it back.
    let private splitLines (text: string) : bool * string[] =
        let crlf = text.Contains "\r\n"
        (crlf, text.Replace("\r\n", "\n").Split '\n')

    let extractRegion (profile: TemplateProfile) (text: string) : Result<string, string> =
        let _, lines = splitLines text

        locate profile lines
        |> Result.map (fun (bi, ei) -> lines.[bi .. ei] |> String.concat "\n")

    let replaceRegion (profile: TemplateProfile) (text: string) : Result<string, string> =
        let crlf, lines = splitLines text

        locate profile lines
        |> Result.map (fun (bi, ei) ->
            let rebuilt =
                [ yield! lines.[.. bi - 1]
                  yield capabilitiesRegion profile
                  yield! lines.[ei + 1 ..] ]
                |> String.concat "\n"

            if crlf then rebuilt.Replace("\n", "\r\n") else rebuilt)
