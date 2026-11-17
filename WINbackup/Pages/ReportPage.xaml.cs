using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

public sealed partial class ReportPage : Page
{
    readonly BackupRunner? _r = BackupRunner.Current;

    public ReportPage()
    {
        InitializeComponent();
        if (_r == null || !_r.IsFinished)
        {
            TitleText.Text = L.T("Nenhum relatório ainda");
            SubText.Text = L.T("O relatório aparece aqui ao fim de cada backup.");
            ExportButton.Visibility =
                FolderButton.Visibility =
                OpenLogLink.Visibility =
                AttentionTitle.Visibility =
                    Visibility.Collapsed;
            return;
        }
        Fill(_r);
        Loaded += async (_, _) =>
        {
            await MaybeAskRebootAsync(_r);
            await BackupRemindersAsync(_r);
            await MaybeRemindDonationAsync(_r);
        };
    }

    /// <summary>Restauração com drivers: reiniciar é o padrão, a menos que o usuário tenha marcado "Não reiniciar".</summary>
    async Task MaybeAskRebootAsync(BackupRunner r)
    {
        if (!r.RebootRequired || r.Spec.NoReboot || r.RebootAsked || r.State != RunState.Completed)
            return;
        r.RebootAsked = true;
        await Task.Delay(400);
        OnReboot(this, new RoutedEventArgs());
    }

    /// <summary>
    /// Fim do backup (uma vez por execução): lembrete das senhas, se o backup incluiu navegadores, e a janela
    /// para abrir as listas do que reinstalar à mão (programas sem winget e extensões).
    /// </summary>
    async Task BackupRemindersAsync(BackupRunner r)
    {
        if (
            r.EndDialogsShown
            || r.Spec.Kind != JobKind.Backup
            || r.State == RunState.Canceled
            || !Directory.Exists(r.VersionPath)
        )
            return;
        r.EndDialogsShown = true;
        await Task.Delay(400);
        var exportNow =
            r.Spec.Browsers
            && await Dialogs.PasswordReminderAsync(
                XamlRoot,
                r.Spec.Folders.Any(f => f.Group.EndsWith("Senhas"))
            );
        var lists = new List<(string, string, string)>();
        if (ProgramsModule.ManualListPath(r.VersionPath) is { } manual)
            lists.Add(
                (
                    L.T("Programas fora do winget"),
                    L.T(
                        "Não têm pacote no winget: a lista traz um link para procurar o download de cada um."
                    ),
                    manual
                )
            );
        var ext = Path.Combine(r.VersionPath, "Navegadores", "Extensoes.html");
        if (File.Exists(ext))
            lists.Add(
                (
                    L.T("Extensões dos navegadores"),
                    L.T("Com o link da loja de cada extensão, por navegador e perfil."),
                    ext
                )
            );
        if (lists.Count > 0)
            await Dialogs.OpenListsAsync(XamlRoot, lists);
        if (exportNow)
            App.Shell.Navigate("browsers");
    }

    async Task MaybeRemindDonationAsync(BackupRunner r)
    {
        if (
            !IsLoaded
            || r.CountedForDonation
            || r.State != RunState.Completed
            || r.ErrorCount > 0
            || r.Spec.Kind != JobKind.Backup
        )
            return;
        r.CountedForDonation = true;
        if (!Donation.CountSuccessAndCheckReminder())
            return;
        await Task.Delay(600);
        if (await Dialogs.DonationReminderAsync(XamlRoot))
            App.Shell.Navigate("donate");
    }

    void Fill(BackupRunner r)
    {
        var restore = r.Spec.IsRestore;
        var what = restore ? L.T("Restauração") : L.T("Backup");
        var issues = r.WarningCount + r.ErrorCount;
        (TitleText.Text, var color, var bg, var icon) = r.State switch
        {
            RunState.Canceled => (
                restore ? L.T("Restauração cancelada") : L.T("Backup cancelado"),
                UI.Warn,
                "#2a1f0b",
                Icons.Close
            ),
            RunState.Failed => (
                restore ? L.T("A restauração não terminou") : L.T("O backup não terminou"),
                UI.ErrSoft,
                "#2a0d15",
                Icons.Warning
            ),
            _ when r.ErrorCount > 0 => (
                restore
                    ? L.T("Restauração concluída com erros")
                    : L.T("Backup concluído com erros"),
                UI.Warn,
                "#2a1f0b",
                Icons.Warning
            ),
            _ => (
                restore ? L.T("Restauração concluída") : L.T("Backup concluído"),
                UI.Ok,
                "#0b2a1f",
                Icons.Check
            ),
        };
        StatusCircle.BorderBrush = color;
        StatusCircle.Background = UI.Brush(bg);
        StatusIcon.Foreground = color;
        StatusIcon.Data = icon;

        var where = r.Spec.Kind switch
        {
            JobKind.RestoreDrivers => r.Spec.DriverSource,
            JobKind.Restore when r.Spec.InstallPackages != null => L.T("lista importada"),
            JobKind.Restore => string.Join(
                ", ",
                r.Spec.RestoreSources.Select(s => Path.GetFileName(s.VersionPath)).Distinct()
            ),
            _ => r.VersionPath,
        };
        SubText.Text =
            $"{L.T(r.Spec.Title)} · {where}"
            + (
                issues == 0 ? ""
                : issues == 1 ? L.T(" · com 1 aviso")
                : L.F(" · com {0} avisos", issues)
            );

        var onlyDrivers = r.Spec.Kind == JobKind.RestoreDrivers;
        Stat(
            0,
            onlyDrivers ? L.T("Pacotes de driver")
                : restore ? L.T("Itens restaurados")
                : L.T("Itens salvos"),
            r.ItemsSaved.ToString("N0"),
            UI.Text
        );
        if (restore && r.RebootRequired)
            Stat(1, L.T("Reinício"), L.T("Necessário"), UI.Warn);
        else
            Stat(
                1,
                onlyDrivers ? L.T("Reinício")
                    : restore ? L.T("Restaurado")
                    : L.T("Tamanho final"),
                onlyDrivers ? L.T("Não precisa") : Sys.Bytes(r.StoredBytes),
                UI.Text
            );
        Stat(2, L.T("Duração"), Sys.Duration(r.Elapsed), UI.Text);
        var hashed = r.Spec.Kind == JobKind.Restore || (!restore && r.Spec.VerifyHash);
        Stat(
            3,
            L.T("Integridade"),
            hashed ? r.Integrity : "—",
            !hashed ? UI.Muted
                : r.IntegrityOk ? UI.Ok
                : UI.ErrSoft
        );

        foreach (var m in r.Modules)
        {
            var dot = m.Level switch
            {
                StepState.Done => UI.Ok,
                StepState.Warning => UI.Warn,
                StepState.Error => UI.ErrSoft,
                _ => UI.BoxOff,
            };
            var g = new Grid
            {
                ColumnSpacing = 12,
                Padding = new Thickness(18, 11, 18, 11),
                BorderBrush = UI.Brush("#16223d"),
                BorderThickness = new Thickness(0, 0, 0, 1),
            };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(
                new Microsoft.UI.Xaml.Shapes.Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = dot,
                    VerticalAlignment = VerticalAlignment.Center,
                }
            );
            var name = new TextBlock
            {
                Text = L.T(m.Name),
                Foreground = m.Level == StepState.Unavailable ? UI.Muted : UI.Text,
            };
            Grid.SetColumn(name, 1);
            g.Children.Add(name);
            var detail = new TextBlock
            {
                Text = m.Detail,
                FontSize = 12,
                Foreground = UI.Muted2,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(detail, 2);
            g.Children.Add(detail);
            ModulesPanel.Children.Add(g);
        }

        List<string> warnings;
        lock (r.Warnings)
            warnings = r.Warnings.ToList();
        if (r.State == RunState.Canceled)
            warnings.Insert(0, L.T("A tarefa foi cancelada: esta versão está incompleta."));
        if (warnings.Count == 0)
        {
            AttentionTitle.Text = L.T("Tudo certo");
            WarningsPanel.Children.Add(
                new TextBlock
                {
                    Text = L.T("Nenhum aviso nesta execução."),
                    Foreground = UI.Muted,
                    FontSize = 13,
                }
            );
        }
        foreach (var w in warnings.Take(4))
            WarningsPanel.Children.Add(WarnCard(w));
        if (warnings.Count > 4)
            WarningsPanel.Children.Add(
                new TextBlock
                {
                    Text = L.F("+ {0} avisos no log", warnings.Count - 4),
                    Foreground = UI.Muted,
                    FontSize = 12.5,
                }
            );

        FillProblems(r);
        RebootButton.Visibility = r.RebootRequired ? Visibility.Visible : Visibility.Collapsed;
        FolderButton.Visibility = restore ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Lista do que deu errado, por tipo, com caminhos e comandos que dá para selecionar e copiar.</summary>
    void FillProblems(BackupRunner r)
    {
        List<Problem> list;
        lock (r.Problems)
            list = r.Problems.ToList();
        if (list.Count == 0)
            return;
        ProblemsCard.Visibility = Visibility.Visible;
        ProblemsTitle.Text = L.N(list.Count, "O que deu errado (1)", "O que deu errado ({0})");
        CopyCommandsButton.Visibility = list.Any(p => p.Command != null)
            ? Visibility.Visible
            : Visibility.Collapsed;
        OpenProblemsButton.Visibility = File.Exists(r.ProblemsPath)
            ? Visibility.Visible
            : Visibility.Collapsed;
        var mono = (FontFamily)Application.Current.Resources["Mono"];
        const int Shown = 8;

        foreach (var group in list.GroupBy(p => p.Kind).OrderBy(g => g.Key))
        {
            var box = new StackPanel { Spacing = 8 };
            box.Children.Add(
                new TextBlock
                {
                    Text = $"{ProblemReport.Title(group.Key, r.Spec.IsRestore)} ({group.Count()})",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 14,
                }
            );
            var commands = ProblemReport.Commands(group);
            if (commands.Length > 0)
            {
                box.Children.Add(
                    new TextBlock
                    {
                        Text = L.T(
                            "Para tentar de novo, cole no Terminal aberto como administrador:"
                        ),
                        FontSize = 12,
                        Foreground = UI.Muted,
                    }
                );
                box.Children.Add(
                    new Border
                    {
                        Style = (Style)Application.Current.Resources["ConsoleCard"],
                        Padding = new Thickness(14, 10, 14, 10),
                        Child = new TextBlock
                        {
                            Text = commands,
                            FontFamily = mono,
                            FontSize = 12,
                            LineHeight = 19,
                            Foreground = UI.NeonSoft,
                            TextWrapping = TextWrapping.Wrap,
                            IsTextSelectionEnabled = true,
                        },
                    }
                );
            }
            foreach (var p in group.Take(Shown))
            {
                var item = new StackPanel { Spacing = 2, Padding = new Thickness(0, 4, 0, 4) };
                item.Children.Add(
                    new TextBlock
                    {
                        Text = p.What,
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold,
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true,
                    }
                );
                item.Children.Add(
                    new TextBlock
                    {
                        Text = p.Reason,
                        FontSize = 12,
                        Foreground = UI.Warn,
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true,
                    }
                );
                if (p.From != null)
                    item.Children.Add(
                        new TextBlock
                        {
                            Text = r.Spec.IsRestore
                                ? L.F("No backup: {0}", p.From)
                                : L.F("Origem: {0}", p.From),
                            FontFamily = mono,
                            FontSize = 11.5,
                            Foreground = UI.Muted2,
                            TextWrapping = TextWrapping.Wrap,
                            IsTextSelectionEnabled = true,
                        }
                    );
                if (p.To != null && p.To != p.What)
                    item.Children.Add(
                        new TextBlock
                        {
                            Text = L.F("Destino: {0}", p.To),
                            FontFamily = mono,
                            FontSize = 11.5,
                            Foreground = UI.Muted2,
                            TextWrapping = TextWrapping.Wrap,
                            IsTextSelectionEnabled = true,
                        }
                    );
                box.Children.Add(item);
            }
            if (group.Count() > Shown)
                box.Children.Add(
                    new TextBlock
                    {
                        Text = L.F("+ {0} na lista completa", group.Count() - Shown),
                        FontSize = 12,
                        Foreground = UI.Muted,
                    }
                );
            ProblemsPanel.Children.Add(box);
        }
    }

    void OnCopyList(object sender, RoutedEventArgs e)
    {
        if (_r == null)
            return;
        Copy(ProblemReport.Text(_r));
        CopyListButton.Content = L.T("Copiado");
    }

    void OnCopyCommands(object sender, RoutedEventArgs e)
    {
        if (_r == null)
            return;
        lock (_r.Problems)
            Copy(ProblemReport.Commands(_r.Problems));
        CopyCommandsButton.Content = L.T("Copiado");
    }

    void OnOpenProblems(object sender, RoutedEventArgs e)
    {
        if (_r?.ProblemsPath is { Length: > 0 } p && File.Exists(p))
            Sys.Open(p);
    }

    static void Copy(string text)
    {
        var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dp.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
    }

    void Stat(int col, string label, string value, Brush color)
    {
        var s = new StackPanel { Spacing = 4 };
        s.Children.Add(
            new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = UI.Muted,
            }
        );
        s.Children.Add(
            new TextBlock
            {
                Text = value,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = color,
                TextWrapping = TextWrapping.Wrap,
            }
        );
        var b = new Border
        {
            Style = (Style)Application.Current.Resources["Card"],
            Padding = new Thickness(18, 16, 18, 16),
            Child = s,
        };
        Grid.SetColumn(b, col);
        StatsGrid.Children.Add(b);
    }

    static Border WarnCard(string text)
    {
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.Children.Add(new Icon { Data = Icons.Warning, Foreground = UI.Warn });
        var t = new TextBlock
        {
            Text = text,
            FontSize = 13,
            LineHeight = 19,
            Foreground = UI.Brush("#ffd89a"),
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetColumn(t, 1);
        g.Children.Add(t);
        return new Border
        {
            Style = (Style)Application.Current.Resources["WarnCard"],
            Padding = new Thickness(16, 14, 16, 14),
            Child = g,
        };
    }

    void OnOpenLog(object sender, RoutedEventArgs e)
    {
        if (_r?.LogPath is { Length: > 0 } p && File.Exists(p))
            Sys.Open(p);
    }

    void OnFolder(object sender, RoutedEventArgs e)
    {
        if (_r?.VersionPath is { Length: > 0 } p && Directory.Exists(p))
            Sys.Open(p);
    }

    void OnDone(object sender, RoutedEventArgs e) => App.Shell.Navigate("home");

    async void OnReboot(object sender, RoutedEventArgs e)
    {
        var ok = await Dialogs.ConfirmAsync(
            XamlRoot,
            L.T("Reiniciar o computador?"),
            L.T("Salve o que estiver aberto em outros programas antes de continuar."),
            L.T("Reiniciar agora"),
            L.T("Depois")
        );
        if (ok)
            Process.Start(
                new ProcessStartInfo(
                    Proc.SystemExe("shutdown.exe"),
                    "/r /t 5 /c \"WINbackup: reiniciando para terminar a instalação dos drivers\""
                )
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                }
            );
    }

    /// <summary>Gera relatorio.html ao lado do backup e abre no navegador.</summary>
    void OnExport(object sender, RoutedEventArgs e)
    {
        if (_r == null)
            return;
        var r = _r;
        static string H(string s) => WebUtility.HtmlEncode(s);
        var sb = new StringBuilder();
        sb.Append(
                "<!doctype html><html lang=\""
                    + L.Culture.Name
                    + "\"><head><meta charset=\"utf-8\"><title>WINbackup · "
                    + L.T("Relatório")
                    + "</title><style>"
            )
            .Append(
                "body{margin:0;background:#0a0f1c;color:#e8f0ff;font:14px 'Segoe UI Variable Text','Segoe UI',system-ui,sans-serif;padding:32px}"
            )
            .Append(
                "h1{font-size:26px;margin:0 0 4px}p.sub{color:#8ea3c7;margin:0 0 20px}.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin-bottom:18px}"
            )
            .Append(
                ".card{background:#0e1526;border:1px solid #1c2a48;border-radius:12px;padding:16px 18px}.lbl{font-size:12px;color:#8ea3c7}.val{font-size:22px;font-weight:700}"
            )
            .Append(
                "table{width:100%;border-collapse:collapse;background:#0e1526;border:1px solid #1c2a48;border-radius:12px;overflow:hidden;margin-bottom:18px}"
            )
            .Append(
                "td,th{padding:10px 16px;border-bottom:1px solid #16223d;text-align:left}th{color:#8ea3c7;font-weight:400;font-size:12px}"
            )
            .Append(
                ".warn{background:#1d1608;border:1px solid #5c4413;color:#ffd89a;border-radius:12px;padding:12px 16px;margin-bottom:8px}"
            )
            .Append(
                "code,pre{font-family:'Cascadia Mono',Consolas,monospace;font-size:12.5px}h2{font-size:17px;margin:22px 0 8px}"
            )
            .Append(
                "pre{background:#070b15;border:1px solid #16223d;border-radius:10px;padding:12px 14px;color:#7befff;white-space:pre-wrap}.why{color:#ffb547;font-size:13px}</style></head><body>"
            );
        sb.Append(
            $"<h1>{H(TitleText.Text)}</h1><p class=\"sub\">{H(SubText.Text)} · {r.StartedAt:g}</p><div class=\"grid\">"
        );
        foreach (var child in StatsGrid.Children.OfType<Border>())
        {
            var texts = ((StackPanel)child.Child).Children.OfType<TextBlock>().ToList();
            sb.Append(
                $"<div class=\"card\"><div class=\"lbl\">{H(texts[0].Text)}</div><div class=\"val\">{H(texts[1].Text)}</div></div>"
            );
        }
        sb.Append($"</div><table><tr><th>{L.T("Módulo")}</th><th>{L.T("Resultado")}</th></tr>");
        foreach (var m in r.Modules)
            sb.Append($"<tr><td>{H(L.T(m.Name))}</td><td>{H(m.Detail)}</td></tr>");
        sb.Append("</table>");
        lock (r.Warnings)
            foreach (var w in r.Warnings)
                sb.Append($"<div class=\"warn\">{H(w)}</div>");
        List<Problem> problems;
        lock (r.Problems)
            problems = r.Problems.ToList();
        foreach (var group in problems.GroupBy(p => p.Kind).OrderBy(g => g.Key))
        {
            sb.Append(
                $"<h2>{H(ProblemReport.Title(group.Key, r.Spec.IsRestore))} ({group.Count()})</h2>"
            );
            if (ProblemReport.Commands(group) is { Length: > 0 } cmds)
                sb.Append(
                    $"<p class=\"sub\">{H(L.T("Para tentar de novo, cole no Terminal aberto como administrador:"))}</p><pre>{H(cmds)}</pre>"
                );
            sb.Append("<table>");
            foreach (var p in group)
                sb.Append(
                    $"<tr><td><b>{H(p.What)}</b><br><span class=\"why\">{H(p.Reason)}</span>"
                        + (
                            p.From != null
                                ? $"<br><code>{H(r.Spec.IsRestore ? L.F("No backup: {0}", p.From) : L.F("Origem: {0}", p.From))}</code>"
                                : ""
                        )
                        + (
                            p.To != null && p.To != p.What
                                ? $"<br><code>{H(L.F("Destino: {0}", p.To))}</code>"
                                : ""
                        )
                        + "</td></tr>"
                );
            sb.Append("</table>");
        }
        sb.Append(
            $"<p class=\"sub\">{L.T("Log completo:")} <code>{H(r.LogPath)}</code></p></body></html>"
        );

        var folder =
            r.Spec.Kind == JobKind.Backup && Directory.Exists(r.VersionPath)
                ? r.VersionPath
                : Path.GetDirectoryName(r.LogPath)!;
        var file = Path.Combine(folder, "relatorio.html");
        File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
        Sys.Open(file);
    }
}
