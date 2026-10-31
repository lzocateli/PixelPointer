using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SkiaSharp;

namespace PixelPointer.App.Linux;

internal static class PipeWireFrame
{
    internal static async Task<SKBitmap> ReadAsync(SafeFileHandle remote, uint nodeId, TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var reader = PipeWireFrameReader.Start(remote, nodeId);
        try
        {
            return await reader.Frame.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Após o teardown nenhum callback roda; um quadro entregue durante o timeout é descartado aqui.
            reader.Dispose();
            if (reader.Frame.IsCompletedSuccessfully)
            {
                reader.Frame.Result.Dispose();
            }

            throw;
        }
    }
}

/// <summary>
/// Lê um único quadro de um nó PipeWire do portal ScreenCast. Usa a libpipewire-0.3 do sistema (MIT), sem
/// redistribuí-la; os callbacks rodam na thread do pw_thread_loop e entregam o quadro por TaskCompletionSource.
/// </summary>
internal sealed unsafe partial class PipeWireFrameReader : IDisposable
{
    private const string Library = "libpipewire-0.3.so.0";
    private const uint StreamFlagAutoconnect = 1 << 0;
    private const uint StreamFlagMapBuffers = 1 << 2;
    private const uint DataMemPtr = 1;
    private const uint DataMemFd = 2;
    private const uint ChunkFlagCorrupted = 1;
    private const int StreamStateError = -1;

    private static int s_initialized;

    private readonly TaskCompletionSource<SKBitmap> _frame = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private GCHandle _self;
    private nint _loop;
    private nint _context;
    private nint _core;
    private nint _stream;
    private nint _hook;
    private nint _events;
    private SpaVideoInfo? _info;

    private PipeWireFrameReader()
    {
    }

    /// <summary>Conecta ao nó, aguarda o primeiro quadro válido e libera todos os recursos antes de retornar.</summary>
    internal static Task<SKBitmap> ReadFrameAsync(SafeFileHandle remote, uint nodeId, TimeSpan timeout,
        CancellationToken cancellationToken) => PipeWireFrame.ReadAsync(remote, nodeId, timeout, cancellationToken);

    internal static PipeWireFrameReader Start(SafeFileHandle remote, uint nodeId)
    {
        var reader = new PipeWireFrameReader();
        try
        {
            reader.Connect(remote, nodeId);
            return reader;
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    internal Task<SKBitmap> Frame => _frame.Task;

    private void Connect(SafeFileHandle remote, uint nodeId)
    {
        if (Interlocked.Exchange(ref s_initialized, 1) == 0)
        {
            Native.pw_init(null, null);
        }

        _self = GCHandle.Alloc(this);
        _loop = Native.pw_thread_loop_new("pixelpointer-screencast", null);
        if (_loop == 0 || Native.pw_thread_loop_start(_loop) < 0)
        {
            throw new InvalidOperationException("Não foi possível iniciar o loop do PipeWire.");
        }

        Native.pw_thread_loop_lock(_loop);
        try
        {
            _context = Native.pw_context_new(Native.pw_thread_loop_get_loop(_loop), 0, 0);
            if (_context == 0)
            {
                throw new InvalidOperationException("Não foi possível criar o contexto PipeWire.");
            }

            // O PipeWire assume o descritor e o fecha ao desconectar.
            var fd = (int)remote.DangerousGetHandle();
            remote.SetHandleAsInvalid();
            _core = Native.pw_context_connect_fd(_context, fd, 0, 0);
            if (_core == 0)
            {
                throw new InvalidOperationException("Não foi possível conectar ao PipeWire do portal.");
            }

            _stream = CreateStream(_core);
            _hook = (nint)NativeMemory.AllocZeroed(64);
            _events = (nint)NativeMemory.AllocZeroed((nuint)sizeof(StreamEvents));
            var events = (StreamEvents*)_events;
            events->Version = 2;
            events->StateChanged = &OnStateChanged;
            events->ParamChanged = &OnParamChanged;
            events->Process = &OnProcess;
            Native.pw_stream_add_listener(_stream, _hook, _events, GCHandle.ToIntPtr(_self));

            var format = SpaPod.BuildVideoEnumFormat();
            fixed (byte* pod = format)
            {
                var parameters = (nint)pod;
                if (Native.pw_stream_connect(_stream, 0, nodeId, StreamFlagAutoconnect | StreamFlagMapBuffers,
                        &parameters, 1) < 0)
                {
                    throw new InvalidOperationException("Não foi possível conectar ao stream de captura.");
                }
            }
        }
        finally
        {
            Native.pw_thread_loop_unlock(_loop);
        }
    }

    private static nint CreateStream(nint core)
    {
        var keys = new[] { "media.type", "media.category", "media.role" };
        var values = new[] { "Video", "Capture", "Screen" };
        var allocations = new List<nint>();
        try
        {
            var items = (nint*)NativeMemory.Alloc((nuint)(keys.Length * 2 * sizeof(nint)));
            allocations.Add((nint)items);
            for (var i = 0; i < keys.Length; i++)
            {
                items[i * 2] = Marshal.StringToCoTaskMemUTF8(keys[i]);
                items[(i * 2) + 1] = Marshal.StringToCoTaskMemUTF8(values[i]);
            }

            var dict = new SpaDict { Flags = 0, Count = (uint)keys.Length, Items = (nint)items };
            var properties = Native.pw_properties_new_dict(&dict);
            for (var i = 0; i < keys.Length * 2; i++)
            {
                Marshal.FreeCoTaskMem(items[i]);
            }

            var stream = Native.pw_stream_new(core, "PixelPointer", properties);
            return stream != 0 ? stream : throw new InvalidOperationException("Não foi possível criar o stream PipeWire.");
        }
        finally
        {
            foreach (var allocation in allocations)
            {
                NativeMemory.Free((void*)allocation);
            }
        }
    }

    public void Dispose()
    {
        if (_loop != 0)
        {
            Native.pw_thread_loop_lock(_loop);
            if (_stream != 0)
            {
                Native.pw_stream_disconnect(_stream);
                Native.pw_stream_destroy(_stream);
            }

            if (_core != 0)
            {
                Native.pw_core_disconnect(_core);
            }

            if (_context != 0)
            {
                Native.pw_context_destroy(_context);
            }

            Native.pw_thread_loop_unlock(_loop);
            Native.pw_thread_loop_stop(_loop);
            Native.pw_thread_loop_destroy(_loop);
            _stream = _core = _context = _loop = 0;
        }

        NativeMemory.Free((void*)_hook);
        NativeMemory.Free((void*)_events);
        _hook = _events = 0;
        if (_self.IsAllocated)
        {
            _self.Free();
        }

        _frame.TrySetCanceled();
    }

    private static PipeWireFrameReader? From(void* data) =>
        data is null ? null : GCHandle.FromIntPtr((nint)data).Target as PipeWireFrameReader;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnStateChanged(void* data, int oldState, int state, byte* error)
    {
        if (state == StreamStateError && From(data) is { } reader)
        {
            var message = error is null ? "erro desconhecido" : Marshal.PtrToStringUTF8((nint)error);
            reader._frame.TrySetException(new InvalidOperationException($"Stream PipeWire falhou: {message}"));
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnParamChanged(void* data, uint id, SpaPodHeader* param)
    {
        if (id != SpaPod.ParamFormat || param is null || From(data) is not { } reader)
        {
            return;
        }

        var length = checked(8 + (int)param->Size);
        reader._info = SpaPod.ParseVideoFormat(new ReadOnlySpan<byte>(param, length));
        if (reader._info is null)
        {
            reader._frame.TrySetException(new InvalidOperationException("O PipeWire negociou um formato de vídeo não suportado."));
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnProcess(void* data)
    {
        if (From(data) is not { } reader)
        {
            return;
        }

        var buffer = Native.pw_stream_dequeue_buffer(reader._stream);
        if (buffer is null)
        {
            return;
        }

        try
        {
            if (reader._frame.Task.IsCompleted || reader._info is not { } info || buffer->Buffer is null
                || buffer->Buffer->DataCount == 0)
            {
                return;
            }

            var spaData = buffer->Buffer->Datas;
            var chunk = spaData->Chunk;
            if (spaData->Data is null || chunk is null || chunk->Size == 0 || (chunk->Flags & ChunkFlagCorrupted) != 0
                || spaData->Type is not (DataMemPtr or DataMemFd))
            {
                return;
            }

            var stride = chunk->Stride > 0 ? chunk->Stride : info.Width * 4;
            var available = (long)spaData->MaxSize - chunk->Offset;
            if (chunk->Offset > spaData->MaxSize || available <= 0)
            {
                return;
            }

            var span = new ReadOnlySpan<byte>((byte*)spaData->Data + chunk->Offset, (int)Math.Min(available, int.MaxValue));
            reader._frame.TrySetResult(VideoFrameConverter.ToBitmap(span, info, stride));
        }
        catch (Exception ex)
        {
            reader._frame.TrySetException(ex);
        }
        finally
        {
            Native.pw_stream_queue_buffer(reader._stream, buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpaDict
    {
        public uint Flags;
        public uint Count;
        public nint Items;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SpaPodHeader
    {
        public uint Size;
        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StreamEvents
    {
        public uint Version;
        public delegate* unmanaged[Cdecl]<void*, void> Destroy;
        public delegate* unmanaged[Cdecl]<void*, int, int, byte*, void> StateChanged;
        public void* ControlInfo;
        public void* IoChanged;
        public delegate* unmanaged[Cdecl]<void*, uint, SpaPodHeader*, void> ParamChanged;
        public void* AddBuffer;
        public void* RemoveBuffer;
        public delegate* unmanaged[Cdecl]<void*, void> Process;
        public void* Drained;
        public void* Command;
        public void* TriggerDone;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PwBuffer
    {
        public SpaBuffer* Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpaBuffer
    {
        public uint MetaCount;
        public uint DataCount;
        public void* Metas;
        public SpaData* Datas;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpaData
    {
        public uint Type;
        public uint Flags;
        public long Fd;
        public uint MapOffset;
        public uint MaxSize;
        public void* Data;
        public SpaChunk* Chunk;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpaChunk
    {
        public uint Offset;
        public uint Size;
        public int Stride;
        public int Flags;
    }

    private static partial class Native
    {
        [LibraryImport(Library)]
        internal static partial void pw_init(int* argc, byte*** argv);

        [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial nint pw_thread_loop_new(string name, void* properties);

        [LibraryImport(Library)]
        internal static partial int pw_thread_loop_start(nint loop);

        [LibraryImport(Library)]
        internal static partial void pw_thread_loop_stop(nint loop);

        [LibraryImport(Library)]
        internal static partial void pw_thread_loop_destroy(nint loop);

        [LibraryImport(Library)]
        internal static partial void pw_thread_loop_lock(nint loop);

        [LibraryImport(Library)]
        internal static partial void pw_thread_loop_unlock(nint loop);

        [LibraryImport(Library)]
        internal static partial nint pw_thread_loop_get_loop(nint loop);

        [LibraryImport(Library)]
        internal static partial nint pw_context_new(nint loop, nint properties, nuint userDataSize);

        [LibraryImport(Library)]
        internal static partial void pw_context_destroy(nint context);

        [LibraryImport(Library)]
        internal static partial nint pw_context_connect_fd(nint context, int fd, nint properties, nuint userDataSize);

        [LibraryImport(Library)]
        internal static partial int pw_core_disconnect(nint core);

        [LibraryImport(Library)]
        internal static partial nint pw_properties_new_dict(SpaDict* dict);

        [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial nint pw_stream_new(nint core, string name, nint properties);

        [LibraryImport(Library)]
        internal static partial void pw_stream_add_listener(nint stream, nint hook, nint events, nint data);

        [LibraryImport(Library)]
        internal static partial int pw_stream_connect(nint stream, uint direction, uint targetId, uint flags,
            nint* parameters, uint count);

        [LibraryImport(Library)]
        internal static partial PwBuffer* pw_stream_dequeue_buffer(nint stream);

        [LibraryImport(Library)]
        internal static partial int pw_stream_queue_buffer(nint stream, PwBuffer* buffer);

        [LibraryImport(Library)]
        internal static partial int pw_stream_disconnect(nint stream);

        [LibraryImport(Library)]
        internal static partial void pw_stream_destroy(nint stream);
    }
}
