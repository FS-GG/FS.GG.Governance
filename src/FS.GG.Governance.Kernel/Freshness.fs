namespace FS.GG.Governance.Kernel

// Evidence freshness (F06 · US3). A PURE predicate over supplied, comparable instants:
// evidence is Fresh exactly while no covered artifact has changed since it was recorded.
// No clock, no filesystem, no git, no network (FR-010). No visibility modifiers on
// top-level bindings — the surface is Freshness.fsi (Principle II).

type Freshness =
    | Fresh
    | Stale

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Freshness =

    let decide (recorded: 'instant) (covered: 'instant list) : Freshness =
        // Fresh iff recorded is at or after the latest covered change (inclusive boundary);
        // empty covered ⇒ nothing to be stale against ⇒ Fresh. Pure over the supplied
        // instants — no clock, no I/O (FR-008/009/010).
        match covered with
        | [] -> Fresh
        | _ -> if recorded >= List.max covered then Fresh else Stale

    let isFresh (recorded: 'instant) (covered: 'instant list) : bool = decide recorded covered = Fresh
