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
        string pending = path + ".pending";
        // Flush before touching the window: recovery must survive helper failure.
        // Publish only a complete record. Failure during writing cannot leave
        // half a JSON record blocking the next launch before any mask existed.
        try
        {
            using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(stream, record); stream.Flush(true); }
            File.Move(pending, path);
        }
        finally { if (File.Exists(pending)) File.Delete(pending); }
        return path;
    }
    internal bool Recover()
    {
        if (!Directory.Exists(DirectoryPath)) return true;
        // The worker mutex excludes a live writer. A pending file was never
        // published and therefore never authorized a window mutation. Clean
        // only our generated names; unrelated files in this folder are kept.
        foreach (string path in Directory.EnumerateFiles(DirectoryPath, "*.json.pending").Take(64))
            if (Guid.TryParseExact(Path.GetFileName(path)[..^13], "N", out _)) File.Delete(path);
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
        // The bounded batch must not report success while additional masks
        // remain. In particular, uninstall may proceed only after all records.
        return recovered && !HasRecords;
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
