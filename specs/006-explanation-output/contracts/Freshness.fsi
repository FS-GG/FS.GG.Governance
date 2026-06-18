// Curated public signature contract for evidence freshness (F06).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Freshness.fs carries NO `private`/`internal`/`public`
// modifiers on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any Freshness.fs body exists (Principle I). The shape
// mirrors docs/governance-design/kernel.md ("evidence freshness") — the SIMPLE, causal
// model: evidence describes the artifact version present when it was recorded, so it is
// fresh exactly while no covered artifact has changed since.
//
// This is the temporal companion to F05's evidence states: F05 tracks HOW TRUSTWORTHY
// the evidence is (real vs synthetic); this tracks WHETHER it is still CURRENT. It is a
// PURE function of supplied, comparable instants — it reads NO clock, NO filesystem, NO
// git, NO network (FR-010). Discovering real artifact modification times and recording
// the result are the edge interpreter's job (F08), NOT this feature (FR-013). Instants
// are generic (`'instant : comparison`, carrying no domain vocabulary), so the same
// predicate serves a software test, a research measurement, or a citation check (FR-012).
// An absolute max-age / TTL notion is deliberately OUT OF SCOPE (spec Assumptions).

namespace FS.GG.Governance.Kernel

/// Whether a recorded piece of evidence still describes the current artifacts.
/// `Fresh` — recorded at or after the latest change to every artifact it covers;
/// `Stale` — some covered artifact changed after the evidence was recorded (the
/// evidence describes a version that no longer exists).
type Freshness =
    | Fresh
    | Stale

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Freshness =

    /// Decide freshness from the instant the evidence was `recorded` and the last-change
    /// instants of the artifacts it `covered`. Returns `Fresh` iff `recorded` is at or
    /// after EVERY instant in `covered` (equivalently `recorded >= max covered`), and
    /// `Stale` if any covered instant is strictly after `recorded`. The boundary is
    /// INCLUSIVE — `recorded` equal to a covered instant is `Fresh` (FR-009). Evidence
    /// covering NO artifacts (`covered = []`) is `Fresh` — there is nothing it can be
    /// stale against (FR-009). PURE over the supplied instants: identical inputs always
    /// give the identical result regardless of when it is evaluated; reads no clock,
    /// filesystem, git, or network (FR-008, FR-010, SC-007, SC-008). Total for every
    /// input.
    val decide: recorded: 'instant -> covered: 'instant list -> Freshness when 'instant: comparison

    /// `decide` as a boolean: `true` iff the evidence is `Fresh` (a convenience for
    /// callers that want a predicate rather than the `Freshness` value). Same semantics,
    /// same purity (FR-008, FR-010).
    val isFresh: recorded: 'instant -> covered: 'instant list -> bool when 'instant: comparison
