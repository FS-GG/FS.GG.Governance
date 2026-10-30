# GOV-423 prerequisite (b): contained kernel-level blocked-read and uncertain-close fixture

Refs FS-GG/FS.GG.SDD#928. This is the source delivery of the fixture and its tests. The
acceptance run belongs to Root in fdev; this document says how to make it.

## Ruling this implements

Governance 423 prerequisite (b) asks for "an authentic regular-file blocked read accepted by
production acquisition, with an independently observed uncertain physical close"
([roadmap](governance-423-neutral-capability-bindings.md)). Root's 2026-10-30 ruling on SDD#928
accepts a contained kernel-level fixture in fdev as authentic when:

1. it exercises the real production path (`openat2`, `statx`, `read`, `close` in
   `ProviderContextCapture.fs`) against a file that `statx` reports as a regular file;
2. the kernel physically produces the block and the close error;
3. the injected cause is recorded explicitly, and is never presented as a field incident.

Teardown (prerequisite (c)) is supplied by fdev plus the SDD#1132 runner and is qualified here
before the uncertain-close case may run.

## Design

The fixture is test code in `tests/FS.GG.Governance.VerifyCommand.Tests`. No product code,
public surface or package changes. The unmodified production `prepare`/`capture`/`release` run
in a `reader` process. Everything else is fixture:

| Role | What it is |
|---|---|
| `scenario` | pid 1 of a private mount + pid namespace (`sudo unshare --mount --pid --fork --kill-child --mount-proc`). Owns setup, injection, ordered teardown, census and the evidence record. |
| `daemon` | Raw-protocol FUSE daemon (no libfuse, gcc or bindings). Mounts with `mount(2)`, serves `INIT`, `LOOKUP`, `GETATTR`, `OPEN`, `READ`, `FLUSH`, `RELEASE`, logs every request and reply with a monotonic timestamp. The wire decisions are pure functions (`PhysicalFixtureFuse.fs`). |
| `reader` | The production capture path against the fixture file. Reports only what production reports (`FirstFailure`, `SecondaryFailures`, `Settlement`, ownership, public `release`). |
| `observer` | A separate process sampling `/proc/<reader>/task/*/{stat,wchan,stack}` every 10 ms with monotonic timestamps. It shares no state with the daemon or reader. |

The test assembly re-enters itself for each role (`--physical-fixture <role>`, see `Main.fs`), so
the fixture needs no second toolchain.

### Modes

* **FUSE mode.** The daemon exposes `provenance.json` as a regular file. `READ` is received and
  left unanswered until the driver writes a release file. `FLUSH` is answered with `EIO`, which
  makes the kernel `close()` of the data descriptor fail. The reading thread parks in
  `request_wait_answer` (state `S`, interruptible until a signal; after a signal the kernel waits
  for the daemon's answer or an abort).
* **Block mode.** A loop device holds an ext4 image; a `dm-delay` target (0 ms) sits on it; the
  device is mounted read-only with `noatime`. Directory and inode are cached by a `stat`, then the
  device is suspended (`dmsetup suspend --nolockfs --noflush`), so the first uncached data read
  sleeps in `D` state (`folio_wait_bit_common` under `filemap_read`). Release swaps the table to
  `error` and resumes, so the read ends with `EIO`.

### Scenarios

| Scenario | Backend | Injection | Purpose |
|---|---|---|---|
| `normal-file` | plain dir | none | production acquisition of a normal file succeeds |
| `fuse-blocked-read` | FUSE | `READ` held | blocked read; teardown by connection abort |
| `block-blocked-read` | loop + dm-delay + ext4 | device suspended | blocked read in `D` state; teardown by error table, `dmsetup remove`, `losetup -d` |
| `fuse-uncertain-close` | FUSE | `READ` held past work end, released inside the cleanup window, `FLUSH` = `EIO` | uncertain close; runs only after both teardowns are qualified |
| `fuse-control-no-block` | FUSE | none | control: the fixture must not pass without the block |
| `block-control-no-block` | loop + dm-delay + ext4 | none | control: same, block layer |

### Independent observation and the judges

`PhysicalFixtureEvidence.fs` holds the evidence record and pure judges. A judge never accepts the
production result alone. For a blocked read it requires all of:

* `FieldIncident = false` and an injected cause stated explicitly;
* `FileType = "regular file"` (coreutils `stat`, which uses `statx`);
* production's classified outcome: first failure `WorkDeadlineReached`, no document accepted,
  settlement `Retained`;
* the daemon log (FUSE) or the device-mapper events (block) join to it: `OPEN` and `READ` from a
  thread the observer saw in the reader, `READ` unanswered at the work deadline, or device
  suspended before capture and held past it;
* the observer saw that thread parked on the cause across the work window (state and kernel
  wait channel, with the kernel stack when readable).

For the uncertain close it additionally requires the daemon's `FLUSH` from the reader answered
with `EIO` after the released `READ`, production classifying `OwnedActivitiesPending`, ownership
recording an uncertain close, and the public `release` refusing.

The FUSE request header carries the requesting task's thread id; the join to the reader is
therefore "a thread the separate observer saw in the reader's task directory".

### Teardown and census

Ordered, timed steps: `release-cause` (connection abort via `/sys/fs/fuse/connections/N/abort`,
or dm error table and resume), `reader-settled`, `unmount`, `dm-remove`, `loop-detach`,
`kill-processes`. Killing pid 1 of the namespace is the final kill. The census runs twice, once
inside the namespace and once from the unprivileged orchestrator after the namespace exited:
D-state tasks, fixture processes, fixture mounts, `fsgg423b-*` dm devices, loop devices backed by
the scratch file, and FUSE connections against the pre-run baseline. Anything left fails the case.

## Tests

`PhysicalFixtureTests.fs` has two tiers.

* **Inert (always run, default CI):** the FUSE wire decisions and the judges, including mutation
  controls (missing `READ`, early reply, no park, wrong thread, no suspension, `S` instead of `D`,
  blank or non-injection cause, relabelled unblocked run, `FLUSH` success, unclassified close,
  surviving mount/dm/loop/D-state/connection, out-of-order or failed teardown steps).
* **Physical (opt-in):** eight cases that run the scenarios above. Default runs show them as
  skipped. With the opt-in set, missing prerequisites fail rather than skip.

## How Root runs the physical cases in fdev

Prerequisites: passwordless `sudo`, `/dev/fuse`, `unshare`, `dmsetup`, `losetup`, `mkfs.ext4`,
and the `dm_delay` module (the run loads it with `sudo modprobe dm_delay`). From a clean checkout
of the PR head with the repository's pinned SDK:

```bash
export FSGG_GOV_PHYSICAL_FIXTURE=1                      # explicit opt-in
export FSGG_GOV_PHYSICAL_EVIDENCE_DIR="$(mktemp -d)"    # keeps <scenario>.evidence.json and census
systemd-run --user --scope -p CPUQuota=300% -p MemoryMax=6G \
  timeout 900 dotnet test tests/FS.GG.Governance.VerifyCommand.Tests -c Debug \
  --filter "FullyQualifiedName~PhysicalFixture" --nologo
```

Each scenario runs once, inside `sudo timeout -s KILL 180 unshare ...`, in a private mktemp
directory. Mounts are namespace-private; dm and loop devices are global kernel objects, which is
why teardown and the census are part of every case and a failsafe removes them if a step fails.
Afterwards check by hand: `sudo dmsetup ls`, `losetup -a`, `grep fsgg423b /proc/mounts` and
`ps -eo stat,cmd | awk '$1 ~ /^D/'` should show nothing from the run.

## What this does and does not establish

It establishes a repeatable, contained, kernel-produced blocked regular-file read and uncertain
close accepted by the production acquisition path, with the injected cause recorded. It is not a
field incident and does not claim one. It does not supply the host composition (a), the real SDD
to Verify to GateExecution acceptance (d), or stronger profiles and C3/C4 (e), and it does not
change any product behaviour. The FUSE blocked read is interruptible `S`, not `D`; the `D`-state
case is the block-layer one.
