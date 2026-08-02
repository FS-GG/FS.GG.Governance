# F# functional core and effect edge

`fsharp-effect-boundary/v1` checks declared stateful workflows, not names. A reducer may be named
`advance`, `decide`, or `update`; the declaration that it is a stateful transition is the applicable
fact. Pure parsers and validators stay outside the rule, as do a narrowly declared one-shot adapter.

The pure transition returns requested effects as data. The edge interpreter performs filesystem,
process, environment, clock/randomness, network, UI/host, persistence, or mutable-global work and
returns an explicit success or failure message. A repeatable effect declares both retry and idempotency
semantics. This keeps callback continuations and exceptions from becoming hidden workflow state.

```fsharp
type Message = Saved of Result<unit, string>
type Effect = Persist of string

let advance model = model, [ Persist model.Document ] // pure: no File.WriteAllText here

let interpret = function
    | Persist text ->
        try File.WriteAllText("document.txt", text); Saved(Ok())
        with ex -> Saved(Error ex.Message)
```

An exemption is deliberately exceptional: it binds one symbol and carries an owner, rationale, and
unexpired review date. An expired or malformed exemption is a blocking input-state finding.
