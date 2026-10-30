/// GOV-423 prerequisite (b) physical fixture, part 1: the FUSE wire protocol as pure functions.
/// A raw protocol implementation (no libfuse, no gcc, no FUSE bindings): decode a kernel request,
/// decide the reply. Nothing here performs I/O, so every decision is covered by inert controls in
/// default CI; the privileged daemon in PhysicalFixtureRuntime only moves bytes to and from /dev/fuse.
module FS.GG.Governance.VerifyCommand.Tests.PhysicalFixtureFuse

open System
open System.Buffers.Binary
open System.Text

// Opcodes (include/uapi/linux/fuse.h).
[<Literal>]
let OpLookup = 1u
[<Literal>]
let OpForget = 2u
[<Literal>]
let OpGetattr = 3u
[<Literal>]
let OpOpen = 14u
[<Literal>]
let OpRead = 15u
[<Literal>]
let OpStatfs = 17u
[<Literal>]
let OpRelease = 18u
[<Literal>]
let OpFlush = 25u
[<Literal>]
let OpInit = 26u
[<Literal>]
let OpOpendir = 27u
[<Literal>]
let OpReaddir = 28u
[<Literal>]
let OpReleasedir = 29u
[<Literal>]
let OpAccess = 34u
[<Literal>]
let OpInterrupt = 36u
[<Literal>]
let OpBatchForget = 42u

let EIO = 5
let ENOENT = 2
let EINTR = 4
let ENOSYS = 38

let RootNode = 1UL
let FileNode = 2UL
let InHeaderSize = 40
let OutHeaderSize = 16

let opName (opcode: uint32) =
    match opcode with
    | OpLookup -> "LOOKUP"
    | OpForget -> "FORGET"
    | OpGetattr -> "GETATTR"
    | OpOpen -> "OPEN"
    | OpRead -> "READ"
    | OpStatfs -> "STATFS"
    | OpRelease -> "RELEASE"
    | OpFlush -> "FLUSH"
    | OpInit -> "INIT"
    | OpOpendir -> "OPENDIR"
    | OpReaddir -> "READDIR"
    | OpReleasedir -> "RELEASEDIR"
    | OpAccess -> "ACCESS"
    | OpInterrupt -> "INTERRUPT"
    | OpBatchForget -> "BATCH_FORGET"
    | other -> "OP" + string other

/// How the daemon behaves. The injected cause is exactly these two switches.
type Config =
    { /// READ is received but not answered until released (the injected block).
      HoldRead: bool
      /// Errno returned by FLUSH (0 = success). EIO is the injected uncertain close.
      FlushErrno: int
      FileName: string
      Content: byte[] }

type FuseRequest =
    { Length: int
      Opcode: uint32
      Unique: uint64
      NodeId: uint64
      Uid: uint32
      Gid: uint32
      Pid: uint32
      Body: byte[] }

type Decision =
    /// Answer now. Errno is the positive errno (0 = success); the wire carries its negation.
    | Reply of errno: int * payload: byte[]
    /// Receive READ but withhold the reply; the payload is what a release will return.
    | Hold of payload: byte[]
    | NoReply

let parseRequest (buffer: byte[]) (count: int) : FuseRequest option =
    if count < InHeaderSize then None
    else
        let span = ReadOnlySpan<byte>(buffer, 0, count)
        let length = int (BinaryPrimitives.ReadUInt32LittleEndian(span.Slice 0))
        if length < InHeaderSize || length > count then None
        else
            Some
                { Length = length
                  Opcode = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice 4)
                  Unique = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice 8)
                  NodeId = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice 16)
                  Uid = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice 24)
                  Gid = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice 28)
                  Pid = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice 32)
                  Body = span.Slice(InHeaderSize, length - InHeaderSize).ToArray() }

/// fuse_out_header + payload. `errno` is positive; the wire error is its negation.
let encodeReply (unique: uint64) (errno: int) (payload: byte[]) : byte[] =
    let body = if errno = 0 then payload else [||]
    let out = Array.zeroCreate<byte> (OutHeaderSize + body.Length)
    BinaryPrimitives.WriteUInt32LittleEndian(Span<byte>(out, 0, 4), uint32 out.Length)
    BinaryPrimitives.WriteInt32LittleEndian(Span<byte>(out, 4, 4), -errno)
    BinaryPrimitives.WriteUInt64LittleEndian(Span<byte>(out, 8, 8), unique)
    Array.blit body 0 out OutHeaderSize body.Length
    out

let private u32 (buffer: byte[]) offset (value: uint32) = BinaryPrimitives.WriteUInt32LittleEndian(Span<byte>(buffer, offset, 4), value)
let private u64 (buffer: byte[]) offset (value: uint64) = BinaryPrimitives.WriteUInt64LittleEndian(Span<byte>(buffer, offset, 8), value)

let private fixedTime = 1_700_000_000UL
let private validSeconds = 3600UL

/// struct fuse_attr, 88 bytes (ino 0, size 8, blocks 16, a/m/ctime 24/32/40, nsecs 48/52/56, mode 60, nlink 64, uid 68, gid 72, rdev 76, blksize 80). Mode is S_IFREG|0644 for the file and S_IFDIR|0755 for the root.
let encodeAttr (config: Config) (node: uint64) : byte[] =
    let attr = Array.zeroCreate<byte> 88
    let isFile = node = FileNode
    u64 attr 0 node
    u64 attr 8 (if isFile then uint64 config.Content.Length else 0UL)
    u64 attr 16 (if isFile then (uint64 config.Content.Length + 511UL) / 512UL else 0UL)
    u64 attr 24 fixedTime
    u64 attr 32 fixedTime
    u64 attr 40 fixedTime
    u32 attr 60 (if isFile then 0o100644u else 0o040755u)
    u32 attr 64 (if isFile then 1u else 2u)
    u32 attr 80 4096u
    attr

let encodeEntry (config: Config) (node: uint64) : byte[] =
    // fuse_entry_out: nodeid, generation, entry_valid, attr_valid, nsecs, attr = 128 bytes.
    let entry = Array.zeroCreate<byte> 128
    u64 entry 0 node
    u64 entry 16 validSeconds
    u64 entry 24 validSeconds
    Array.blit (encodeAttr config node) 0 entry 40 88
    entry

let encodeAttrOut (config: Config) (node: uint64) : byte[] =
    // fuse_attr_out: attr_valid, attr_valid_nsec, dummy, attr = 104 bytes.
    let out = Array.zeroCreate<byte> 104
    u64 out 0 validSeconds
    Array.blit (encodeAttr config node) 0 out 16 88
    out

/// fuse_init_out (64 bytes). Protocol 7.31, no optional capabilities negotiated.
let encodeInitOut () : byte[] =
    let out = Array.zeroCreate<byte> 64
    u32 out 0 7u
    u32 out 4 31u
    u32 out 8 0u
    u32 out 12 0u
    BinaryPrimitives.WriteUInt16LittleEndian(Span<byte>(out, 16, 2), 12us)
    BinaryPrimitives.WriteUInt16LittleEndian(Span<byte>(out, 18, 2), 9us)
    u32 out 20 131072u
    u32 out 24 1u
    out

let private encodeOpenOut () : byte[] =
    let out = Array.zeroCreate<byte> 16
    u64 out 0 1UL
    out

let private lookupName (body: byte[]) =
    let terminator = Array.IndexOf(body, 0uy)
    let length = if terminator < 0 then body.Length else terminator
    Encoding.UTF8.GetString(body, 0, length)

/// fuse_read_in carries offset at 8 and size at 16. The file is served from `Content` only.
let readSlice (config: Config) (body: byte[]) : byte[] =
    if body.Length < 40 then [||]
    else
        let offset = BinaryPrimitives.ReadUInt64LittleEndian(ReadOnlySpan<byte>(body, 8, 8))
        let size = BinaryPrimitives.ReadUInt32LittleEndian(ReadOnlySpan<byte>(body, 16, 4))
        if offset >= uint64 config.Content.Length then [||]
        else
            let available = config.Content.Length - int offset
            Array.sub config.Content (int offset) (min available (int size))

let decide (config: Config) (request: FuseRequest) : Decision =
    match request.Opcode with
    | OpInit -> Reply(0, encodeInitOut ())
    | OpLookup when request.NodeId = RootNode && lookupName request.Body = config.FileName -> Reply(0, encodeEntry config FileNode)
    | OpLookup -> Reply(ENOENT, [||])
    | OpGetattr when request.NodeId = RootNode || request.NodeId = FileNode -> Reply(0, encodeAttrOut config request.NodeId)
    | OpGetattr -> Reply(ENOENT, [||])
    | OpOpen when request.NodeId = FileNode -> Reply(0, encodeOpenOut ())
    | OpOpendir when request.NodeId = RootNode -> Reply(0, encodeOpenOut ())
    | OpOpen | OpOpendir -> Reply(ENOENT, [||])
    | OpRead when request.NodeId = FileNode ->
        let data = readSlice config request.Body
        if config.HoldRead then Hold data else Reply(0, data)
    | OpRead -> Reply(ENOENT, [||])
    | OpReaddir -> Reply(0, [||])
    | OpFlush -> Reply(config.FlushErrno, [||])
    | OpRelease | OpReleasedir -> Reply(0, [||])
    | OpAccess -> Reply(0, [||])
    | OpStatfs -> Reply(0, Array.zeroCreate<byte> 80)
    | OpForget | OpBatchForget | OpInterrupt -> NoReply
    | _ -> Reply(ENOSYS, [||])

/// The unique id an INTERRUPT refers to (fuse_interrupt_in).
let interruptTarget (request: FuseRequest) : uint64 option =
    if request.Opcode = OpInterrupt && request.Body.Length >= 8 then
        Some(BinaryPrimitives.ReadUInt64LittleEndian(ReadOnlySpan<byte>(request.Body, 0, 8)))
    else None
