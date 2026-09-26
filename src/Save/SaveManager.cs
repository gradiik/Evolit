using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Evolit.Core;
using Evolit.Game;
using Evolit.Session;
using Evolit.Storage;
using Evolit.Versioning;
using Godot;

namespace Evolit.Save;

public sealed class SaveManager
{
    public const int SchemaVersion = 1;
    public const int AutosaveSlots = 5;
    public const string ManualType = "manual";
    public const string AutosaveType = "autosave";

    private readonly string _saveDirectory;
    private readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public SaveManager()
    {
        _saveDirectory = ProjectSettings.GlobalizePath("user://saves");
        Directory.CreateDirectory(_saveDirectory);
    }

    public SaveOperationResult SaveManual(
        GameSession session,
        GameTimeController? time = null,
        SimulationSpeedState? speed = null,
        DemoWorldDataProvider? world = null,
        CoreSimulationSnapshot? core = null)
    {
        var document = BuildDocument(session, ManualType, null, time, speed, world, core);
        var path = Path.Combine(_saveDirectory, $"manual_{session.SaveId}.json");
        return WriteDocument(path, document);
    }

    public SaveOperationResult CreateAutosave(
        GameSession session,
        GameTimeController? time = null,
        SimulationSpeedState? speed = null,
        DemoWorldDataProvider? world = null,
        CoreSimulationSnapshot? core = null)
    {
        var slots = ListSaves()
            .Where(slot => slot.Document?.SaveType == AutosaveType && slot.Document.AutosaveIndex.HasValue)
            .OrderByDescending(slot => slot.Document!.SavedAt)
            .ToList();

        var nextIndex = slots.Count == 0
            ? 1
            : (slots[0].Document!.AutosaveIndex!.Value % AutosaveSlots) + 1;

        var document = BuildDocument(session, AutosaveType, nextIndex, time, speed, world, core);
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

        return AtomicFile.Write(
                path,
                stream => JsonSerializer.Serialize(stream, document, _json),
                true,
                out var error)
            ? SaveOperationResult.Ok(path)
            : SaveOperationResult.Fail(error);
    }

    private SaveDocument BuildDocument(
        GameSession session,
        string saveType,
        int? autosaveIndex,
        GameTimeController? time,
        SimulationSpeedState? speed,
        DemoWorldDataProvider? world,
        CoreSimulationSnapshot? core)
    {
        return new SaveDocument
        {
            SchemaVersion = SchemaVersion,
            SaveId = session.SaveId,
            WorldName = session.WorldName,
            Seed = session.Seed,
            WorldSize = session.WorldSize,
            LandAmount = (int)session.LandAmount,
            Climate = (int)session.Climate,
            GeologicalActivity = (int)session.Geology,
            CreatedAt = session.CreatedAt,
            SavedAt = DateTimeOffset.UtcNow,
            PlaytimeSeconds = session.PlaytimeSeconds,
            GameVersion = AppVersionCatalog.CurrentVersion,
            SaveType = saveType,
            AutosaveIndex = autosaveIndex,
            Payload = new Dictionary<string, string>(),
            Runtime = time is not null && speed is not null && world is not null
                ? new GameRuntimeSaveState
                {
                    Time = new GameTimeSaveState
                    {
                        Day = time.Day,
                        MinuteOfDay = time.MinuteOfDay,
                        TickCount = time.TickCount
                    },
                    Speed = new SimulationSpeedSaveState
                    {
                        Paused = speed.Paused,
                        Multiplier = speed.Multiplier
                    },
                    World = world.CaptureSaveState(),
                    Core = core
                }
                : null
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

            if (new FileInfo(path).Length == 0)
            {
                error = "Файл пуст.";
                return false;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var parsed = JsonSerializer.Deserialize<SaveDocument>(stream, _json);
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

        // Legacy schema-1 saves did not have Runtime. Keep them loadable and let
        // AppRoot rebuild deterministic defaults from seed + world size.
        if (document.Runtime is not null)
        {
            if (document.Runtime.Time.Day < 1)
            {
                error = "Некорректный день в runtime-состоянии.";
                return false;
            }
            if (document.Runtime.Speed.Multiplier <= 0)
            {
                error = "Некорректная скорость в runtime-состоянии.";
                return false;
            }
            if (document.Runtime.World.Entities.Count == 0 || document.Runtime.World.Species.Count == 0)
            {
                error = "Runtime-состояние не содержит обязательные данные мира.";
                return false;
            }
            if (document.Runtime.World.Map is { Cells.Count: 0 })
            {
                error = "Сохранённая карта мира не содержит клеток.";
                return false;
            }

            if (document.Runtime.Core is not null)
            {
                if (document.Runtime.Core.Version is < 1 or > CoreSimulation.SnapshotVersion)
                {
                    error = $"Неподдерживаемая версия Evolit Core snapshot: {document.Runtime.Core.Version}.";
                    return false;
                }

                if (document.Runtime.Core.Topology?.Cells.Length is null or 0)
                {
                    error = "Evolit Core snapshot не содержит topology.";
                    return false;
                }
            }
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
