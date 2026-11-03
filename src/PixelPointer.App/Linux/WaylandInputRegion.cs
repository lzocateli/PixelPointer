using System.Diagnostics;
using System.Runtime.InteropServices;
using SDL;
using static SDL.SDL3;

namespace PixelPointer.App.Linux;

internal static unsafe partial class WaylandInputRegion
{
    private const string Library = "libwayland-client.so.0";
    private const string DisplayProperty = "SDL.window.wayland.display";
    private const string SurfaceProperty = "SDL.window.wayland.surface";
    private const uint DestroyProxy = 1;

    private static readonly object s_gate = new();
    private static nint s_registryListener;
    private static RegistryBinding? s_binding;
    private static GCHandle s_bindingHandle;
    private static nint s_library;
    private static nint s_registry;
    private static nint s_compositor;
    private static nint s_display;
    private static long s_registryStartedAt;

    static WaylandInputRegion()
    {
        var listener = (WlRegistryListener*)NativeMemory.AllocZeroed((nuint)sizeof(WlRegistryListener));
        listener->Global = &OnRegistryGlobal;
        listener->GlobalRemove = &OnRegistryGlobalRemove;
        s_registryListener = (nint)listener;
    }

    internal static bool SetEmpty(SDL_Window* window)
    {
        var properties = SDL_GetWindowProperties(window);
        var display = SDL_GetPointerProperty(properties, DisplayProperty, 0);
        var surface = SDL_GetPointerProperty(properties, SurfaceProperty, 0);
        if (display == 0 || surface == 0)
        {
            throw new NotSupportedException("SDL não expôs a superfície Wayland para configurar o pass-through.");
        }

        if (!EnsureCompositor(display))
        {
            return false;
        }
        var regionInterface = GetInterface("wl_region_interface");
        var region = MarshalRequest(s_compositor, opcode: 1, regionInterface, version: 1, flags: 0, null);
        if (region == 0)
        {
            throw new InvalidOperationException("Não foi possível criar a região de input Wayland.");
        }

        try
        {
            WaylandArgument argument = default;
            argument.Pointer = region;
            MarshalRequest(surface, opcode: 5, @interface: 0, version: 0, flags: 0, &argument);
            MarshalRequest(surface, opcode: 6, @interface: 0, version: 0, flags: 0, null);
            if (Native.wl_display_flush(display) < 0)
            {
                throw new InvalidOperationException("Não foi possível aplicar a região de input Wayland.");
            }
        }
        finally
        {
            MarshalRequest(region, opcode: 0, @interface: 0, version: 1, flags: DestroyProxy, null);
        }

        return true;
    }

    private static bool EnsureCompositor(nint display)
    {
        lock (s_gate)
        {
            if (s_compositor != 0)
            {
                if (display != s_display)
                {
                    throw new InvalidOperationException("O backend Wayland do SDL mudou durante a sessão.");
                }

                return true;
            }

            if (s_binding?.Error is { } callbackError)
            {
                throw new NotSupportedException("Não foi possível vincular wl_compositor.", callbackError);
            }

            if (s_registry != 0)
            {
                if (Stopwatch.GetElapsedTime(s_registryStartedAt) > TimeSpan.FromSeconds(2))
                {
                    throw new TimeoutException("O compositor não anunciou wl_compositor no registry Wayland.");
                }

                return false;
            }

            s_library = NativeLibrary.Load(Library);
            s_display = display;
            s_binding = new RegistryBinding(GetInterface("wl_compositor_interface"));
            s_bindingHandle = GCHandle.Alloc(s_binding);
            var registryInterface = GetInterface("wl_registry_interface");
            var registryArgument = default(WaylandArgument);
            s_registry = MarshalRequest(display, opcode: 1, registryInterface, version: 1, flags: 0,
                &registryArgument);
            if (s_registry == 0
                || Native.wl_proxy_add_listener(s_registry, s_registryListener,
                    GCHandle.ToIntPtr(s_bindingHandle)) < 0
                || Native.wl_display_flush(display) < 0)
            {
                throw new NotSupportedException("Não foi possível consultar o registry Wayland.");
            }

            s_registryStartedAt = Stopwatch.GetTimestamp();
            return false;
        }
    }

    private static nint GetInterface(string name) => NativeLibrary.GetExport(s_library, name);

    private static nint MarshalRequest(nint proxy, uint opcode, nint @interface, uint version, uint flags,
        WaylandArgument* arguments) =>
        Native.wl_proxy_marshal_array_flags(proxy, opcode, @interface, version, flags, (nint)arguments);

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static void OnRegistryGlobal(nint data, nint registry, uint name, nint interfaceName, uint version)
    {
        if (GCHandle.FromIntPtr(data).Target is not RegistryBinding binding)
        {
            return;
        }

        try
        {
            if (Marshal.PtrToStringUTF8(interfaceName) != "wl_compositor" || version == 0)
            {
                return;
            }

            var namePointer = Marshal.StringToCoTaskMemUTF8("wl_compositor");
            try
            {
                WaylandArgument* arguments = stackalloc WaylandArgument[4];
                arguments[0].UInt = name;
                arguments[1].Pointer = namePointer;
                arguments[2].UInt = Math.Min(version, 1u);
                binding.Compositor = MarshalRequest(registry, opcode: 0, binding.CompositorInterface,
                    Math.Min(version, 1u), flags: 0, arguments);
                s_compositor = binding.Compositor;
            }
            finally
            {
                Marshal.FreeCoTaskMem(namePointer);
            }
        }
        catch (Exception ex)
        {
            binding.Error = ex;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static void OnRegistryGlobalRemove(nint data, nint registry, uint name)
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WlRegistryListener
    {
        public delegate* unmanaged[Cdecl]<nint, nint, uint, nint, uint, void> Global;
        public delegate* unmanaged[Cdecl]<nint, nint, uint, void> GlobalRemove;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    private struct WaylandArgument
    {
        [FieldOffset(0)]
        public uint UInt;

        [FieldOffset(0)]
        public nint Pointer;
    }

    private sealed class RegistryBinding(nint compositorInterface)
    {
        internal nint CompositorInterface { get; } = compositorInterface;

        internal nint Compositor { get; set; }

        internal Exception? Error { get; set; }
    }

    private static partial class Native
    {
        [LibraryImport(Library)]
        internal static partial int wl_display_flush(nint display);

        [LibraryImport(Library)]
        internal static partial int wl_display_roundtrip(nint display);

        [LibraryImport(Library)]
        internal static partial int wl_proxy_add_listener(nint proxy, nint implementation, nint data);

        [LibraryImport(Library)]
        internal static partial nint wl_proxy_marshal_array_flags(nint proxy, uint opcode, nint @interface,
            uint version, uint flags, nint arguments);
    }
}
