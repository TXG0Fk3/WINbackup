using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;
using Windows.System;
using Windows.UI.Core;

namespace WINbackup.Pages;

public sealed partial class HomePage : Page
{
    readonly List<ToggleButton> _cards = new();

    public HomePage()
    {
        InitializeComponent();
        var chosen = Settings.Data.ChosenPresets();
        for (var i = 0; i < Presets.All.Length; i++)
        {
            var p = Presets.All[i];
            var index = i;
            var content = new StackPanel { Spacing = 8 };
            content.Children.Add(
                new TextBlock
                {
                    Text = p.Label,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                }
            );
            content.Children.Add(
                new TextBlock
                {
                    Text = p.Info,
                    FontSize = 11.5,
                    LineHeight = 16,
                    Foreground = UI.Muted,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                }
            );
            var card = new ToggleButton
            {
                Style = (Style)Application.Current.Resources["PresetToggle"],
                Tag = p.Icon,
                Content = content,
                IsChecked = chosen.Contains(i),
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(card, p.Label);
            card.Click += (_, _) => Choose(index);
            Grid.SetColumn(card, i);
            PresetGrid.Children.Add(card);
            _cards.Add(card);
        }
        Loaded += (_, _) =>
        {
            Settings.DestinationChanged += Refresh;
            Refresh();
        };
        Unloaded += (_, _) => Settings.DestinationChanged -= Refresh;
    }

    /// <summary>Clique escolhe só este preset; Ctrl + clique marca ou desmarca este e mantém os outros.</summary>
    void Choose(int index)
    {
        var ctrl = InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(CoreVirtualKeyStates.Down);
        var chosen = Settings.Data.ChosenPresets();
        if (!ctrl)
            chosen = [index];
        else if (chosen.Contains(index))
        {
            if (chosen.Count > 1)
                chosen.Remove(index);
        }
        else
            chosen.Add(index);
        chosen.Sort();
        Settings.Data.Presets = chosen;
        Settings.Data.Preset = chosen[0];
        Settings.Save();
        for (var i = 0; i < _cards.Count; i++)
            _cards[i].IsChecked = chosen.Contains(i);
        Refresh();
    }

    static string ChosenLabel =>
        string.Join(" + ", Settings.Data.ChosenPresets().Select(i => Presets.All[i].Label));

    void Refresh()
    {
        var chosen = Settings.Data.ChosenPresets();
        BackupSub.Text =
            chosen.Count == 1
                ? L.F("Preset: {0}", ChosenLabel)
                : L.F("{0} presets: {1}", chosen.Count, ChosenLabel);
        // Sem procurar nos discos aqui (é lento): a tela Restaurar backup faz isso
        var folder = Destinations.BackupRoot(Settings.Data.RestoreFrom)
            ?? Destinations.BackupRoot(Settings.Data.Destination);
        var last = folder == null ? null : Destinations.Versions(folder).FirstOrDefault();
        RestoreSub.Text =
            last == null
                ? L.T("Acha o backup e deixa o PC como estava")
                : L.F(
                    "Último backup: {0:g} · {1}",
                    VersionDate(last),
                    Path.GetPathRoot(folder!) ?? ""
                );
    }

    public static DateTime? VersionDate(string versionPath) =>
        Destinations.VersionDate(versionPath);

    async void OnBackup(object sender, RoutedEventArgs e)
    {
        if (BackupRunner.Current is { IsFinished: false })
        {
            App.Shell.Navigate("progress");
            return;
        }
        var chosen = Settings.Data.ChosenPresets();
        var spec = Presets.Build(chosen);

        var summary = new StackPanel
        {
            Spacing = 10,
            Padding = new Thickness(16, 14, 16, 14),
            CornerRadius = new CornerRadius(10),
            Background = UI.Brush("#0a1120"),
            BorderBrush = UI.Brush("#16223d"),
            BorderThickness = new Thickness(1),
        };
        summary.Children.Add(
            Row(chosen.Count == 1 ? L.T("Preset") : L.T("Presets"), ChosenLabel, mono: false)
        );
        summary.Children.Add(
            Row(
                L.T("Destino"),
                string.IsNullOrEmpty(spec.Destination) ? L.T("Não escolhido") : spec.Destination,
                mono: true
            )
        );
        var runs = new List<string>();
        var personal = spec.Folders.Count(f => f.Group == "Arquivos");
        if (personal > 0)
            runs.Add(L.F("Arquivos pessoais ({0} pastas)", personal));
        var saves = spec.Folders.Count(f => f.Group.StartsWith("Saves de jogos"));
        if (saves > 0)
            runs.Add(L.F("Saves de jogos ({0})", saves));
        var creators = spec
            .Folders.Where(f => f.Group.StartsWith("Criadores"))
            .Select(f => f.Group)
            .Distinct()
            .Count();
        if (creators > 0)
            runs.Add(L.F("Criadores ({0} apps)", creators));
        if (
            spec.Folders.Any(f => f.Group.StartsWith("Configurações"))
            || spec.WindowsKeys.Count > 0
        )
            runs.Add(L.T("Configurações"));
        if (spec.Browsers)
            runs.Add(L.T("Navegadores"));
        if (spec.Programs)
            runs.Add(L.T("Programas"));
        if (spec.Drivers)
            runs.Add(L.T("Drivers"));
        summary.Children.Add(
            Row(
                L.T("Nesta versão"),
                runs.Count > 0 ? string.Join(", ", runs) : L.T("Nada ainda"),
                mono: false
            )
        );

        var body = new StackPanel { Spacing = 18 };
        body.Children.Add(summary);
        if (spec.Unavailable.Count > 0)
            body.Children.Add(
                Dialogs.Para(
                    L.F(
                        "Em breve: {0}. Essas etapas aparecem no progresso, mas ainda não salvam nada.",
                        string.Join(", ", spec.Unavailable)
                    ),
                    muted: true
                )
            );
        body.Children.Add(
            Dialogs.Para(
                L.T("Você pode continuar usando o PC enquanto o backup roda."),
                muted: true
            )
        );

        if (string.IsNullOrEmpty(spec.Destination))
        {
            await Dialogs.InfoAsync(
                XamlRoot,
                L.T("Escolha o destino"),
                L.T(
                    "Clique em Destino dos backups, no menu lateral, e escolha um HD externo ou uma pasta."
                )
            );
            return;
        }
        if (!spec.HasWork)
        {
            await Dialogs.InfoAsync(
                XamlRoot,
                L.F("{0}: em breve", ChosenLabel),
                L.F(
                    "Este preset usa módulos que ainda não executam nesta versão ({0}). Escolha Backup completo ou Só documentos.",
                    string.Join(", ", spec.Unavailable)
                )
            );
            return;
        }
        if (
            await Dialogs.ConfirmAsync(
                XamlRoot,
                L.T("Iniciar o backup agora?"),
                body,
                L.T("Sim, iniciar backup"),
                L.T("Agora não")
            )
        )
            App.Shell.Run(spec);
    }

    static Grid Row(string label, string value, bool mono)
    {
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.Children.Add(
            new TextBlock
            {
                Text = label,
                FontSize = 13,
                Foreground = UI.Muted,
            }
        );
        var v = new TextBlock
        {
            Text = value,
            FontSize = mono ? 12.5 : 13,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.Wrap,
        };
        if (mono)
            v.FontFamily = (FontFamily)Application.Current.Resources["Mono"];
        Grid.SetColumn(v, 1);
        g.Children.Add(v);
        return g;
    }

    void OnRestore(object sender, RoutedEventArgs e) => App.Shell.Navigate("restore");

    void OnManagePresets(object sender, RoutedEventArgs e) => App.Shell.Navigate("schedule");
}
