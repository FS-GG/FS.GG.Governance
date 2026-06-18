// Implementation of the composition-root machinery (F09). Visibility lives in
// Composition.fsi (Principle II) — NO `private`/`internal`/`public` on top-level
// bindings here; the signature file hides every binding it does not declare. Pure
// value/fold layer: no I/O, no Model/Msg/Effect, no interpreter (Principle IV N/A). It
// adds NO evaluation or precedence logic — confluence is the kernel's least fixed point
// (F01) and cross-domain precedence is F07's `Route`. Reuses F09 `Adapter`/`Lift`, F04
// `CheckRule`/`Bridge`, F07 `Fence`, F01 `Rule`.

namespace FS.GG.Governance.Adapters.Spi

open FS.GG.Governance.Kernel

type Lifted<'project, 'change> =
    { Rules: CheckRule<'project> list
      Fences: Fence<'change> list }

type Composed<'project, 'change> =
    { Catalog: CheckRule<'project> list
      Fences: Fence<'change> list }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Composition =

    let lift
        (project: 'project -> 'dom option)
        (narrow: 'change -> 'domChange)
        (adapter: Adapter<'dom, 'artifact, 'domChange>)
        : Lifted<'project, 'change> =
        { Rules = adapter.Rules |> List.map (Lift.checkRule project)
          Fences = adapter.Fences |> List.map (Lift.fence narrow) }

    let compose
        (lifted: Lifted<'project, 'change> list)
        (crossDomain: CheckRule<'project> list)
        : Composed<'project, 'change> =
        // Catalog: every adapter's lifted rules, in list order, followed by the small,
        // named cross-domain set. A pure CONCATENATION — its downstream least fixed point
        // is order-independent (the kernel's Datalog guarantee, law C2).
        let catalog = (lifted |> List.collect (fun l -> l.Rules)) @ crossDomain
        // Fences: the union of all lifted fences, DEDUPED BY NAME keeping the first
        // occurrence under the stable list order — two adapters naming the same surface are
        // counted once (law C5, FR-011). A SET, so the route over it is order-independent.
        let fences =
            (lifted |> List.collect (fun l -> l.Fences))
            |> List.fold
                (fun (seen: Set<string>, acc) (f: Fence<'change>) ->
                    if seen.Contains f.Name then (seen, acc) else (seen.Add f.Name, f :: acc))
                (Set.empty, [])
            |> snd
            |> List.rev

        { Catalog = catalog; Fences = fences }

    let toRules (bridge: Bridge<'project>) (composed: Composed<'project, 'change>) : Rule<'project> list =
        composed.Catalog |> List.map (CheckRule.toRule bridge)
