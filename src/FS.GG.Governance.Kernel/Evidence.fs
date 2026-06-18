namespace FS.GG.Governance.Kernel

// Evidence model & synthetic taint (F05): a pure, domain-neutral derivation.
//
// The matching Evidence.fsi is the SOLE visibility declaration — no top-level binding
// here carries private/internal/public (Principle II). The abstract EvidenceGraph<'id>
// is hidden by the signature, not by an access keyword: the .fs gives it a concrete
// representation; the .fsi withholds it, so the ONLY way to obtain one is `build`.
//
// Pure & total (FR-011/FR-013): no I/O, no real-artifact reads — it operates over the
// DECLARED states handed to it. Reinforces decision #4: the dependency structure is a
// DAG; `build` rejects cycles, so `effective` is a terminating least-fixed-point.

type EvidenceState =
    | Pending
    | Real
    | Synthetic
    | Failed
    | Skipped
    | AutoSynthetic

type GraphError<'id> =
    | Cycle of cycle: 'id list
    | UnknownNode of node: 'id
    | AutoSyntheticDeclared of node: 'id

// Hidden representation of the abstract graph: declared state per node + the directed
// dependency edges as an adjacency map ("a rests on b" ⇒ b ∈ Deps[a]). The .fsi keeps
// this opaque, so the DAG invariant `build` establishes cannot be forged.
type EvidenceGraph<'id when 'id: comparison> =
    { Nodes: Map<'id, EvidenceState>
      Deps: Map<'id, Set<'id>> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Evidence =

    // Directed neighbours of `n` — the nodes `n` rests on. Empty when `n` is a sink.
    // Not declared in Evidence.fsi, so the signature hides it (Principle II — visibility
    // is presence/absence in the .fsi, never an access keyword here).
    let restsOn (deps: Map<'id, Set<'id>>) (n: 'id) : Set<'id> =
        deps |> Map.tryFind n |> Option.defaultValue Set.empty

    // First dependency cycle reachable in the adjacency map, as a witnessing loop in
    // dependency order, or None for a DAG. A pure depth-first walk: `onStack` is the
    // current root→node path (most-recent-first), `finished` the fully-explored nodes.
    // A self-loop on "a" witnesses as [ "a" ]; recursion is bounded by the node count.
    let findCycle (ids: 'id list) (deps: Map<'id, Set<'id>>) : 'id list option =
        let witness (onStack: 'id list) (n: 'id) =
            // The segment of onStack from the current head down to (and including) n,
            // re-reversed into dependency order.
            let rec take acc lst =
                match lst with
                | x :: _ when x = n -> List.rev (x :: acc)
                | x :: rest -> take (x :: acc) rest
                | [] -> List.rev acc
            take [] onStack

        let rec visit (onStack: 'id list) (finished: Set<'id>) (n: 'id) : Result<Set<'id>, 'id list> =
            if Set.contains n finished then Ok finished
            elif List.contains n onStack then Error(witness onStack n)
            else
                let stack' = n :: onStack

                let rec loop finished ns =
                    match ns with
                    | [] -> Ok(Set.add n finished)
                    | m :: rest ->
                        match visit stack' finished m with
                        | Error path -> Error path
                        | Ok finished' -> loop finished' rest

                loop finished (restsOn deps n |> Set.toList)

        let rec scan finished ids =
            match ids with
            | [] -> None
            | id :: rest ->
                match visit [] finished id with
                | Error path -> Some path
                | Ok finished' -> scan finished' rest

        scan Set.empty ids

    let build
        (nodes: ('id * EvidenceState) list)
        (dependencies: ('id * 'id) list)
        : Result<EvidenceGraph<'id>, GraphError<'id>> =

        // A repeated id keeps its LAST declaration — the nodes form a Map.
        let nodeMap = nodes |> List.fold (fun m (id, st) -> Map.add id st m) Map.empty

        // Precedence (data-model §build): AutoSyntheticDeclared, then UnknownNode, then Cycle.
        let autoDeclared =
            nodeMap |> Map.tryPick (fun id st -> if st = AutoSynthetic then Some id else None)

        let unknownEndpoint =
            dependencies
            |> List.tryPick (fun (a, b) ->
                if not (Map.containsKey a nodeMap) then Some a
                elif not (Map.containsKey b nodeMap) then Some b
                else None)

        match autoDeclared, unknownEndpoint with
        | Some id, _ -> Error(AutoSyntheticDeclared id)
        | None, Some id -> Error(UnknownNode id)
        | None, None ->
            // Adjacency: edge (a, b) — "a rests on b" — adds b to a's dependency set.
            let deps =
                dependencies
                |> List.fold
                    (fun m (a, b) -> Map.add a (Set.add b (restsOn m a)) m)
                    Map.empty

            match findCycle (nodeMap |> Map.toList |> List.map fst) deps with
            | Some path -> Error(Cycle path)
            | None -> Ok { Nodes = nodeMap; Deps = deps }

    let nodes (graph: EvidenceGraph<'id>) : ('id * EvidenceState) list =
        // Map.toList is de-duplicated and ordered by id — the accessor is order-free.
        graph.Nodes |> Map.toList

    let dependencies (graph: EvidenceGraph<'id>) : ('id * 'id) list =
        // Ordered by (dependent, dependency); Set collapses duplicate edges.
        graph.Deps
        |> Map.toList
        |> List.collect (fun (a, bs) -> bs |> Set.toList |> List.map (fun b -> a, b))

    let effective (graph: EvidenceGraph<'id>) : Map<'id, EvidenceState> =
        // Memoized DFS least-fixed-point: each node's effective state is computed once
        // (O(V + E)) and threaded through an immutable memo — no mutable, no I/O. The
        // recursion terminates because `build` guarantees a DAG.
        let rec eval (memo: Map<'id, EvidenceState>) (n: 'id) : Map<'id, EvidenceState> * EvidenceState =
            match Map.tryFind n memo with
            | Some s -> memo, s
            | None ->
                match graph.Nodes.[n] with
                | Real ->
                    // A Real node is AutoSynthetic iff any dependency is effectively
                    // Synthetic or AutoSynthetic (directly or transitively).
                    let memo', tainted =
                        restsOn graph.Deps n
                        |> Set.toList
                        |> List.fold
                            (fun (m, t) d ->
                                let m', ds = eval m d
                                m', t || ds = Synthetic || ds = AutoSynthetic)
                            (memo, false)

                    let s = if tainted then AutoSynthetic else Real
                    Map.add n s memo', s
                | declared ->
                    // Synthetic (root cause) is reported verbatim; Pending/Failed/Skipped
                    // are inert to taint. (AutoSynthetic is unreachable — build refuses it.)
                    Map.add n declared memo, declared

        graph.Nodes
        |> Map.toList
        |> List.fold (fun memo (n, _) -> fst (eval memo n)) Map.empty
