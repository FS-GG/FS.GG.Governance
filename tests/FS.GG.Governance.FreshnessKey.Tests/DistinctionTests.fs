module FS.GG.Governance.FreshnessKey.Tests.DistinctionTests

open Expecto
open FS.GG.Governance.FreshnessKey
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.FreshnessKey.Tests.Support

// Single-field distinction for EVERY input category (SC-003, US1 #2–#4): from `baseInputs`, a variant
// differing in exactly one category must yield a different key and `matches = false`. Table-driven over
// all 10 categories, incl. Command present↔absent and the environment-class change.

[<Tests>]
let tests =
    testList
        "Distinction"
        [ for (category, vary) in allCategories ->
              test (sprintf "changing %s ⇒ key differs and matches = false" (Model.categoryToken category)) {
                  let variant = vary baseInputs

                  Expect.notEqual
                      (FreshnessKey.value (FreshnessKey.compute baseInputs))
                      (FreshnessKey.value (FreshnessKey.compute variant))
                      (sprintf "a single change in %A must change the key" category)

                  Expect.isFalse
                      (FreshnessKey.matches baseInputs variant)
                      (sprintf "a single change in %A must forbid reuse" category)
              } ]
