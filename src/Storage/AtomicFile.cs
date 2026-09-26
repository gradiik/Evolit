using System;
using System.IO;

namespace Evolit.Storage;

public static class AtomicFile
{
    public static bool Write(string path, string content, bool keepBackup, out string error)
    {
        error = string.Empty;
        var tempPath = path + ".tmp";
        var backupPath = path + ".bak";

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            File.WriteAllText(tempPath, content);
            using (var stream = new FileStream(tempPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                stream.Flush(true);

            if (keepBackup && File.Exists(path))
                File.Copy(path, backupPath, true);

            File.Move(tempPath, path, true);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Best effort cleanup only.
            }

            return false;
        }
    }

    public static bool Write(string path, Action<Stream> writer, bool keepBackup, out string error)
    {
        ArgumentNullException.ThrowIfNull(writer);
        error = string.Empty;
        var tempPath = path + ".tmp";
        var backupPath = path + ".bak";

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                writer(stream);
                stream.Flush(true);
            }

            if (keepBackup && File.Exists(path))
                File.Copy(path, backupPath, true);

            File.Move(tempPath, path, true);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Best effort cleanup only.
            }

            return false;
        }
    }

    public static bool RestoreBackup(string path, out string error)
    {
        error = string.Empty;
        var backupPath = path + ".bak";

        try
        {
            if (!File.Exists(backupPath))
            {
                error = "Резервная копия отсутствует.";
                return false;
            }

            File.Copy(backupPath, path, true);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
