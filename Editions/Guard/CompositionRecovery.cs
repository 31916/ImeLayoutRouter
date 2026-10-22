using System.Text.Json;

// Only window identity and region geometry are stored. No captions, input,
// keystrokes or application documents are collected.
internal sealed class CompositionRecovery
{
    internal string DirectoryPath { get; }
    internal CompositionRecovery(string? directory = null) => DirectoryPath = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImeLayoutRouter", "v3-recovery");
    internal bool HasRecords => Directory.Exists(DirectoryPath) && Directory.EnumerateFiles(DirectoryPath, "*.json").Any();
    internal string Save(CompositionMaskRecord record)
    {
        Directory.CreateDirectory(DirectoryPath);
        string path = Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N") + ".json");
        // Flush before touching the window: recovery must survive helper failure.
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, record);
        stream.Flush(true);
        return path;
    }
    internal bool Recover()
    {
        if (!Directory.Exists(DirectoryPath)) return true;
        bool recovered = true;
        foreach (string path in Directory.EnumerateFiles(DirectoryPath, "*.json").Take(64))
        {
            if (new FileInfo(path).Length > 100_000) { recovered = false; continue; }
            try
            {
                var record = JsonSerializer.Deserialize<CompositionMaskRecord>(File.ReadAllText(path));
                if (record is not { Version: 1, Window: not 0, Token: > 0, ProcessId: > 0, ThreadId: > 0 }
                    || record.Token > int.MaxValue || !ValidRegion(record.Region)) { recovered = false; continue; }
                recovered &= CompositionWindowMask.Recover(record, path);
            }
            catch (JsonException) { recovered = false; }
        }
        return recovered;
    }
    internal static bool ValidRegion(byte[]? data)
    {
        if (data == null) return true;
        if (data.Length < 32 || data.Length > 65536) return false;
        uint Read(int offset) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
        uint rectangles = Read(8);
        return Read(0) == 32 && Read(4) == 1 && rectangles <= 4094
            && Read(12) == rectangles * 16 && data.Length == 32 + rectangles * 16;
    }
}

internal sealed record CompositionMaskRecord(int Version, long Window, long Token, uint ProcessId, uint ThreadId, byte[]? Region);
