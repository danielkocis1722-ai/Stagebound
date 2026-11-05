using System;
using System.Runtime.InteropServices;

// Windows Logitech Steering Wheel SDK 8.75.30. Keep the native ABI at this boundary.
internal sealed class LogitechWheelBackend : IDisposable
{
    internal struct Sample
    {
        public int X, Y, Z, Rx, Ry, Rz, Slider0, Slider1;
        public uint Buttons;
        public uint Pov;
    }

    public string Name { get; private set; } = "";
    public string Status { get; private set; } = "Waiting for the game to have focus";
    public int Range { get; private set; }
    public bool CombinedPedals { get; private set; }
    public bool ForceAvailable { get; private set; }
    public int ShifterMode { get; private set; } = -1;
    public bool DeviceScanAvailable { get; private set; }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private const int G29 = 26;
    private const string Library = "LogitechSteeringWheelEnginesWrapper";
    private bool initialized, unavailable, playing;
    private int slot = -1, originalRange;
    private float nextScan, nextProperties;

    public bool Poll(int requestedRange, float now, out Sample sample)
    {
        sample = default;
        DeviceScanAvailable = false;
        if (unavailable)
            return false;

        try
        {
            if (!initialized)
            {
                // LogiUpdate completes initialization once the SDK can acquire the window.
                LogiSteeringInitialize(true);
                initialized = true;
            }
            if (!LogiUpdate())
            {
                ClearDevice();
                Status = "Waiting for Logitech DirectInput";
                return false;
            }
            DeviceScanAvailable = true;
            if (slot >= 0 && !IsG29(slot))
                ClearDevice();
            if (slot < 0)
            {
                Status = "Waiting for G29";
                if (now < nextScan)
                    return false;
                nextScan = now + .5f;
                for (int index = 0; index < 4; index++)
                    if (IsG29(index))
                    {
                        slot = index;
                        var name = new System.Text.StringBuilder(128);
                        Name = LogiGetFriendlyProductName(index, name, name.Capacity) ? name.ToString() : "Logitech G29";
                        Range = ReadRange();
                        if (Range != 0 && Range != requestedRange && LogiSetOperatingRange(slot, requestedRange))
                            originalRange = Range;
                        nextProperties = 0f;
                        break;
                    }
            }
            if (slot < 0)
                return false;

            if (now >= nextProperties)
            {
                nextProperties = now + 1f;
                Range = ReadRange();
                var properties = new Properties();
                CombinedPedals = LogiGetCurrentControllerProperties(slot, ref properties) && properties.CombinePedals != 0;
                ForceAvailable = LogiHasForceFeedback(slot);
                ShifterMode = LogiGetShifterMode(slot);
            }
            IntPtr state = LogiGetStateENGINES(slot);
            if (state == IntPtr.Zero)
            {
                StopForce();
                Status = "Waiting for G29 input state";
                return false;
            }

            // DIJOYSTATE2ENGINES starts with eight int axes and four DWORD POVs,
            // followed by the button bytes. Read only that prefix.
            sample.X = ReadAxis(state, 0);
            sample.Y = ReadAxis(state, 1);
            sample.Z = ReadAxis(state, 2);
            sample.Rx = ReadAxis(state, 3);
            sample.Ry = ReadAxis(state, 4);
            sample.Rz = ReadAxis(state, 5);
            sample.Slider0 = ReadAxis(state, 6);
            sample.Slider1 = ReadAxis(state, 7);
            sample.Pov = unchecked((uint)Marshal.ReadInt32(state, 32));
            for (int button = 0; button < 32; button++)
                if ((Marshal.ReadByte(state, 48 + button) & 0x80) != 0)
                    sample.Buttons |= 1u << button;

            Status = "G29 connected";
            return true;
        }
        catch (Exception error) when (IsInteropFailure(error))
        {
            Dispose();
            DeviceScanAvailable = false;
            unavailable = true;
            Status = error is DllNotFoundException ? "Install the official Logitech x64 SDK wrapper" : "Incompatible Logitech SDK wrapper";
            return false;
        }
    }

    public bool SendForce(int percent)
    {
        if (percent == 0 || slot < 0 || !ForceAvailable)
        {
            StopForce();
            return false;
        }
        try
        {
            playing = LogiPlayConstantForce(slot, Math.Max(-100, Math.Min(100, percent)));
            if (!playing)
                LogiStopConstantForce(slot);
            return playing;
        }
        catch (Exception error) when (IsInteropFailure(error))
        {
            Dispose();
            unavailable = true;
            Status = "Incompatible Logitech force output";
            return false;
        }
    }

    public void StopForce()
    {
        if (!playing)
            return;
        playing = false;
        try
        {
            if (slot >= 0)
                LogiStopConstantForce(slot);
        }
        catch (Exception error) when (IsInteropFailure(error))
        {
            ForceAvailable = false;
        }
    }

    public bool ConstantForceActive => slot >= 0 && LogiIsPlaying(slot, 1);

    private void ClearDevice()
    {
        StopForce();
        if (originalRange != 0 && slot >= 0 && IsG29(slot))
            LogiSetOperatingRange(slot, originalRange);
        originalRange = 0;
        slot = -1;
        Name = "";
        Range = 0;
        CombinedPedals = ForceAvailable = false;
        ShifterMode = -1;
    }

    public void Dispose()
    {
        if (!initialized)
            return;
        try
        {
            ClearDevice();
        }
        catch (Exception error) when (IsInteropFailure(error))
        {
            // A mismatched wrapper may be missing a range operation.
        }
        finally
        {
            try { LogiSteeringShutdown(); }
            catch (Exception error) when (IsInteropFailure(error)) { }
            initialized = false;
            playing = false;
            slot = -1;
            originalRange = Range = 0;
            Name = "";
            CombinedPedals = ForceAvailable = false;
            ShifterMode = -1;
            nextScan = 0f;
        }
    }

    private int ReadRange()
    {
        int range = 0;
        return LogiGetOperatingRange(slot, ref range) && range >= 40 && range <= 900 ? range : 0;
    }

    private static bool IsG29(int index) => LogiIsConnected(index) && LogiIsModelConnected(index, G29);
    private static bool IsInteropFailure(Exception error) =>
        error is DllNotFoundException || error is EntryPointNotFoundException || error is BadImageFormatException;
    private static int ReadAxis(IntPtr state, int axis) => Marshal.ReadInt32(state, axis * sizeof(int));

    [StructLayout(LayoutKind.Sequential)]
    private struct Properties
    {
        public byte ForceEnable;
        public int OverallGain, SpringGain, DamperGain;
        public byte DefaultSpringEnabled;
        public int DefaultSpringGain;
        public byte CombinePedals;
        public int WheelRange;
        public byte GameSettingsEnabled, AllowGameSettings;
    }

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiSteeringInitialize([MarshalAs(UnmanagedType.I1)] bool ignoreXInput);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void LogiSteeringShutdown();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiUpdate();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiIsConnected(int index);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiIsModelConnected(int index, int model);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiGetFriendlyProductName(int index, System.Text.StringBuilder name, int capacity);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr LogiGetStateENGINES(int index);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiGetOperatingRange(int index, ref int range);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiSetOperatingRange(int index, int range);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiGetCurrentControllerProperties(int index, ref Properties properties);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiHasForceFeedback(int index);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiPlayConstantForce(int index, int percent);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiStopConstantForce(int index);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiIsPlaying(int index, int forceType);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int LogiGetShifterMode(int index);
#else
    public bool Poll(int requestedRange, float now, out Sample sample)
    {
        sample = default;
        Status = "Logitech SDK is Windows only";
        return false;
    }

    public bool SendForce(int percent) => false;
    public bool ConstantForceActive => false;
    public void StopForce() { }
    public void Dispose() { }
#endif
}
