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
DEFAULT_ROOTS=".claude/skills .codex/skills .agents/skills"   # ADR-0011's three (ADR-0065's one default).

SPECIFY="$REPO_ROOT/.specify"
CLAUDE_MANIFEST="$SPECIFY/integrations/claude.manifest.json"
PRESET_COMMANDS="$SPECIFY/presets/fsharp-opinionated/commands"
EXT_REGISTRY="$SPECIFY/extensions/.registry"

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
if [ -n "${AGENT_SKILL_ROOTS:-}" ]; then
  ROOTS="$AGENT_SKILL_ROOTS"; ROOTS_SRC="\$AGENT_SKILL_ROOTS"
elif [ -f "$REPO_ROOT/.agent-skill-roots" ]; then
  ROOTS="$(sed 's/#.*$//' "$REPO_ROOT/.agent-skill-roots" | tr '\n\t\r' '   ' | tr -s ' ' | sed 's/^ *//; s/ *$//')"
  ROOTS_SRC=".agent-skill-roots"
  [ -n "$ROOTS" ] || die ".agent-skill-roots parses to nothing — a tree that checked it in meant to say something."
else
  ROOTS="$DEFAULT_ROOTS"; ROOTS_SRC="default (ADR-0011's three)"
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

for r in $ROOTS; do
  [ -d "$REPO_ROOT/$r" ] || die "configured root is absent: $r (roots from $ROOTS_SRC)"
done

note "roots: $ROOTS (from $ROOTS_SRC)"
note "producer output root: $OUT_ROOT (derived from $(basename "$CLAUDE_MANIFEST"))"

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
note "attributed union: $n_union skills — projecting into the derived roots."

# ---------------------------------------------------------------------------------------------------
# Step 2 — project the union into every root that is not the producer's own output root.
# ---------------------------------------------------------------------------------------------------
drift=0
changed=0

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

for root in $ROOTS; do
  [ "$root" = "$OUT_ROOT" ] && continue

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
  note "every root holds the attributed union, byte-identically. In sync."
  exit 0
fi

if [ "$changed" -eq 0 ]; then
  note "already in sync — nothing written (the materialize is idempotent)."
else
  note "wrote $changed file(s). Re-run with --check, or a second time, to confirm idempotency."
fi
