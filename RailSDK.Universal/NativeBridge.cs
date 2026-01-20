// ============================================================================
// NATIVE BRIDGE P/INVOKE
// ============================================================================
// P/Invoke declarations for RailBridge.dll native library.
//
// ============================================================================

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace RailSDK
{
    /// <summary>
    /// P/Invoke bindings for the native RailBridge library.
    /// </summary>
    internal static class NativeBridge
    {
        private const string DllName = "RailBridge";

        /// <summary>
        /// Callback delegate for command execution.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr CommandCallback(IntPtr commandJson);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Rail_Ignite(string instanceId, string jsonManifest, CommandCallback onCommand);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Rail_Disconnect();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Rail_Heartbeat();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr Rail_GetVersion();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Rail_IsConnected();

        /// <summary>
        /// Static constructor to ensure native library is loaded from correct location.
        /// </summary>
        static NativeBridge()
        {
            // Try to help the runtime find the native DLL
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (assemblyDir != null)
            {
                var nativePath = Path.Combine(assemblyDir, "runtimes", "win-x64", "native");
                if (Directory.Exists(nativePath))
                {
                    SetDllDirectory(nativePath);
                }
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);
    }
}



