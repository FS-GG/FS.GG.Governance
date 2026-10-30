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

Teardown is performed by the fixture's own pid-1 driver inside the private namespace (connection
abort, dm error table and removal, loop detach, namespace kill) and is qualified here before the
uncertain-close case may run. A janitor outside the namespace restores a clean state after an
abnormal exit. Who is the accountable supplier of that teardown for prerequisite (c) is Root's
decision; this fixture does not claim the SDD#1132 runner performs it.

## Design

The fixture is test code in `tests/FS.GG.Governance.VerifyCommand.Tests`. No product code,
public surface or package changes. The unmodified production `prepare`/`capture`/`release` run
in a `reader` process. Everything else is fixture:

| Role | What it is |
|---|---|
| `scenario` | pid 1 of a private mount + pid namespace (`sudo unshare --mount --pid --fork --kill-child --mount-proc`). Owns setup, injection, ordered teardown, census and the evidence record. |
| `daemon` | Raw-protocol FUSE daemon (no libfuse, gcc or bindings). Mounts with `mount(2)`, serves `INIT`, `LOOKUP`, `GETATTR`, `OPEN`, `READ`, `FLUSH`, `RELEASE`, logs every request and reply with a monotonic timestamp. The wire decisions are pure functions (`PhysicalFixtureFuse.fs`). |
| `reader` | The production capture path against the fixture file, run as the invoking user (`setpriv`, the uid recorded in the evidence) while the other roles stay root. Reports only what production reports (`FirstFailure`, `SecondaryFailures`, `Settlement`, ownership, public `release`). |
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
  sleeps in `D` state (`folio_wait_bit_common` under `filemap_read`; this is the `TASK_KILLABLE`
  flavour, so a fatal signal can end the task but the device stays suspended until released).
  The judge accepts a block-layer park only when the sampled kernel stack shows `ksys_read` or
  `vfs_read`; a park in another syscall, or an unreadable stack, is not proof. Release swaps the
  table to `error` and resumes, so the read ends with `EIO`.

### Scenarios

| Scenario | Backend | Injection | Purpose |
|---|---|---|---|
| `normal-file` | plain dir | none | production acquisition of a normal file succeeds |
| `fuse-blocked-read` | FUSE | `READ` held | blocked read; teardown by connection abort |
| `block-blocked-read` | loop + dm-delay + ext4 | device suspended | blocked read in `D` state; teardown by error table, `dmsetup remove`, `losetup -d` |
| `fuse-uncertain-close` | FUSE | `READ` held past work end, released inside the cleanup window, `FLUSH` = `EIO` | uncertain close; runs only after both teardowns are qualified |
| `fuse-control-no-block` | FUSE | none | control: the fixture must not pass without the block |
| `block-control-no-block` | loop + dm-delay + ext4 | none | control: same, block layer |
| `block-abnormal-exit` | loop + dm-delay + ext4 | device suspended, then the driver exits abruptly | janitor control: devices leak, the janitor restores a clean census |

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
  wait channel, with the kernel stack), and its 250 ms heartbeats cover the whole span with no
  gap above one second, so a stalled observer cannot look like a sustained park;
* the evidence names the scenario's fixed cause text exactly (a negated or renamed cause fails)
  and carries the commit and tree of a clean checkout (`Head`, `Tree`, `Dirty=false`).

For the uncertain close it additionally requires the daemon's `FLUSH` from the reader answered
with `EIO` after the released `READ`, production classifying `OwnedActivitiesPending`, ownership
recording an uncertain close, and the public `release` refusing. The uncertain-close judge also
folds in the whole blocked-read judge, so a close error without a proven held `READ` is refused.

The FUSE request header carries the requesting task's thread id; the join to the reader is
therefore "a thread the separate observer saw in the reader's task directory".

### Teardown and census

Ordered, timed steps: `release-cause` (connection abort via `/sys/fs/fuse/connections/N/abort`,
or dm error table and resume), `reader-settled`, `unmount`, `dm-remove`, `loop-detach`,
`kill-processes`. Killing pid 1 of the namespace is the final kill. The census runs twice, once
inside the namespace and once from the unprivileged orchestrator after the namespace exited:
D-state tasks, fixture processes, fixture mounts, `fsgg423b-*` dm devices (and their
`/sys/block/dm-*/inflight`), loop devices backed by the scratch file, and FUSE connections against
the pre-run baseline. Anything left fails the case.

**Janitor.** dm and loop devices are global kernel objects; a namespace that dies abnormally (OOM
kill, scope stop, a hang past the hard kill) leaves them behind. `runPhysically` runs the janitor
before each run and again after it, and takes the outside census on every exit path, including a run
that wrote no evidence. For each `fsgg423b-` dm device the janitor loads the error table, resumes it
and removes it with retries, then detaches every loop device backed under a `/fsgg423b-` scratch
directory. Internal deadlines are budgeted under the 180 s hard kill: 75 s for the working phase,
150 s for teardown waits, 155 s for any tool.

## Tests

`PhysicalFixtureTests.fs` has two tiers.

* **Inert (always run, default CI):** the FUSE wire decisions and the judges, including mutation
  controls (missing `READ`, early reply, no park, wrong thread, no suspension, `S` instead of `D`,
  blank, negated or foreign cause, a park inside an `openat2` lookup, an unreadable stack, a stalled
  observer, an unbound or dirty checkout, a close error without a held `READ`, relabelled unblocked run, `FLUSH` success, unclassified close,
  surviving mount/dm/loop/D-state/connection, out-of-order or failed teardown steps).
* **Physical (opt-in):** ten cases that run the scenarios above, including a janitor control that
  kills the driver while the read is parked, asserts that devices really leaked, and asserts that
  the janitor restores a clean census. Observed: pid 1 cannot finish exiting while its reader is
  parked on the suspended device, so the command hangs until the control's 40 s kill; the dm device
  and the loop device (reported as `/fsgg423b-XXXX/backing.img` by a hung namespace) leak until the
  janitor loads the error table, resumes and removes them. Default runs show them as
  skipped. With the opt-in set, missing prerequisites fail rather than skip.

## How Root runs the physical cases in fdev

Prerequisites: passwordless `sudo`, `/dev/fuse`, `unshare`, `dmsetup`, `losetup`, `mkfs.ext4`,
and the `dm_delay` module (the run loads it with `sudo modprobe dm_delay`). Run one physical run at
a time: the dm and loop tables are global and the janitor treats every `fsgg423b-` device as its own.
This is enforced, not just advised: every run, and the janitor control, holds an exclusive host
`flock` on `/run/lock/fsgg423b.lock` (override with `FSGG_GOV_PHYSICAL_LOCK`; the file is created
world-writable through `sudo` when needed). A second runner raises `PhysicalRunBusy` and touches no
device. A physical control proves it by holding the lock while a stand-in "live" dm and loop device
exist, asserting both entry points refuse and the devices survive.
Use a clean checkout (the evidence records `Head`, `Tree` and `Dirty`, and the judges refuse a dirty
tree) and the repository's pinned SDK, which is at `~/.dotnet-10.0.403` in fdev:

```bash
export PATH="$HOME/.dotnet-10.0.403:$PATH" DOTNET_ROOT="$HOME/.dotnet-10.0.403"
export FSGG_GOV_PHYSICAL_FIXTURE=1                      # explicit opt-in
export FSGG_GOV_PHYSICAL_EVIDENCE_DIR="$(mktemp -d)"    # keeps <scenario>.evidence.json and census
systemd-run --user --scope -p CPUQuota=300% -p MemoryMax=6G \
  timeout 900 dotnet test tests/FS.GG.Governance.VerifyCommand.Tests -c Debug \
  --filter "FullyQualifiedName~PhysicalFixture" --nologo
```

Each scenario runs once, inside `sudo timeout -s KILL 180 unshare ...`, in a private mktemp
directory. Mounts are namespace-private; dm and loop devices are global kernel objects, which is
why teardown and the census are part of every case, the driver runs a failsafe release if a step
fails, and the janitor above covers the case where the driver itself never gets to run.
Afterwards check by hand: `sudo dmsetup ls`, `losetup -a`, `grep fsgg423b /proc/mounts` and
`ps -eo stat,cmd | awk '$1 ~ /^D/'` should show nothing from the run.

## What this does and does not establish

It establishes a repeatable, contained, kernel-produced blocked regular-file read and uncertain
close accepted by the production acquisition path, with the injected cause recorded. It is not a
field incident and does not claim one. It does not supply the host composition (a), the real SDD
to Verify to GateExecution acceptance (d), or stronger profiles and C3/C4 (e), and it does not
change any product behaviour. The FUSE blocked read is interruptible `S`, not `D`; the `D`-state
case is the block-layer one.
