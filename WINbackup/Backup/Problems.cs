using System.Text;
using WINbackup.Services;

namespace WINbackup.Backup;

public enum ProblemKind
{
    File,
    Program,
    Driver,
    Item,
}

/// <summary>
/// Algo que não deu certo numa execução, com o necessário para resolver à mão: o arquivo (de onde e para onde),
/// o programa (com o comando do winget) ou o driver (com o comando do PnPUtil).
/// </summary>
public record Problem(
    ProblemKind Kind,
    string What,
    string Reason,
    string? From = null,
    string? To = null,
    string? Command = null
);

public static class ProblemReport
{
    public static string Title(ProblemKind kind, bool restore) =>
        kind switch
        {
            ProblemKind.File => restore
                ? L.T("Arquivos que não foram restaurados")
                : L.T("Arquivos que não foram copiados"),
            ProblemKind.Program => L.T("Programas que não foram instalados"),
            ProblemKind.Driver => L.T("Drivers com problema"),
            _ => L.T("Outros problemas"),
        };

    /// <summary>Comandos para tentar de novo à mão (programas e drivers), um por linha.</summary>
    public static string Commands(IEnumerable<Problem> list) =>
        string.Join(
            Environment.NewLine,
            list.Where(p => p.Command != null).Select(p => p.Command).Distinct()
        );

    /// <summary>Lista completa em texto: agrupada, com caminhos e comandos.</summary>
    public static string Text(BackupRunner r)
    {
        List<Problem> list;
        lock (r.Problems)
            list = r.Problems.ToList();
        var restore = r.Spec.IsRestore;
        var sb = new StringBuilder();
        sb.AppendLine($"WINbackup · {L.T(r.Spec.Title)} · {r.StartedAt:g}");
        sb.AppendLine(L.F("{0} problemas. O resto da tarefa foi feito normalmente.", list.Count));
        if (r.LogPath.Length > 0)
            sb.AppendLine(L.F("Log completo: {0}", r.LogPath));
        foreach (var group in list.GroupBy(p => p.Kind).OrderBy(g => g.Key))
        {
            sb.AppendLine();
            sb.AppendLine($"== {Title(group.Key, restore)} ({group.Count()}) ==");
            if (group.Any(p => p.Command != null))
            {
                sb.AppendLine(
                    L.T(
                        "Para tentar de novo, cole no Terminal (PowerShell ou Prompt de Comando) aberto como administrador:"
                    )
                );
                sb.AppendLine();
                sb.AppendLine(Commands(group));
                sb.AppendLine();
            }
            foreach (var p in group)
            {
                sb.AppendLine($"- {p.What}");
                sb.AppendLine($"  {p.Reason}");
                if (p.From != null)
                    sb.AppendLine(
                        "  "
                            + (restore ? L.F("No backup: {0}", p.From) : L.F("Origem: {0}", p.From))
                    );
                if (p.To != null)
                    sb.AppendLine("  " + L.F("Destino: {0}", p.To));
            }
        }
        return sb.ToString();
    }
}
