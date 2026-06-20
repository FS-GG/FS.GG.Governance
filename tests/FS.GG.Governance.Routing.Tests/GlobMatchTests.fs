module FS.GG.Governance.Routing.Tests.GlobMatchTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.Routing.Tests.Support

// SC-006 matcher coverage (FR-002): one accepting case per MVP construct + representative
// rejects, over the real public `Glob.matches` / `Glob.checkSyntax`.

let private matches g p = Glob.matches (gp g) (gp p)

[<Tests>]
let tests =
    testList
        "GlobMatch"
        [ test "literal exact match" {
              Expect.isTrue (matches "src/Kernel/Eval.fs" "src/Kernel/Eval.fs") "exact literal matches"
              Expect.isFalse (matches "src/Kernel/Eval.fs" "src/Kernel/Other.fs") "different literal rejects"
          }

          test "? matches exactly one character, not a separator" {
              Expect.isTrue (matches "src/a?c.fs" "src/abc.fs") "? matches one char"
              Expect.isFalse (matches "src/a?c.fs" "src/ac.fs") "? requires a char"
              Expect.isFalse (matches "src/a?c.fs" "src/a/c.fs") "? never matches '/'"
          }

          test "* matches within a single segment only" {
              Expect.isTrue (matches "src/*.fs" "src/Eval.fs") "* matches segment chars"
              Expect.isTrue (matches "src/*" "src/anything" ) "* matches whole segment"
              Expect.isFalse (matches "src/*.fs" "src/a/b.fs") "* does not cross '/'"
          }

          test "** matches zero or more whole segments" {
              Expect.isTrue (matches "src/**" "src/a.fs") "** matches one segment"
              Expect.isTrue (matches "src/**" "src/a/b/c.fs") "** matches deep"
              Expect.isFalse (matches "src/**" "src") "src/** requires the src/ prefix; bare src rejects"
              Expect.isFalse (matches "src/**" "docs/a.fs") "** still anchored under src"
          }

          test "checkSyntax rejects reserved characters, accepts MVP set" {
              Expect.equal (Glob.checkSyntax (gp "src/**")) (Ok()) "MVP glob is supported"
              Expect.equal (Glob.checkSyntax (gp "src/*.fs")) (Ok()) "star/literal supported"
              Expect.equal (Glob.checkSyntax (gp "src/[ab].fs")) (Error()) "character class reserved"
              Expect.equal (Glob.checkSyntax (gp "src/{a,b}.fs")) (Error()) "brace expansion reserved"
              Expect.equal (Glob.checkSyntax (gp "!src/**")) (Error()) "negation reserved"
          } ]
