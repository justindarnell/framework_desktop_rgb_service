using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FrameworkDesktopRgbService;

public static class CrosEcDevice
{
    private const string DevicePath = "\\\\.\\GLOBALROOT\\Device\\CrosEC";
    private const int EcRgbKbdMaxKeyCount = 64;
    private const int CrosEcCmdMaxRequest = 0x100;
    private const int HeaderLength = 20;
    private const ushort EcCommandRgbKbdSetColor = 0x013A;
    private const uint FileGenericRead = 0x80000000;
    private const uint FileGenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint MethodBuffered = 0;
    private const uint FileReadData = 0x0001;
    private const uint FileWriteData = 0x0002;
    private const uint FileDeviceCrosEmbeddedController = 0x80EC;

    private static readonly uint IoctlCrosEcXcmd = CtlCode(
        FileDeviceCrosEmbeddedController,
        0x801,
        MethodBuffered,
        FileReadData | FileWriteData);

    public static Task SetRgbKeyboardColorsAsync(int startKey, IReadOnlyList<string> colors, CancellationToken cancellationToken)
    {
        return Task.Run(() => SetRgbKeyboardColors(startKey, colors, cancellationToken), cancellationToken);
    }

    private static void SetRgbKeyboardColors(int startKey, IReadOnlyList<string> colors, CancellationToken cancellationToken)
    {
        if (startKey < 0 || startKey > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(startKey), "Start key must fit in a byte.");
        }

        using var handle = OpenDevice();
        var rgbValues = colors.Select(ParseColor).ToList();
        var currentStartKey = startKey;

        foreach (var chunk in rgbValues.Chunk(EcRgbKbdMaxKeyCount))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = BuildRgbPayload((byte)currentStartKey, chunk);
            SendCommand(handle, EcCommandRgbKbdSetColor, 0, payload);
            currentStartKey += chunk.Length;
        }
    }

    private static SafeFileHandle OpenDevice()
    {
        var handle = CreateFile(
            DevicePath,
            FileGenericRead | FileGenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to open CrosEC device. Ensure the Framework Windows EC driver is installed.");
        }

        return handle;
    }

    private static byte[] BuildRgbPayload(byte startKey, IReadOnlyList<uint> colors)
    {
        var payload = new byte[2 + (EcRgbKbdMaxKeyCount * 3)];
        payload[0] = startKey;
        payload[1] = (byte)colors.Count;

        var offset = 2;
        foreach (var color in colors)
        {
            payload[offset++] = (byte)((color & 0x00FF0000) >> 16);
            payload[offset++] = (byte)((color & 0x0000FF00) >> 8);
            payload[offset++] = (byte)(color & 0x000000FF);
        }

        return payload;
    }

    private static uint ParseColor(string color)
    {
        var trimmed = color.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }
        else if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        return uint.Parse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static void SendCommand(SafeFileHandle handle, ushort command, byte commandVersion, byte[] payload)
    {
        if (payload.Length > CrosEcCmdMaxRequest - HeaderLength)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), "Payload exceeds maximum EC command size.");
        }

        var commandPacket = new CrosEcCommand
        {
            Version = commandVersion,
            Command = command,
            OutSize = (uint)payload.Length,
            InSize = (uint)(CrosEcCmdMaxRequest - HeaderLength),
            Result = 0xFF,
            Buffer = new byte[CrosEcCmdMaxRequest - HeaderLength],
        };

        Array.Copy(payload, commandPacket.Buffer, payload.Length);
        var size = Marshal.SizeOf<CrosEcCommand>();
        var commandPtr = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.StructureToPtr(commandPacket, commandPtr, false);

            if (!DeviceIoControl(
                    handle,
                    IoctlCrosEcXcmd,
                    commandPtr,
                    size,
                    commandPtr,
                    size,
                    out var _,
                    IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to send EC command.");
            }

            var response = Marshal.PtrToStructure<CrosEcCommand>(commandPtr);

            if (response.Result != 0)
            {
                throw new InvalidOperationException($"EC command failed with status 0x{response.Result:X}.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(commandPtr);
        }
    }

    private static uint CtlCode(uint deviceType, uint function, uint method, uint access)
    {
        return (deviceType << 16) + (access << 14) + (function << 2) + method;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct CrosEcCommand
    {
        public uint Version;
        public uint Command;
        public uint OutSize;
        public uint InSize;
        public uint Result;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CrosEcCmdMaxRequest - HeaderLength)]
        public byte[] Buffer;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        int nInBufferSize,
        IntPtr lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);
}
