namespace FS.GG.Governance.RuleIdentity

// The 068 per-finding rule-identity leaf. Pure source-prefixing constructors over a single-case newtype:
// each constructor stamps the source class as a leading `<source>:` segment so the five sources are
// disjoint (FR-008) and `ruleIdToken` is the total inverse. No clock/host/env/ordering input (FR-002);
// no hashing; no verdict. No visibility modifiers — the surface is RuleIdentity.fsi (Principle II). The
// shape mirrors the existing `gateIdValue`/`findingIdToken`/`categoryToken` string-token precedents
// (Principle III): the plainest possible newtype + prefixing, no abstraction, no custom operator.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RuleIdentity =

    type RuleId = RuleId of string

    let gate (gateId: string) : RuleId = RuleId("gate:" + gateId)

    let boundary (findingToken: string) : RuleId = RuleId("boundary:" + findingToken)

    let surface (domain: string) (code: string) : RuleId = RuleId("surface:" + domain + ":" + code)

    let release (kindToken: string) : RuleId = RuleId("release:" + kindToken)

    let unattributed (reason: string) : RuleId = RuleId("unattributed:" + reason)

    let ruleIdToken (id: RuleId) : string =
        let (RuleId token) = id
        token
