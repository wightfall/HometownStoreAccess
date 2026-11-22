using System;
using System.Runtime.InteropServices;
using System.Text;

namespace HometownStoreAccess.Speech
{
    /// <summary>
    /// P/Invoke surface for Prism (https://github.com/ethindp/prism), the screen reader and TTS
    /// abstraction library. Signatures follow prism.h from release 0.18.3.
    /// All calls are made from Unity's main thread: Prism backend instances are not thread-safe.
    /// </summary>
    internal static class PrismNative
    {
        private const string Dll = "prism";

        public const byte PrismConfigVersion = 4;

        [StructLayout(LayoutKind.Sequential)]
        public struct PrismConfig
        {
            public byte version;
            public IntPtr registry;
            public IntPtr availability_callback;
            public IntPtr availability_userdata;
            public uint availability_poll_interval_ms;
            public uint availability_debounce_samples;
            public uint availability_backoff_max_ms;
            public byte availability_auto_power_manage;
            public IntPtr availability_baseline_callback;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void AvailabilityCallback(IntPtr userdata, ulong backend, IntPtr name, [MarshalAs(UnmanagedType.U1)] bool available);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismConfig prism_config_init();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_init(IntPtr cfg);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_init(ref PrismConfig cfg);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void prism_shutdown(IntPtr ctx);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_registry_create_best(IntPtr ctx);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_registry_create(IntPtr ctx, ulong id);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr prism_registry_count(IntPtr ctx);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong prism_registry_id_at(IntPtr ctx, UIntPtr index);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_registry_name(IntPtr ctx, ulong id);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void prism_backend_free(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_backend_name(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int prism_backend_output(IntPtr backend, byte[] text, [MarshalAs(UnmanagedType.U1)] bool interrupt);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int prism_backend_speak(IntPtr backend, byte[] text, [MarshalAs(UnmanagedType.U1)] bool interrupt);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int prism_backend_stop(IntPtr backend);

        /// <summary>A backend made with prism_registry_create must be initialized before use (the "best" ones come ready).</summary>
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int prism_backend_initialize(IntPtr backend);

        public const int PrismOk = 0;
        public const int PrismErrorAlreadyInitialized = 15;

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_error_string(int error);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_version_string();

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadLibraryW(string path);

        public static byte[] Utf8Z(string s)
        {
            int n = Encoding.UTF8.GetByteCount(s);
            var bytes = new byte[n + 1];
            Encoding.UTF8.GetBytes(s, 0, s.Length, bytes, 0);
            return bytes;
        }

        public static string FromUtf8(IntPtr p)
        {
            if (p == IntPtr.Zero) return string.Empty;
            int n = 0;
            while (Marshal.ReadByte(p, n) != 0) n++;
            var bytes = new byte[n];
            Marshal.Copy(p, bytes, 0, n);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
