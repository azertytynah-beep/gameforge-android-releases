using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace GameForge.Desktop
{
    internal static class WindowsDictation
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Input { internal uint Type; internal InputData Data; }
        [StructLayout(LayoutKind.Explicit)]
        internal struct InputData
        {
            [FieldOffset(0)] internal KeyboardInput Keyboard;
            // INPUT includes the mouse union member even for keyboard events.
            [FieldOffset(0)] internal MouseInput Mouse;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct KeyboardInput { internal ushort Key, Scan; internal uint Flags, Time; internal UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct MouseInput { internal int X, Y; internal uint Data, Flags, Time; internal UIntPtr Extra; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, Input[] inputs, int size);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        internal static Input[] Shortcut() => new[] { Key(0x5B, false), Key(0x48, false), Key(0x48, true), Key(0x5B, true) };
        private static Input Key(ushort key, bool up) => new Input
        { Type = 1, Data = new InputData { Keyboard = new KeyboardInput { Key = key, Flags = up ? 2u : 0u } } };

        internal static void Open(Window owner, TextBox field)
        {
            var handle = new WindowInteropHelper(owner).Handle;
            if (handle == IntPtr.Zero || GetForegroundWindow() != handle || !field.IsKeyboardFocusWithin ||
                !field.IsEnabled || !field.IsVisible || Window.GetWindow(field) != owner)
                throw new InvalidOperationException("Clique dans ta réponse, puis appuie sur Windows + H pour dicter.");
            foreach (int key in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C, 0x48 })
                if (GetAsyncKeyState(key) < 0)
                    throw new InvalidOperationException("Relâche les touches du clavier, puis réessaie ou utilise Windows + H.");
            var keys = Shortcut();
            int size = Marshal.SizeOf<Input>();
            uint sent = SendInput((uint)keys.Length, keys, size);
            if (sent == keys.Length) return;
            if (sent > 0 && SendInput(2, new[] { keys[2], keys[3] }, size) != 2)
                throw new InvalidOperationException("Relâche les touches H et Windows, puis utilise Windows + H dans ta réponse.");
            throw new InvalidOperationException("Windows n’a pas accepté l’ouverture de la dictée. Le champ est prêt : appuie sur Windows + H.");
        }
    }
}
