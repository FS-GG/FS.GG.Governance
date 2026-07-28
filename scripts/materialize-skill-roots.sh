#!/usr/bin/env bash
# materialize-skill-roots.sh — project the producer-authored agent-skill union into ADR-0011's three roots.
#
#   scripts/materialize-skill-roots.sh            # apply: write the union into every derived root
#   scripts/materialize-skill-roots.sh --check     # verify only; exit 1 on drift. No writes.
#   scripts/materialize-skill-roots.sh --list      # print the attributed union and exit 0
#
# WHY THIS EXISTS (FS.GG.Governance#326 / .github#1504)
#
# ADR-0011 requires every agent-skill root — `.claude/skills`, `.codex/skills`, `.agents/skills` — to
# hold the BYTE-IDENTICAL UNION of the skills produced for this repo, so the Claude, Codex and generic
# agent runtimes are interchangeable. This repo did not hold it: 15 skills in `.claude/skills`, 4 in each
# of the other two. Nothing had DRIFTED — every skill present in more than one root was byte-identical —
# eleven PROJECTIONS were simply MISSING, which is why a byte-comparison-only checker saw nothing wrong.
#
# `coordination-coherence` was green throughout, and correctly so: its subject is the KIT-OWNED SUBSET
# (the four `kit:` rows in .github's registry/repos.yml). A co-tenant skill is not in that subset, so the
# ten `speckit-*` skills and `spectre-console` were outside the kit materializer AND outside its
# acceptance check. A `coordination-coherence` green is evidence about four skills; only a `skill-union`
# green is evidence about the tree.
#
# WHY NOT `cp -R .claude/skills .codex/skills`
#
# It produces the same bytes TODAY and reproduces the defect on the next producer change, because nothing
# then knows the roots are DERIVED. This script's projection is gated on the PRODUCER's own declarations,
# so a producer change is detected rather than laundered:
#
#   * it derives the `speckit-*` id set from `.specify/` — it does not read it off `.claude/skills` — so a
#     skill the producer ADDS or REMOVES changes the set, and a skill present in the output root that no
#     producer declares is reported rather than fanned out;
#   * it VERIFIES each declared skill against its producer authority (below) before projecting it, so
#     unverified bytes never reach a derived root.
#
# THE PRODUCER AUTHORITIES, AND WHAT BINDS EACH SKILL
#
# `.claude/skills` is the spec-kit `claude` integration's OWN OUTPUT LOCATION (the integration id is
# "claude" and its manifest's paths are `.claude/skills/...`; this script derives the output root from
# those paths rather than hardcoding it). So it is the producer's tree, not an arbitrary root chosen as a
# donor — which is why it is the SOURCE of the projection and is never WRITTEN by this script. This script
# does not invent producer bytes; regenerating them would make it a second, competing producer.
#
#   1. `speckit-*`, un-overridden (6) — authority: `.specify/integrations/claude.manifest.json`, the
#      content-addressed record of the integration's installation. Checked: sha256(SKILL.md) equals the
#      declared digest.
#   2. `speckit-*`, preset-overridden (3: constitution, implement, tasks) — authority:
#      `.specify/presets/fsharp-opinionated/commands/speckit.<name>.md`. The preset overrides the base
#      integration for exactly these, so `claude.manifest.json`'s digest for them is STALE BY DESIGN and
#      checking it would be a false red. Checked instead: the skill body equals the producer command body,
#      modulo the frontmatter and the leading H1 title (see `producer_core` — each integration rewrites the
#      title: the preset lane PREPENDS `# Speckit <Name> Skill`, the extension lane DROPS the producer's
#      own H1; the body below is verbatim in both).
#   3. `speckit-agent-context-update` (1) — authority:
#      `.specify/extensions/agent-context/commands/speckit.agent-context.update.md`, declared by
#      `.specify/extensions/.registry`. Same body check as (2). Note it is NOT in claude.manifest.json:
#      it arrives via the extension, not the base integration.
#   4. The four kit skills (`cross-repo-coordination`, `intra-repo-parallel-work`, `check-board`,
#      `pnext-item`) — authority: the pinned `FS.GG.Kit` package, materialized by
#      `.config/kit/FS.GG.Kit.receiver.proj -t:FsggKitMaterialize` (ADR-0062) and already bound to
#      canonical in ALL THREE roots by the required `coordination-coherence` gate. This script does not
#      re-verify them — that gate is their check, and duplicating it here would be a restatement that can
#      disagree with it. They are projected as part of the union, which is a no-op while that gate is green.
#   5. `spectre-console` (1) — authority: THIS repo. It is repo-native, authored here by specs 091/093
#      (`metadata.source` records that provenance), so there is no external producer to verify against and
#      `.claude/skills/spectre-console` is itself the authoritative body.
#
# An id in the output root that falls into none of the above is UNATTRIBUTED: reported, and refused rather
# than projected. That is the property `cp -R` cannot have.
#
# THE MANIFEST IS AN INSTALL RECEIPT, NOT A DESIRED STATE (FS.GG.Governance#328)
#
# Authority (2) above says `claude.manifest.json`'s digests for the three preset-overridden skills are
# "stale by design". That was true, and it was knowledge held ONLY in this comment — so every OTHER reader
# of the manifest saw a content-addressed record reporting drift on a CORRECT tree. `overlay.json` is that
# knowledge, PUBLISHED and machine-readable, derived here and asserted by `--check`.
#
# The manifest's rows were NOT rewritten to the post-overlay digests, and that is a deliberate choice with
# evidence behind it:
#
#   * The digests are CORRECT FOR WHAT THEY MEAN. `specify integration status` reads exactly these rows and
#     reports the three as "managed file(s) were modified" — the installer's own vocabulary for "differs
#     from what I laid down". The manifest is a RECEIPT of the install, and a receipt is not a wish.
#   * Rewriting them is NOT REFUSED by the tooling — that was checked, and it is the reason this is a
#     JUDGEMENT and not a constraint. Against the real CLI, on a throwaway copy: rewriting the three digests
#     is accepted, not rejected, and not overwritten on read. So both branches were open and this one was
#     chosen on meaning, not feasibility.
#   * Rewriting them makes the receipt LIE, which is why. In that same experiment, regenerating the digests
#     flips `specify integration status` from WARNING to OK — telling the installer it laid down the preset
#     bytes itself. That discards the only tamper baseline those three files have, and
#     `specify integration uninstall` — documented as "safely preserving modified files" — would then treat
#     preset-authored content as pristine installer output.
#     CAVEAT, since it bounds the claim: the CLI on hand is 0.14.3.dev0 and this project was installed by
#     0.10.3.dev0 (`.specify/init-options.json`). The behaviour above is 0.14.3.dev0's. It was not verified
#     against 0.10.3.dev0, which was not available to run.
#   * Rewriting them is NOT DURABLE. A frozen digest goes stale again the next time the preset's content
#     changes. That is not hypothetical: it already happened once. The manifest has NEVER matched these three
#     files in this repo's history — not even at the bootstrap commit (1766ffe) — and commit 0fe7828 changed
#     all three skills without touching it.
#   * It would also make this script a second producer, which the paragraph above refuses on its own terms.
#
# So the record is annotated rather than rewritten, and the annotation carries NO CONTENT DIGEST. It names
# the AUTHORITY FILE and the CHECK ("body-derivation"). That is what survives the next preset change:
#
#   * preset command CONTENT changes  -> nothing to update; the derivation check still holds.
#   * a preset ADDS a 4th override    -> the derived set grows, `overlay.json` is regenerated, and a tree
#                                        that forgot to regenerate it fails `--check` and the tests.
#   * a preset DROPS an override      -> the row leaves the record and the skill falls back to the
#                                        manifest digest, which is the correct authority again.
#
# Rows the manifest does not list at all (the extension-provided `speckit-agent-context-update`) are
# recorded in the same file under `not_in_manifest`, so "the manifest is not the whole tree" is also a fact
# a consumer can read rather than a surprise it has to survive.
#
# NOT AFFECTED: `speckit.manifest.json`. The preset's `provides:` also `replaces:` two TEMPLATES
# (`constitution-template`, `tasks-template`) whose base copies that manifest records. Those rows are
# accurate and structurally cannot go stale the way the skill rows did, because the template lane RESOLVES
# at read time instead of overlaying bytes: `specify preset resolve constitution-template` answers with the
# preset's own copy while `.specify/templates/` keeps the base bytes the manifest recorded. Only the lane
# that MATERIALIZES bytes into an output root needs an overlay record.
#
# WHAT STILL GATES THE NEXT REGRESSION
#
# This script repairs today's state and can prove itself re-runnable. The DURABLE guard is the
# `skill-union` receiver caller (`.github/workflows/skill-union.yml`) whose `skill-union / skill-union`
# context is required on `main`: any future change that lands a skill into one root only turns the PR red.
# Both halves are needed and neither is sufficient.
#
# Env: AGENT_SKILL_ROOTS  override the root set (default ADR-0011's three), shared with
#                         `coordination-sync` and `skill-union-assert.sh` so the writer and the gate
#                         cannot disagree about which roots exist.
# Exit: 0 = union materialized / already in sync;  1 = drift (--check only);  2 = misconfiguration;
#       3 = a required interpreter is missing.

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$HERE/.." && pwd)"
# THE ROOT SET IS DERIVED FROM THE PINNED KIT DECLARATION, NEVER HARDCODED (FS.GG.Governance#338).
#
# It used to be `.claude/skills .codex/skills .agents/skills` — "ADR-0011's three". Both of the other
# two have since changed disposition underneath that constant and it never learned:
#
#   * ADR-0067 §5 (executed by FS-GG/.github#1636) RETIRED `.codex/skills` from the runtime contract.
#     Projecting into it re-creates a root the contract retired — measured, 11 -> 15 entries, by
#     running the remedy this script's own `--check` prints. ADR-0065 §Retiring a root forbids exactly
#     that.
#   * FS-GG/.github#1748 made `.agents/skills` a VIEW root: generated at checkout, git-ignored, and
#     therefore ABSENT in a fresh clone. The old absent-root check died `exit 2` on untouched `main`
#     for that reason alone, masking the `.codex` drift behind it.
#
# ADR-0065 §A root's three dispositions gives a root THREE states; the constant modelled one. So the
# set is now read from the receiver project that already declares it, and each disposition is
# subtracted from the WRITE set for its own stated reason. A declaration is what a tool can subtract;
# prose is not.
KIT_RECEIVER_PROJ="$REPO_ROOT/.config/kit/FS.GG.Kit.receiver.proj"

SPECIFY="$REPO_ROOT/.specify"
CLAUDE_MANIFEST="$SPECIFY/integrations/claude.manifest.json"
PRESET_DIR="$SPECIFY/presets/fsharp-opinionated"
PRESET_COMMANDS="$PRESET_DIR/commands"
EXT_REGISTRY="$SPECIFY/extensions/.registry"
# The DERIVED, published authority record that tells any consumer which of the manifest's rows the
# manifest itself does not speak for. Written by this script, asserted by --check, and consumed by
# `IntegrationRecordTests` — see "THE MANIFEST IS AN INSTALL RECEIPT" above.
OVERLAY_RECORD="$SPECIFY/integrations/overlay.json"

# The kit-owned subset (.github registry/repos.yml `kit:` rows, kind: skill). Named here ONLY to
# attribute them to `coordination-coherence` rather than leaving them unattributed — this script does not
# check them, so this list cannot drift into a second, disagreeing definition of the kit.
KIT_SKILLS="cross-repo-coordination intra-repo-parallel-work check-board pnext-item"
# Repo-native co-tenants: this repo is the owner, so there is no external producer to verify against.
NATIVE_SKILLS="spectre-console"

die()  { echo "materialize-skill-roots: $*" >&2; exit 2; }
note() { echo "materialize-skill-roots: $*"; }

usage() { awk 'NR == 1 { next } /^#/ { sub(/^# ?/, ""); print; next } { exit }' "$0"; }

mode="apply"
while [ $# -gt 0 ]; do
  case "$1" in
    --check)   mode="check"; shift ;;
    --list)    mode="list";  shift ;;
    -h|--help) usage; exit 0 ;;
    *)         die "unknown argument '$1'. Try --help." ;;
  esac
done

command -v python3 >/dev/null 2>&1 || {
  echo "materialize-skill-roots: python3 is required (reads the producer's JSON manifest and compares" >&2
  echo "  producer bodies). Install python3 — no PyYAML or other module is needed." >&2
  exit 3
}

[ -d "$SPECIFY" ]           || die "no .specify/ in $REPO_ROOT — this repo has no spec-kit producer to drive."
[ -f "$CLAUDE_MANIFEST" ]   || die "producer manifest not found: $CLAUDE_MANIFEST"

# ---------------------------------------------------------------------------------------------------
# Roots. Same precedence as coordination-sync / skill-union-assert.sh: $AGENT_SKILL_ROOTS, else a
# checked-in .agent-skill-roots, else the default. An ABSENT root is a hard error at every level —
# declaring roots narrows what is asked for, it never weakens the answer (#517 / #266).
# ---------------------------------------------------------------------------------------------------
# Reads one MSBuild property out of the receiver project. `;`-separated per MSBuild, space-separated
# out. Absent property -> empty string, which is a legitimate answer (a receiver may declare no view
# and no retired root); an absent FILE is not, and is caught by the caller.
kit_prop() {
  python3 - "$KIT_RECEIVER_PROJ" "$1" <<'PY'
import re, sys
try:
    xml = open(sys.argv[1], encoding="utf-8").read()
except OSError:
    sys.exit(3)
m = re.search(r"<%s>([^<]*)</%s>" % (sys.argv[2], sys.argv[2]), xml)
print(" ".join(p.strip() for p in m.group(1).split(";") if p.strip()) if m else "")
PY
}

VIEW_ROOTS=""; RETIRED_ROOTS=""
if [ -n "${AGENT_SKILL_ROOTS:-}" ]; then
  ROOTS="$AGENT_SKILL_ROOTS"; ROOTS_SRC="\$AGENT_SKILL_ROOTS"
elif [ -f "$REPO_ROOT/.agent-skill-roots" ]; then
  ROOTS="$(sed 's/#.*$//' "$REPO_ROOT/.agent-skill-roots" | tr '\n\t\r' '   ' | tr -s ' ' | sed 's/^ *//; s/ *$//')"
  ROOTS_SRC=".agent-skill-roots"
  [ -n "$ROOTS" ] || die ".agent-skill-roots parses to nothing — a tree that checked it in meant to say something."
elif [ -f "$KIT_RECEIVER_PROJ" ]; then
  # The runtime contract is the UNION of the source and view roots — ADR-0011's two, as ADR-0067 §5
  # narrowed them. Retired roots are read too, but only so the write set can subtract them by name.
  src_roots="$(kit_prop FsggKitSkillRoots)"     || die "could not read FsggKitSkillRoots from $KIT_RECEIVER_PROJ"
  VIEW_ROOTS="$(kit_prop FsggKitViewSkillRoots)" || die "could not read FsggKitViewSkillRoots from $KIT_RECEIVER_PROJ"
  RETIRED_ROOTS="$(kit_prop FsggKitRetiredSkillRoots)" || true
  ROOTS="$(printf '%s %s' "$src_roots" "$VIEW_ROOTS" | tr -s ' ' | sed 's/^ *//; s/ *$//')"
  ROOTS_SRC="the pinned kit declaration ($(basename "$KIT_RECEIVER_PROJ"))"
  [ -n "$ROOTS" ] || die "$KIT_RECEIVER_PROJ declares no FsggKitSkillRoots — the receiver's runtime
  contract is empty, and this script will not guess one (ADR-0065)."
else
  die "no root set: \$AGENT_SKILL_ROOTS is unset, .agent-skill-roots is absent, and there is no
  $KIT_RECEIVER_PROJ to read the declaration from. This script no longer carries a hardcoded default —
  a constant is what let a retired root (.codex/skills) and a generated view root (.agents/skills)
  stay in the write set after the contract changed (FS.GG.Governance#338, ADR-0067 §5)."
fi

# ---------------------------------------------------------------------------------------------------
# The producer OUTPUT ROOT, derived from the manifest's own paths rather than hardcoded: the `claude`
# integration writes where its manifest says it wrote.
# ---------------------------------------------------------------------------------------------------
OUT_ROOT="$(python3 - "$CLAUDE_MANIFEST" <<'PY'
import json, sys, os, posixpath
m = json.load(open(sys.argv[1], encoding="utf-8"))
roots = set()
for p in m.get("files", {}):
    parts = p.split("/")
    if len(parts) >= 3 and parts[1] == "skills":
        roots.add(posixpath.join(parts[0], parts[1]))
if len(roots) != 1:
    sys.stderr.write(f"expected exactly one skill output root in the manifest, found {sorted(roots)}\n")
    sys.exit(2)
print(roots.pop())
PY
)" || die "could not derive the producer's skill output root from $CLAUDE_MANIFEST"

[ -d "$REPO_ROOT/$OUT_ROOT" ] || die "producer output root is absent: $OUT_ROOT"

case " $ROOTS " in
  *" $OUT_ROOT "*) ;;
  *) die "the producer output root ($OUT_ROOT) is not in the root set ($ROOTS, from $ROOTS_SRC) — refusing
  to project bytes into roots while the tree that produces them is not itself asserted." ;;
esac

# ---------------------------------------------------------------------------------------------------
# THE WRITE SET, WHICH IS NARROWER THAN THE CONTRACT SET. Three subtractions, three different reasons:
#
#   the SOURCE   — the producer's own output root. Regenerating it would make this script a second,
#                  competing producer (see the header). Never written; already excluded below.
#   a VIEW root  — its content is GENERATED by `scripts/skill-view`, never transported. Writing into
#                  one either lands on the source through a symlink, or (under `--mode copy`, the
#                  documented Windows fallback) re-creates the very copy the view exists to remove.
#   a RETIRED root — withdrawn from the runtime contract. ADR-0065 §Retiring a root: a receiver never
#                  hand-re-materializes one.
#
# A view root stays in $ROOTS, because it IS in the runtime contract and the attribution/agreement
# pass below must still see it. Only the WRITES narrow. Do not fold it into the retired set.
# ---------------------------------------------------------------------------------------------------
in_list() { case " $2 " in *" $1 "*) return 0 ;; *) return 1 ;; esac; }

WRITE_ROOTS=""
for r in $ROOTS; do
  [ "$r" = "$OUT_ROOT" ] && continue
  in_list "$r" "$VIEW_ROOTS"    && continue
  in_list "$r" "$RETIRED_ROOTS" && continue
  WRITE_ROOTS="$WRITE_ROOTS $r"
done
WRITE_ROOTS="$(printf '%s' "$WRITE_ROOTS" | sed 's/^ *//; s/ *$//')"

# An absent root is still a hard error (#517 / #266) — EXCEPT a view root, which is git-ignored and
# therefore absent in every fresh checkout by construction (ADR-0067 §6.1). Its visibility is
# `FsggKitCheckSkillView` / `scripts/skill-view check`'s subject, not this script's, and dying on it
# here made `--check` exit 2 on an untouched `main`.
for r in $ROOTS; do
  [ -d "$REPO_ROOT/$r" ] && continue
  if in_list "$r" "$VIEW_ROOTS"; then
    note "view root not generated in this tree: $r — not an error here (ADR-0067 §6.1); its visibility
  belongs to \`scripts/skill-view check\`. Generate it with:  bash scripts/skill-view generate --source $OUT_ROOT --roots \"$r\""
  else
    die "configured root is absent: $r (roots from $ROOTS_SRC)"
  fi
done

note "roots: $ROOTS (from $ROOTS_SRC)"
note "producer output root: $OUT_ROOT (derived from $(basename "$CLAUDE_MANIFEST")) — the SOURCE, never written"
[ -n "$VIEW_ROOTS" ]    && note "view roots: $VIEW_ROOTS — in the contract, GENERATED, not written here"
[ -n "$RETIRED_ROOTS" ] && note "retired roots: $RETIRED_ROOTS — withdrawn from the contract, not written, not swept here"
if [ -n "$WRITE_ROOTS" ]; then
  note "write set: $WRITE_ROOTS"
else
  # Say it out loud. A projection with no target that reports nothing is a pass that asserted
  # nothing — epic #266's shape, and the exact failure this repo keeps paying for.
  note "write set: EMPTY — every declared root is either the source or generated, so this script
  projects nothing in this tree. That is the ADR-0067 §5 end state (one source + one generated view),
  not a silent success: the verification and authority-record steps below still run and still grade."
fi

# ---------------------------------------------------------------------------------------------------
# Step 1 — verify the `.specify/`-produced set against its producer authority, and attribute every id in
# the output root. Emits TSV: <id>\t<class>\t<verdict>\t<detail>
# ---------------------------------------------------------------------------------------------------
ATTRIB="$(python3 - "$REPO_ROOT" "$OUT_ROOT" "$CLAUDE_MANIFEST" "$PRESET_COMMANDS" "$EXT_REGISTRY" \
                   "$KIT_SKILLS" "$NATIVE_SKILLS" <<'PY'
import hashlib, json, os, re, sys

repo, out_root, manifest_p, preset_dir, ext_registry, kit_s, native_s = sys.argv[1:8]
kit = set(kit_s.split())
native = set(native_s.split())
out = os.path.join(repo, out_root)

def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()

def strip_frontmatter(t):
    if t.startswith("---\n"):
        i = t.find("\n---", 3)
        if i != -1:
            return t[i + 4:]
    return t

def producer_core(t):
    """The body a producer command and its generated SKILL.md share.

    Drops the YAML frontmatter (each integration writes its own) and the leading run of blank lines and
    H1 title lines (each integration rewrites the title: the preset lane prepends its own H1 above the
    producer's, the extension lane drops the producer's). Everything below is verbatim, so comparing this
    is a real derivation check and not a fuzzy match. Trailing newlines are normalized because the
    installer is inconsistent about the final one.
    """
    b = strip_frontmatter(t).replace("\r\n", "\n")
    lines = b.split("\n")
    i = 0
    while i < len(lines) and (lines[i].strip() == "" or re.match(r"^#\s", lines[i])):
        i += 1
    return "\n".join(lines[i:]).rstrip("\n") + "\n"

def sha(p):
    with open(p, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()

# --- producer declarations ------------------------------------------------------------------------
manifest = json.load(open(manifest_p, encoding="utf-8"))
declared = {}   # id -> declared sha256 of its SKILL.md
for p, d in manifest.get("files", {}).items():
    parts = p.split("/")
    if len(parts) == 4 and parts[1] == "skills" and parts[3] == "SKILL.md":
        declared[parts[2]] = d

# preset overrides: .specify/presets/<preset>/commands/speckit.<name>.md  ->  speckit-<name>
overrides = {}
if os.path.isdir(preset_dir):
    for fn in sorted(os.listdir(preset_dir)):
        m = re.fullmatch(r"speckit\.([A-Za-z0-9._-]+)\.md", fn)
        if m:
            overrides["speckit-" + m.group(1).replace(".", "-")] = os.path.join(preset_dir, fn)

# extension-provided commands, read from the extensions registry (not from a directory walk: the
# registry is what says an extension is ENABLED and which commands it registered).
ext_cmds = {}
if os.path.isfile(ext_registry):
    reg = json.load(open(ext_registry, encoding="utf-8"))
    for ext_id, ext in (reg.get("extensions") or {}).items():
        if not ext.get("enabled", False):
            continue
        ext_dir = os.path.join(os.path.dirname(ext_registry), ext_id)
        ymlp = os.path.join(ext_dir, "extension.yml")
        # The command file is declared in extension.yml's `provides.commands[].file`. Parsed with a
        # narrow line scan rather than a YAML library on purpose: PyYAML is not a dependency of this
        # repo's tooling, and the two keys needed are flat scalars.
        files = re.findall(r"^\s*file:\s*(\S+)\s*$", read(ymlp), re.M) if os.path.isfile(ymlp) else []
        for rel in files:
            base = os.path.basename(rel)
            m = re.fullmatch(r"speckit\.([A-Za-z0-9._-]+)\.md", base)
            if m:
                sid = "speckit-" + m.group(1).replace(".", "-")
                ext_cmds[sid] = os.path.join(ext_dir, rel)

present = sorted(d for d in os.listdir(out) if os.path.isdir(os.path.join(out, d)))
rows = []

def emit(sid, cls, verdict, detail):
    rows.append("\t".join((sid, cls, verdict, detail)))

# every producer-declared id must be present in the output root
producer_ids = set(declared) | set(overrides) | set(ext_cmds)
for sid in sorted(producer_ids - set(present)):
    emit(sid, "speckit", "ABSENT", "declared by the producer but missing from " + out_root)

for sid in present:
    skill_md = os.path.join(out, sid, "SKILL.md")
    if sid in overrides or sid in ext_cmds:
        src = overrides.get(sid) or ext_cmds[sid]
        cls = "speckit/preset" if sid in overrides else "speckit/extension"
        if not os.path.isfile(skill_md):
            emit(sid, cls, "NO-SKILL-MD", skill_md)
        elif producer_core(read(skill_md)) == producer_core(read(src)):
            emit(sid, cls, "derived", os.path.relpath(src, repo))
        else:
            emit(sid, cls, "UNDERIVED", "body differs from " + os.path.relpath(src, repo))
    elif sid in declared:
        if not os.path.isfile(skill_md):
            emit(sid, "speckit/base", "NO-SKILL-MD", skill_md)
        else:
            a = sha(skill_md)
            if a == declared[sid]:
                emit(sid, "speckit/base", "digest-ok", declared[sid][:12])
            else:
                emit(sid, "speckit/base", "DRIFTED",
                     f"declared {declared[sid][:12]} actual {a[:12]} in {os.path.basename(manifest_p)}")
    elif sid in kit:
        emit(sid, "kit", "kit-owned", "FS.GG.Kit / coordination-coherence")
    elif sid in native:
        emit(sid, "native", "repo-owned", "authored in this repo")
    else:
        emit(sid, "?", "UNATTRIBUTED", "no producer declares this id")

print("\n".join(rows))
PY
)" || die "producer attribution failed."

printf '%s\n' "$ATTRIB" | while IFS=$'\t' read -r sid cls verdict detail; do
  [ -n "$sid" ] || continue
  printf '  %-32s %-20s %-14s %s\n' "$sid" "$cls" "$verdict" "$detail"
done

# ---------------------------------------------------------------------------------------------------
# Step 1b — DERIVE the published authority record (`overlay.json`).
#
# Derived from the producers under `.specify/` on every run, never read back as input: this script's
# answer about who owns a skill does not depend on the file it publishes, so the record cannot bootstrap
# its own mistake. `--check` asserts the published copy equals this derivation; `apply` rewrites it.
#
# The record deliberately carries NO CONTENT DIGEST — see the header. It names the authority file and the
# check, which is what makes a preset CONTENT change a no-op here instead of a re-staling event.
# ---------------------------------------------------------------------------------------------------
DERIVED_OVERLAY="$(python3 - "$REPO_ROOT" "$CLAUDE_MANIFEST" "$PRESET_DIR" "$EXT_REGISTRY" "$OUT_ROOT" <<'PY'
import json, os, re, sys

# OUT_ROOT is passed in rather than re-derived from the manifest. The block above already derived it and
# REFUSED a manifest that names more than one skill root; re-deriving here would be a second definition
# of the same fact, free to disagree with the first — the exact failure mode the KIT_SKILLS note warns
# about. One derivation, one answer.
repo, manifest_p, preset_dir, ext_registry, out_root = sys.argv[1:6]
preset_cmds = os.path.join(preset_dir, "commands")
preset_id = os.path.basename(preset_dir)

def rel(p):
    return os.path.relpath(p, repo).replace(os.sep, "/")

def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()

# The preset's overrides, taken from the commands it actually ships AND cross-checked against what
# `preset.yml` DECLARES it replaces. Two sources that must agree: a command file nobody declares, or a
# declaration with no file behind it, is a producer defect and is reported rather than published.
shipped = {}
if os.path.isdir(preset_cmds):
    for fn in sorted(os.listdir(preset_cmds)):
        m = re.fullmatch(r"speckit\.([A-Za-z0-9._-]+)\.md", fn)
        if m:
            shipped["speckit-" + m.group(1).replace(".", "-")] = os.path.join(preset_cmds, fn)

declared_yml = set()
preset_yml = os.path.join(preset_dir, "preset.yml")
if os.path.isfile(preset_yml):
    # Narrow line scan, for the same reason the extension lane uses one: PyYAML is not a dependency of
    # this repo's tooling. Only `commands/` files are overrides; the preset's `templates/` entries are
    # resolved at read time and never overlay the tree (header).
    #
    # The quotes are NOT optional decoration to skip over: preset.yml writes `file: "commands/x.md"` and
    # a pattern anchored on a bare `commands/` matched NOTHING, which made the agreement check below
    # vacuous — it silently passed on a set it had never read. Strip an optional quote on each side.
    for f in re.findall(r"""^\s*file:\s*["']?(commands/[^"'\s]+)["']?\s*$""", read(preset_yml), re.M):
        m = re.fullmatch(r"speckit\.([A-Za-z0-9._-]+)\.md", os.path.basename(f))
        if m:
            declared_yml.add("speckit-" + m.group(1).replace(".", "-"))

if not declared_yml:
    # A preset that ships command files must declare them. An empty parse against a non-empty commands/
    # directory means the scan stopped matching, not that the preset declares nothing — fail loudly
    # rather than publish a record derived from a set nobody checked.
    if shipped:
        sys.stderr.write(
            "parsed no `file: commands/...` entries from %s, but %s/ ships %d command(s): %s\n"
            % (rel(preset_yml), rel(preset_cmds), len(shipped), sorted(shipped))
        )
        sys.exit(2)

if declared_yml != set(shipped):
    sys.stderr.write(
        "preset.yml and %s/ disagree about the override set:\n  declared only: %s\n  shipped only:  %s\n"
        % (rel(preset_cmds), sorted(declared_yml - set(shipped)) or "-", sorted(set(shipped) - declared_yml) or "-")
    )
    sys.exit(2)

# Extension-provided commands, from the registry that says which are ENABLED (same source as attribution).
ext_cmds = {}
if os.path.isfile(ext_registry):
    reg = json.load(open(ext_registry, encoding="utf-8"))
    for ext_id, ext in (reg.get("extensions") or {}).items():
        if not ext.get("enabled", False):
            continue
        ext_dir = os.path.join(os.path.dirname(ext_registry), ext_id)
        ymlp = os.path.join(ext_dir, "extension.yml")
        # Quote-tolerant, unlike the attribution scan above: a value this scan fails to read drops a row
        # from `not_in_manifest`, which fails OPEN (a consumer is told nothing rather than told wrong).
        # The attribution scan can afford the stricter pattern because a miss there makes the skill
        # UNATTRIBUTED and refuses the whole projection.
        files = re.findall(r"""^\s*file:\s*["']?([^"'\s]+)["']?\s*$""", read(ymlp), re.M) if os.path.isfile(ymlp) else []
        for r in files:
            m = re.fullmatch(r"speckit\.([A-Za-z0-9._-]+)\.md", os.path.basename(r))
            if m:
                ext_cmds["speckit-" + m.group(1).replace(".", "-")] = os.path.join(ext_dir, r)

manifest = json.load(open(manifest_p, encoding="utf-8"))
rows = {}          # skill id -> manifest path
for p in manifest.get("files", {}):
    parts = p.split("/")
    if len(parts) == 4 and parts[1] == "skills" and parts[3] == "SKILL.md":
        rows[parts[2]] = p

# One producer per id, and the PRESET WINS a collision — the same precedence the attribution block applies
# (`overrides.get(sid) or ext_cmds[sid]`). Merging the two dicts blind would let iteration order decide,
# and the record would then name a different authority than the check that enforces it.
producers = dict(ext_cmds)
producers.update(shipped)

superseded, not_in_manifest = {}, {}
for sid, src in sorted(producers.items()):
    kind = "preset-command" if sid in shipped else "extension-command"
    entry = {"authority": rel(src), "authority_kind": kind, "check": "body-derivation"}
    if kind == "preset-command":
        entry["preset"] = preset_id
    if sid in rows:
        entry["manifest_row"] = "pre-overlay"
        superseded[rows[sid]] = entry
    else:
        not_in_manifest["%s/%s/SKILL.md" % (out_root, sid)] = entry

record = {
    "schema_version": "1.0",
    "generated_by": "scripts/materialize-skill-roots.sh",
    "manifest": os.path.basename(manifest_p),
    "manifest_semantics": "install-receipt",
    "note": (
        "%s records the bytes the spec-kit installer LAID DOWN, not the bytes that SHOULD be present. "
        "For every path under `superseded` the installed bytes were then replaced by an overlay, so the "
        "manifest's digest for that path is pre-overlay and comparing it to the tree is a FALSE RED. "
        "Check those paths against `authority` instead, using `check`: `body-derivation` means the "
        "SKILL.md body equals the authority command's body once each side's YAML frontmatter and leading "
        "H1 title lines are dropped. Paths under `not_in_manifest` are produced for this output root but "
        "are not manifest rows at all, so the manifest is not a complete inventory of the root. Rows in "
        "neither list are the manifest's own, and their digests are authoritative. This file carries no "
        "content digest on purpose: it names WHO owns each path, so a change to an authority's CONTENT "
        "needs no update here. Derived from .specify/ — regenerate with "
        "`scripts/materialize-skill-roots.sh` and assert with `--check`."
    ) % os.path.basename(manifest_p),
    "superseded": superseded,
    "not_in_manifest": not_in_manifest,
}
print(json.dumps(record, indent=2, sort_keys=True))
PY
)" || die "could not derive the overlay authority record."

# Compared as BYTES, through a file, not as `[ "$(cat …)" = "$DERIVED_OVERLAY" ]`. Command substitution
# strips trailing newlines from BOTH sides, so a record whose final newline was lost compared EQUAL while
# `apply` would still rewrite it — `--check` green on a tree the writer would change is precisely the
# idempotency claim this script makes ("re-run with --check, or a second time, to confirm idempotency").
OVERLAY_TMP="$(mktemp)"
trap 'rm -f "$OVERLAY_TMP"' EXIT
printf '%s\n' "$DERIVED_OVERLAY" > "$OVERLAY_TMP"

overlay_stale=0
if [ -f "$OVERLAY_RECORD" ] && cmp -s "$OVERLAY_TMP" "$OVERLAY_RECORD"; then
  note "authority record: $(basename "$OVERLAY_RECORD") agrees with the producers."
else
  overlay_stale=1
  if [ -f "$OVERLAY_RECORD" ]; then
    note "authority record: $(basename "$OVERLAY_RECORD") does NOT match the producers under .specify/"
  else
    note "authority record: $(basename "$OVERLAY_RECORD") is MISSING — nothing tells a consumer which manifest rows are pre-overlay"
  fi
fi

# `--list` is a REPORT: the table above is its whole output, and it must stop here — before the
# projection below, which WRITES. (It used to fall through and materialize, so the one mode documented
# as read-only was the one that silently applied.) Its verdicts are on the rows, so it makes no claim of
# its own and needs no gate.
if [ "$mode" = "list" ]; then
  exit 0
fi

bad="$(printf '%s\n' "$ATTRIB" | awk -F'\t' '$3 ~ /^(ABSENT|UNDERIVED|DRIFTED|UNATTRIBUTED|NO-SKILL-MD)$/ { print $1 " [" $3 "]" }')"
if [ -n "$bad" ]; then
  echo "materialize-skill-roots: refusing to project — the producer authority is not satisfied:" >&2
  # Quoted: each verdict is a LINE ("<id> [VERDICT]"), and an unquoted expansion word-split it into one
  # token per line — garbling the one output a failure path exists to produce.
  printf '%s\n' "$bad" | sed 's/^/  /' >&2
  echo "Fix the producer (or its manifest) first; projecting unverified bytes is what this script exists" >&2
  echo "to prevent. If a verdict is UNATTRIBUTED the id is real but this script cannot name its producer:" >&2
  echo "  * a newly added kit skill        -> add it to KIT_SKILLS at the top of this script;" >&2
  echo "  * a new repo-native co-tenant    -> add it to NATIVE_SKILLS;" >&2
  echo "  * anything else                  -> it has no producer, which is the finding." >&2
  exit 2
fi

UNION="$(printf '%s\n' "$ATTRIB" | awk -F'\t' 'NF { print $1 }' | sort -u)"
n_union="$(printf '%s\n' "$UNION" | grep -c . || true)"
if [ -n "$WRITE_ROOTS" ]; then
  note "attributed union: $n_union skills — projecting into: $WRITE_ROOTS"
else
  note "attributed union: $n_union skills — verified and attributed, projected NOWHERE (write set is empty)."
fi

# ---------------------------------------------------------------------------------------------------
# Step 2 — project the union into every root that is not the producer's own output root.
# ---------------------------------------------------------------------------------------------------
drift=0
changed=0

# The published authority record travels with the projection: `apply` brings it into line, `--check`
# calls a stale or absent one drift. Deferred to here — past the refusal gate — so a run that refuses to
# project does not leave a rewritten record behind as its only effect.
if [ "$overlay_stale" -ne 0 ]; then
  if [ "$mode" = "check" ]; then
    echo "  drift: $(basename "$OVERLAY_RECORD")" >&2
    drift=1
  else
    cp -- "$OVERLAY_TMP" "$OVERLAY_RECORD"
    changed=$((changed + 1))
    note "wrote $(basename "$OVERLAY_RECORD")."
  fi
fi

# sync_file <src> <dst>: byte-identical content plus the executable bit.
sync_file() {
  local src="$1" dst="$2" sx dx
  sx=$([ -x "$src" ] && echo x || echo -)
  if [ -f "$dst" ] && cmp -s "$src" "$dst"; then
    dx=$([ -x "$dst" ] && echo x || echo -)
    [ "$sx" = "$dx" ] && return 0
  fi
  if [ "$mode" = "check" ]; then
    echo "  drift: $dst" >&2; drift=1; return 0
  fi
  mkdir -p "$(dirname "$dst")"
  cp -- "$src" "$dst"
  if [ "$sx" = x ]; then chmod +x "$dst"; else chmod -x "$dst"; fi
  changed=$((changed + 1))
}

for root in $WRITE_ROOTS; do

  # prune: skill dirs in this root that the attributed union does not contain
  for d in "$REPO_ROOT/$root"/*/; do
    [ -d "$d" ] || continue
    sid="$(basename "$d")"
    if ! printf '%s\n' "$UNION" | grep -Fqx "$sid"; then
      if [ "$mode" = "check" ]; then
        echo "  stale (not in the union): $root/$sid" >&2; drift=1
      else
        rm -rf -- "$d"; changed=$((changed + 1)); note "pruned $root/$sid"
      fi
    fi
  done

  # project each skill in the union, file by file, and prune files the producer no longer has
  while IFS= read -r sid; do
    [ -n "$sid" ] || continue
    srcdir="$REPO_ROOT/$OUT_ROOT/$sid"
    dstdir="$REPO_ROOT/$root/$sid"
    while IFS= read -r rel; do
      [ -n "$rel" ] || continue
      sync_file "$srcdir/$rel" "$dstdir/$rel"
    done < <(cd "$srcdir" && find . -type f | sed 's|^\./||' | sort)

    [ -d "$dstdir" ] || continue
    while IFS= read -r rel; do
      [ -n "$rel" ] || continue
      if [ ! -f "$srcdir/$rel" ]; then
        if [ "$mode" = "check" ]; then
          echo "  stale file (not in the producer's skill): $root/$sid/$rel" >&2; drift=1
        else
          rm -f -- "$dstdir/$rel"; changed=$((changed + 1))
        fi
      fi
    done < <(cd "$dstdir" && find . -type f | sed 's|^\./||' | sort)

    # Remove directories the file pruning above emptied. `-mindepth 1` so the skill's OWN directory is
    # never the thing deleted: a skill that legitimately has no files would otherwise be removed here and
    # then reported as a partition on the next --check.
    [ "$mode" = "check" ] || find "$dstdir" -mindepth 1 -type d -empty -delete 2>/dev/null || true
  done <<< "$UNION"
done

if [ "$mode" = "check" ]; then
  if [ "$drift" -ne 0 ]; then
    echo "materialize-skill-roots: the derived roots are NOT the producer-authored union. Re-run" >&2
    echo "  scripts/materialize-skill-roots.sh   (no flags) and commit the result." >&2
    exit 1
  fi
  if [ -n "$WRITE_ROOTS" ]; then
    note "every derived root holds the attributed union, byte-identically. In sync."
  else
    # NOT "in sync" — there is nothing to be in sync WITH. Reporting a byte-identity verdict over an
    # empty write set would be a gate that cannot fail claiming it passed (#266). What DID grade
    # here is the producer verification and the authority record, and only those are claimed.
    note "no derived root to compare: the write set is empty, so no byte-identity verdict is made here.
  The producer verification and the overlay.json authority record above DID run and passed; the view
  root's visibility is \`scripts/skill-view check\`'s verdict, not this one."
  fi
  exit 0
fi

if [ "$changed" -eq 0 ]; then
  note "already in sync — nothing written (the materialize is idempotent)."
else
  note "wrote $changed file(s). Re-run with --check, or a second time, to confirm idempotency."
fi
