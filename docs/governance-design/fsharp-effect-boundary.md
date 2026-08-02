# F# functional core and effect edge

`fsharp-effect-boundary/v1` checks declared stateful workflows, not names. A reducer may be named
`advance`, `decide`, or `update`; the declaration that it is a stateful transition is the applicable
fact. Pure parsers and validators stay outside the rule, as do a narrowly declared one-shot adapter.

Declare a compiled transition directly above its same-named `let`. Its body ends at the next F# declaration
at the same or lower indentation; later edge I/O is therefore never attributed to the transition. Options
are exact, case-sensitive `key=value` tokens:

- `kind=transition` (the default), `kind=parser`, `kind=validator`, or `kind=thin-adapter`;
- `edge=<symbol>` naming a real `let` in the same compiled source file;
- `success=<message>`, `failure=<message>`, `retry=<policy>`, and `idempotency=<policy>`;
- the all-or-none exemption tuple `exemption-owner=<owner>`,
  `exemption-rationale="<rationale>"`, and `exemption-review-by=YYYY-MM-DD`.

Unknown, duplicate, bare, partial-exemption, missing-symbol, and unresolved-edge declarations fail closed as
malformed input. In particular, `not-edge` is not an alias or substring match for `edge`. Parser, validator,
and thin-adapter kinds are narrow non-applicability controls. The Verify command senses declarations from
compiled project items and folds findings into its normal blocking result.

The pure transition returns requested effects as data. The edge interpreter performs filesystem,
process, environment, clock/randomness, network, UI/host, persistence, or mutable-global work and
returns an explicit success or failure message. A repeatable effect declares both retry and idempotency
semantics. This keeps callback continuations and exceptions from becoming hidden workflow state.

```fsharp
type Message = Saved | SaveFailed of string
type Effect = Persist of string

// fsgg:effect-boundary advance edge=interpret success=Saved failure=SaveFailed retry=never idempotency=document-id
let advance model = model, [ Persist model.Document ] // pure: no File.WriteAllText here

let interpret effect = task {
    match effect with
    | Persist text ->
        try
            do! File.WriteAllTextAsync("document.txt", text)
            return Saved
        with ex ->
            return SaveFailed ex.Message
}
```

An exemption is deliberately exceptional: the three exemption tokens bind the immediately following symbol
and carry a non-empty owner, quoted rationale, and exact unexpired review date. An expired or malformed
exemption is a blocking input-state finding.
