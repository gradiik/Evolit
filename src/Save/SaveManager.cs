using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Evolit.Session;
using Evolit.Storage;
using Godot;

namespace Evolit.Save;

public sealed class SaveManager
{
    public const int SchemaVersion = 1;
    public const int AutosaveSlots = 5;
    public const string GameVersion = "dev";
    public const string ManualType = "manual";
    public const string AutosaveType = "autosave";

    private readonly string _saveDirectory;
    private readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public SaveManager()
    {
        _saveDirectory = ProjectSettings.GlobalizePath("user://saves");
        Directory.CreateDirectory(_saveDirectory);
    }

    public SaveOperationResult SaveManual(GameSession session)
    {
        var document = BuildDocument(session, ManualType, null);
        var path = Path.Combine(_saveDirectory, $"manual_{session.SaveId}.json");
        return WriteDocument(path, document);
    }

    public SaveOperationResult CreateAutosave(GameSession session)
    {
        var slots = ListSaves()
            .Where(slot => slot.Document?.SaveType == AutosaveType && slot.Document.AutosaveIndex.HasValue)
            .OrderByDescending(slot => slot.Document!.SavedAt)
            .ToList();

        var nextIndex = slots.Count == 0
            ? 1
            : (slots[0].Document!.AutosaveIndex!.Value % AutosaveSlots) + 1;

        var document = BuildDocument(session, AutosaveType, nextIndex);
        var path = Path.Combine(_saveDirectory, $"autosave_{nextIndex}.json");
        return WriteDocument(path, document);
    }

    public IReadOnlyList<SaveSlot> ListSaves()
    {
        Directory.CreateDirectory(_saveDirectory);

        var slots = new List<SaveSlot>();
        foreach (var path in Directory.GetFiles(_saveDirectory, "*.json", SearchOption.TopDirectoryOnly))
            slots.Add(Inspect(path));

        return slots
            .OrderByDescending(slot => slot.Document?.SavedAt ?? DateTimeOffset.MinValue)
            .ThenBy(slot => slot.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public SaveSlot? GetLatestLoadable()
    {
        return ListSaves().FirstOrDefault(slot => slot.CanLoad);
    }

    public bool TryLoad(SaveSlot slot, out SaveDocument document, out bool recoveredFromBackup, out string error)
    {
        recoveredFromBackup = false;

        if (TryReadDocument(slot.Path, out document, out error))
            return true;

        if (!TryReadDocument(slot.BackupPath, out document, out var backupError))
        {
            error = string.IsNullOrWhiteSpace(error)
                ? backupError
                : $"{error} Backup: {backupError}";
            return false;
        }

        if (!AtomicFile.RestoreBackup(slot.Path, out var restoreError))
        {
            error = $"Backup валиден, но восстановить основной файл не удалось: {restoreError}";
            return false;
        }

        recoveredFromBackup = true;
        error = string.Empty;
        return true;
    }

    public bool Delete(SaveSlot slot, out string error)
    {
        error = string.Empty;

        try
        {
            if (File.Exists(slot.Path))
                File.Delete(slot.Path);
            if (File.Exists(slot.BackupPath))
                File.Delete(slot.BackupPath);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private SaveOperationResult WriteDocument(string path, SaveDocument document)
    {
        document.SavedAt = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(document, _json);

        return AtomicFile.Write(path, json, true, out var error)
            ? SaveOperationResult.Ok(path)
            : SaveOperationResult.Fail(error);
    }

    private SaveDocument BuildDocument(GameSession session, string saveType, int? autosaveIndex)
    {
        return new SaveDocument
        {
            SchemaVersion = SchemaVersion,
            SaveId = session.SaveId,
            WorldName = session.WorldName,
            Seed = session.Seed,
            WorldSize = session.WorldSize,
            CreatedAt = session.CreatedAt,
            SavedAt = DateTimeOffset.UtcNow,
            PlaytimeSeconds = session.PlaytimeSeconds,
            GameVersion = GameVersion,
            SaveType = saveType,
            AutosaveIndex = autosaveIndex,
            Payload = new Dictionary<string, string>()
        };
    }

    private SaveSlot Inspect(string path)
    {
        if (TryReadDocument(path, out var document, out var error))
        {
            return new SaveSlot
            {
                Path = path,
                Document = document,
                Status = SaveSlotStatus.Valid
            };
        }

        var backupPath = path + ".bak";
        if (TryReadDocument(backupPath, out var backup, out _))
        {
            return new SaveSlot
            {
                Path = path,
                Document = backup,
                Status = SaveSlotStatus.Recoverable,
                Error = error
            };
        }

        return new SaveSlot
        {
            Path = path,
            Status = SaveSlotStatus.Invalid,
            Error = error
        };
    }

    private bool TryReadDocument(string path, out SaveDocument document, out string error)
    {
        document = new SaveDocument();
        error = string.Empty;

        try
        {
            if (!File.Exists(path))
            {
                error = "Файл не найден.";
                return false;
            }

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Файл пуст.";
                return false;
            }

            var parsed = JsonSerializer.Deserialize<SaveDocument>(json, _json);
            if (parsed is null)
            {
                error = "JSON не содержит сохранение.";
                return false;
            }

            if (!Validate(parsed, out error))
                return false;

            document = parsed;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool Validate(SaveDocument document, out string error)
    {
        if (document.SchemaVersion != SchemaVersion)
        {
            error = $"Неподдерживаемая версия сохранения: {document.SchemaVersion}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(document.SaveId) || string.IsNullOrWhiteSpace(document.WorldName))
        {
            error = "В сохранении отсутствуют обязательные метаданные.";
            return false;
        }

        if (document.SaveType != ManualType && document.SaveType != AutosaveType)
        {
            error = "Неизвестный тип сохранения.";
            return false;
        }

        if (document.SaveType == AutosaveType &&
            (!document.AutosaveIndex.HasValue ||
             document.AutosaveIndex.Value < 1 ||
             document.AutosaveIndex.Value > AutosaveSlots))
        {
            error = "Некорректный индекс автосохранения.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

public sealed class SaveOperationResult
{
    public bool Success { get; private init; }
    public string Path { get; private init; } = string.Empty;
    public string Error { get; private init; } = string.Empty;

    public static SaveOperationResult Ok(string path) => new() { Success = true, Path = path };
    public static SaveOperationResult Fail(string error) => new() { Success = false, Error = error };
}
