namespace WINbackup.Backup;

/// <summary>
/// Onde está, de fato, um arquivo guardado. Files[].Stored começa pelo nome que a pasta da versão tinha quando
/// o arquivo foi copiado ("WINbackup_2026-11-16_14-51-33\Arquivos\…"); se a pasta foi renomeada depois, o nome
/// original é achado pelo manifesto dela.
/// </summary>
public static class StoredFiles
{
    static readonly Dictionary<string, (DateTime Stamp, Dictionary<string, string> Map)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Caminho completo de um Stored dentro da pasta de backups.</summary>
    public static string Path(string root, string stored)
    {
        var sep = stored.IndexOf('\\');
        if (sep <= 0)
            return System.IO.Path.Combine(root, stored);
        var folder = VersionFolder(root, stored[..sep]);
        return folder == null
            ? System.IO.Path.Combine(root, stored)
            : System.IO.Path.Combine(folder, stored[(sep + 1)..]);
    }

    /// <summary>Pasta atual da versão que se chamava <paramref name="name"/> (null se não estiver na pasta de backups).</summary>
    public static string? VersionFolder(string root, string name)
    {
        var direct = System.IO.Path.Combine(root, name);
        if (Directory.Exists(direct))
            return direct;
        return Map(root).TryGetValue(name, out var actual) ? actual : null;
    }

    static Dictionary<string, string> Map(string root)
    {
        DateTime stamp;
        try
        {
            stamp = Directory.GetLastWriteTimeUtc(root);
        }
        catch (Exception)
        {
            return new();
        }
        lock (Cache)
        {
            // Renomear ou criar uma subpasta muda a data da pasta de backups
            if (Cache.TryGetValue(root, out var hit) && hit.Stamp == stamp)
                return hit.Map;
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in Services.Destinations.Versions(root))
                if (Manifest.Load(v) is { } m)
                    foreach (var n in m.OriginalNames())
                        map.TryAdd(n, v);
            Cache[root] = (stamp, map);
            return map;
        }
    }
}
