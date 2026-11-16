using System.Runtime.InteropServices;
using WINbackup.Controls;

namespace WINbackup.Services;

public record DestOption(string Label, string Path, string Icon);

/// <summary>Discos onde dá para salvar os backups e o espaço livre de cada um.</summary>
public static partial class Destinations
{
    public static List<DestOption> Candidates()
    {
        var system = System.IO.Path.GetPathRoot(Environment.SystemDirectory);
        var list = new List<DestOption>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady || string.Equals(d.Name, system, StringComparison.OrdinalIgnoreCase)) continue;
                if (d.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                list.Add(new DestOption(LabelFor(d), System.IO.Path.Combine(d.Name, "Backup"),
                    d.DriveType == DriveType.Network ? Icons.Network : Icons.Drive));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return list;
    }

    static string LabelFor(DriveInfo d)
    {
        var letter = d.Name.TrimEnd('\\');
        if (!string.IsNullOrWhiteSpace(d.VolumeLabel)) return $"{d.VolumeLabel} ({letter})";
        return d.DriveType switch
        {
            DriveType.Removable => L.F("Disco removível ({0})", letter),
            DriveType.Network => L.F("Unidade de rede ({0})", letter),
            _ => L.F("Disco local ({0})", letter),
        };
    }

    /// <summary>Nome amigável para qualquer caminho (inclusive pastas escolhidas à mão).</summary>
    public static DestOption Describe(string path)
    {
        if (string.IsNullOrEmpty(path)) return new DestOption(L.T("Nenhum destino"), L.T("Escolha onde salvar"), Icons.Drive);
        var match = Candidates().FirstOrDefault(c => string.Equals(c.Path, path, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;
        if (path.StartsWith(@"\\")) return new DestOption("Pasta na rede", path, Icons.Network);
        try
        {
            var root = System.IO.Path.GetPathRoot(path);
            if (root != null)
            {
                var d = new DriveInfo(root);
                if (d.IsReady) return new DestOption(LabelFor(d), path, Icons.Drive);
            }
        }
        catch (ArgumentException) { }
        catch (IOException) { }
        return new DestOption(L.T("Pasta escolhida"), path, Icons.Drive);
    }

    /// <summary>Espaço livre e total do volume do caminho (funciona com caminhos de rede).</summary>
    public static (long Free, long Total)? Space(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var probe = path;
        while (!string.IsNullOrEmpty(probe) && !Directory.Exists(probe))
            probe = System.IO.Path.GetDirectoryName(probe);
        if (string.IsNullOrEmpty(probe)) probe = System.IO.Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(probe)) return null;
        if (!GetDiskFreeSpaceEx(probe, out var free, out var total, out _)) return null;
        return ((long)free, (long)total);
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetDiskFreeSpaceExW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetDiskFreeSpaceEx(string dir, out ulong freeForCaller, out ulong total, out ulong totalFree);

    /// <summary>
    /// Pasta com versões (WINbackup_*) a partir de um caminho escolhido: a própria pasta, a pasta acima de uma versão
    /// ou uma subpasta logo abaixo (escolher E:\ acha E:\Backup). Null se não houver backup.
    /// </summary>
    public static string? BackupRoot(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return null;
        if (Versions(path).Count > 0) return path;
        var name = System.IO.Path.GetFileName(path.TrimEnd('\\'));
        if (name.StartsWith("WINbackup_", StringComparison.OrdinalIgnoreCase) && System.IO.Path.GetDirectoryName(path.TrimEnd('\\')) is { } parent)
            return parent;
        return Subdirs(path).Where(d => Versions(d).Count > 0).OrderByDescending(Newest, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }

    /// <summary>Procura backups nos discos conectados (raiz e um nível abaixo); devolve a pasta com a versão mais nova.</summary>
    public static string? FindBackups()
    {
        var roots = new List<string>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady || d.DriveType is not (DriveType.Fixed or DriveType.Removable or DriveType.Network)) continue;
                roots.Add(d.RootDirectory.FullName);
                roots.AddRange(Subdirs(d.RootDirectory.FullName));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return roots.Where(r => Versions(r).Count > 0).OrderByDescending(Newest, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }

    /// <summary>De onde restaurar: a pasta escolhida em Restaurar backup, o destino dos backups ou o que achar nos discos.</summary>
    public static string? RestoreFolder() =>
        BackupRoot(Settings.Data.RestoreFrom) ?? BackupRoot(Settings.Data.Destination) ?? FindBackups();

    static string Newest(string root) => Versions(root).Select(v => System.IO.Path.GetFileName(v)).FirstOrDefault() ?? "";

    static string[] Subdirs(string path)
    {
        try { return Directory.GetDirectories(path); }
        catch (Exception) { return []; }
    }

    /// <summary>Pastas de versão (WINbackup_*) no destino atual, da mais nova para a mais antiga.</summary>
    public static List<string> Versions(string? dest = null)
    {
        dest ??= Settings.Data.Destination;
        try
        {
            if (string.IsNullOrEmpty(dest) || !Directory.Exists(dest)) return new();
            return Directory.GetDirectories(dest, "WINbackup_*").OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception)
        {
            return new();
        }
    }
}
