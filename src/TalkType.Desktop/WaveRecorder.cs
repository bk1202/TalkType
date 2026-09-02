using System.Runtime.InteropServices;

namespace TalkType.Desktop;

internal sealed class WaveRecorder : IDisposable
{
    private const uint WaveMapper = uint.MaxValue;
    private const uint CallbackFunction = 0x00030000;
    private const uint WomData = 0x03C0;
    private const int BufferSize = 32000;
    private readonly List<byte> audio = [];
    private readonly WaveInProc callback;
    private readonly List<(IntPtr Header, IntPtr Data)> buffers = [];
    private IntPtr waveIn;
    private bool recording;

    public WaveRecorder() => callback = OnWaveMessage;

    public void Start()
    {
        var format = new WaveFormat
        {
            FormatTag = 1,
            Channels = 1,
            SamplesPerSecond = 16000,
            AverageBytesPerSecond = 32000,
            BlockAlign = 2,
            BitsPerSample = 16
        };

        Check(waveInOpen(out waveIn, WaveMapper, ref format, callback, IntPtr.Zero, CallbackFunction));
        recording = true;
        for (var index = 0; index < 3; index++) AddBuffer();
        Check(waveInStart(waveIn));
    }

    public string Stop()
    {
        if (!recording) throw new InvalidOperationException("Recorder is not running.");
        FinishCapture();
        var path = Path.Combine(Path.GetTempPath(), $"talktype-{Guid.NewGuid():N}.wav");
        WriteWave(path, audio.ToArray());
        audio.Clear();
        return path;
    }

    private void FinishCapture()
    {
        recording = false;
        Check(waveInStop(waveIn));
        Check(waveInReset(waveIn));
        foreach (var buffer in buffers)
        {
            waveInUnprepareHeader(waveIn, buffer.Header, (uint)Marshal.SizeOf<WaveHeader>());
            Marshal.FreeHGlobal(buffer.Header);
            Marshal.FreeHGlobal(buffer.Data);
        }
        buffers.Clear();
        Check(waveInClose(waveIn));
        waveIn = IntPtr.Zero;
    }

    private void AddBuffer()
    {
        var data = Marshal.AllocHGlobal(BufferSize);
        var header = new WaveHeader { Data = data, BufferLength = BufferSize };
        var headerPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHeader>());
        Marshal.StructureToPtr(header, headerPointer, false);
        Check(waveInPrepareHeader(waveIn, headerPointer, (uint)Marshal.SizeOf<WaveHeader>()));
        Check(waveInAddBuffer(waveIn, headerPointer, (uint)Marshal.SizeOf<WaveHeader>()));
        buffers.Add((headerPointer, data));
    }

    private void OnWaveMessage(IntPtr handle, uint message, IntPtr instance, IntPtr param1, IntPtr param2)
    {
        if (message != WomData) return;
        var header = Marshal.PtrToStructure<WaveHeader>(param1);
        if (header.BytesRecorded > 0)
        {
            var chunk = new byte[header.BytesRecorded];
            Marshal.Copy(header.Data, chunk, 0, chunk.Length);
            lock (audio) audio.AddRange(chunk);
        }
        if (recording) waveInAddBuffer(waveIn, param1, (uint)Marshal.SizeOf<WaveHeader>());
    }

    private static void WriteWave(string path, byte[] data)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + data.Length);
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(16000);
        writer.Write(32000);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(data.Length);
        writer.Write(data);
    }

    private static void Check(uint result)
    {
        if (result != 0) throw new InvalidOperationException($"Windows audio error {result}.");
    }

    public void Dispose()
    {
        if (recording) FinishCapture();
        audio.Clear();
    }

    private delegate void WaveInProc(IntPtr handle, uint message, IntPtr instance, IntPtr param1, IntPtr param2);

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat
    {
        public ushort FormatTag, Channels;
        public uint SamplesPerSecond, AverageBytesPerSecond;
        public ushort BlockAlign, BitsPerSample;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr Data;
        public int BufferLength;
        public int BytesRecorded;
        public IntPtr User;
        public int Flags, Loops;
        public IntPtr Next, Reserved;
    }

    [DllImport("winmm.dll")]
    private static extern uint waveInOpen(out IntPtr handle, uint deviceId, ref WaveFormat format,
        WaveInProc callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")]
    private static extern uint waveInPrepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")]
    private static extern uint waveInUnprepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")]
    private static extern uint waveInAddBuffer(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")]
    private static extern uint waveInStart(IntPtr handle);
    [DllImport("winmm.dll")]
    private static extern uint waveInStop(IntPtr handle);
    [DllImport("winmm.dll")]
    private static extern uint waveInReset(IntPtr handle);
    [DllImport("winmm.dll")]
    private static extern uint waveInClose(IntPtr handle);
}
