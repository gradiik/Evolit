namespace Evolit.Save;

public enum SaveSlotStatus
{
    Valid,
    Recoverable,
    Invalid
}

public sealed class SaveSlot
{
    public string Path { get; init; } = string.Empty;
    public string BackupPath => Path + ".bak";
    public SaveDocument? Document { get; init; }
    public SaveSlotStatus Status { get; init; }
    public string Error { get; init; } = string.Empty;
    public bool CanLoad => Status is SaveSlotStatus.Valid or SaveSlotStatus.Recoverable;
}
