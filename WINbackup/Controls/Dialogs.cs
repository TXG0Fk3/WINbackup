using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Controls;

public static class Dialogs
{
    static Style S(string key) => (Style)Application.Current.Resources[key];

    static ContentDialog Base(
        XamlRoot root,
        string title,
        UIElement body,
        string primary,
        string? secondary,
        bool danger = false,
        bool warn = false
    )
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        header.Children.Add(
            new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                BorderThickness = new Thickness(warn || danger ? 1 : 1.5),
                Background = warn || danger ? UI.Brush("#2a1f0b") : UI.Brush("#1a2b52"),
                BorderBrush = warn || danger ? UI.Brush("#5c4413") : UI.Blue,
                Child = new Icon
                {
                    Width = 20,
                    Height = 20,
                    StrokeThickness = 2.25,
                    Data = warn || danger ? Icons.Warning : Icons.Download,
                    Foreground = warn || danger ? UI.Warn : UI.Brush("#ffffff"),
                },
            }
        );
        header.Children.Add(
            new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            }
        );

        var content = new StackPanel { Spacing = 18, Width = 392 };
        content.Children.Add(header);
        content.Children.Add(body);

        var d = new ContentDialog
        {
            XamlRoot = root,
            Content = content,
            PrimaryButtonText = primary,
            CloseButtonText = secondary ?? "",
            DefaultButton = ContentDialogButton.Primary,
            PrimaryButtonStyle = S(
                danger ? "DangerButton"
                : warn ? "WarnButton"
                : "PrimaryButton"
            ),
            CloseButtonStyle = S("SecondaryButton"),
            RequestedTheme = ElementTheme.Dark,
        };
        if (warn || danger)
            d.BorderBrush = UI.Brush("#5c4413");
        return d;
    }

    public static TextBlock Para(string text, bool muted = false) =>
        new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = muted ? 13 : 14,
            LineHeight = muted ? 20 : 21,
            Foreground = muted ? UI.Muted2 : UI.Brush("#c9d6ee"),
        };

    public static async Task<bool> ConfirmAsync(
        XamlRoot root,
        string title,
        string text,
        string primary,
        string secondary,
        bool danger = false
    )
    {
        var d = Base(root, title, Para(text), primary, secondary, danger: danger);
        return await d.ShowAsync() == ContentDialogResult.Primary;
    }

    public static async Task<bool> ConfirmAsync(
        XamlRoot root,
        string title,
        UIElement body,
        string primary,
        string secondary
    )
    {
        var d = Base(root, title, body, primary, secondary);
        return await d.ShowAsync() == ContentDialogResult.Primary;
    }

    public static async Task InfoAsync(XamlRoot root, string title, string text)
    {
        var d = Base(root, title, Para(text, muted: true), L.T("Entendi"), null);
        await d.ShowAsync();
    }

    /// <summary>Lembrete ocasional de doação, com "não mostrar de novo".</summary>
    public static async Task<bool> DonationReminderAsync(XamlRoot root)
    {
        var never = new ToggleButton
        {
            Style = S("CheckCard"),
            Background = UI.Brush("#0a1120"),
            BorderBrush = UI.Brush("#2a3b63"),
            Content = new TextBlock
            {
                Text = L.T("Não mostrar este lembrete de novo"),
                FontSize = 13,
            },
        };
        var body = new StackPanel { Spacing = 14 };
        body.Children.Add(
            Para(
                L.F(
                    "Você já fez {0} backups com o WINbackup. Ele é gratuito e feito pelo Canal WINchester; se está sendo útil, apoie com o valor que achar justo.",
                    Services.Settings.Data.SuccessfulBackups
                )
            )
        );
        body.Children.Add(never);
        var d = Base(
            root,
            L.T("Curtindo o WINbackup?"),
            body,
            L.T("Quero apoiar"),
            L.T("Agora não")
        );
        var result = await d.ShowAsync();
        if (never.IsChecked == true)
        {
            Services.Settings.Data.DonationReminderOff = true;
            Services.Settings.Save();
        }
        return result == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Pop-up de risco do "Não reiniciar o computador": só confirma depois de marcar
    /// "Entendo os riscos… por minha conta" (HANDOFF, decisões já tomadas).
    /// </summary>
    public static async Task<bool> NoRebootAsync(XamlRoot root)
    {
        var ack = new ToggleButton
        {
            Style = S("CheckCard"),
            Background = UI.Brush("#0a1120"),
            BorderBrush = UI.Brush("#2a3b63"),
            Content = new TextBlock
            {
                Text = L.T("Entendo os riscos e quero continuar sem reiniciar, por minha conta."),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
            },
        };
        var body = new StackPanel { Spacing = 16 };
        body.Children.Add(
            Para(
                L.T(
                    "Depois de instalar drivers, o Windows precisa reiniciar para carregá-los corretamente. Sem reiniciar, alguns dispositivos podem não funcionar, ter conflitos ou causar erros até o próximo reinício."
                )
            )
        );
        body.Children.Add(
            Para(
                L.T(
                    "Os drivers serão instalados normalmente. Só o reinício automático será pulado, e você reinicia quando quiser."
                ),
                muted: true
            )
        );
        body.Children.Add(ack);

        var d = Base(
            root,
            L.T("Reiniciar é recomendado"),
            body,
            L.T("Continuar sem reiniciar"),
            L.T("Manter reinício"),
            warn: true
        );
        d.DefaultButton = ContentDialogButton.Close;
        d.IsPrimaryButtonEnabled = false;
        ack.Checked += (_, _) => d.IsPrimaryButtonEnabled = true;
        ack.Unchecked += (_, _) => d.IsPrimaryButtonEnabled = false;
        return await d.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Pop-up de risco do "Não reiniciar os navegadores": restaurar com o navegador aberto pode não funcionar
    /// (ele regrava os arquivos ao fechar). Só confirma depois de marcar o "por minha conta".
    /// </summary>
    public static async Task<bool> KeepBrowsersAsync(XamlRoot root)
    {
        var ack = new ToggleButton
        {
            Style = S("CheckCard"),
            Background = UI.Brush("#0a1120"),
            BorderBrush = UI.Brush("#2a3b63"),
            Content = new TextBlock
            {
                Text = L.T(
                    "Entendo os riscos e quero restaurar com os navegadores abertos, por minha conta."
                ),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
            },
        };
        var body = new StackPanel { Spacing = 16 };
        body.Children.Add(
            Para(
                L.T(
                    "Para restaurar os dados dos navegadores, o recomendado é que eles estejam fechados. Aberto, o navegador mantém os arquivos do perfil em uso e, ao fechar, grava por cima: favoritos e configurações restaurados podem se perder."
                )
            )
        );
        body.Children.Add(
            Para(
                L.T(
                    "Por padrão o WINbackup fecha os navegadores (inclusive os que ficam rodando em segundo plano) e abre de novo no fim. Com esta opção eles ficam como estão."
                ),
                muted: true
            )
        );
        body.Children.Add(ack);

        var d = Base(
            root,
            L.T("Fechar os navegadores é recomendado"),
            body,
            L.T("Não reiniciar os navegadores"),
            L.T("Manter recomendado"),
            warn: true
        );
        d.DefaultButton = ContentDialogButton.Close;
        d.IsPrimaryButtonEnabled = false;
        ack.Checked += (_, _) => d.IsPrimaryButtonEnabled = true;
        ack.Unchecked += (_, _) => d.IsPrimaryButtonEnabled = false;
        return await d.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>Fim do backup com navegadores: as senhas não entram sozinhas no backup. true = ir exportar agora.</summary>
    public static async Task<bool> PasswordReminderAsync(XamlRoot root, bool csvIncluded)
    {
        var body = new StackPanel { Spacing = 14 };
        body.Children.Add(
            Para(
                L.T(
                    "Por segurança, o WINbackup não copia as senhas salvas nos navegadores. Antes de formatar, garanta que elas não vão se perder:"
                )
            )
        );
        body.Children.Add(
            Para(
                L.T(
                    "• exporte as senhas de cada navegador (Navegadores → Senhas: exportação guiada) e adicione o .csv ao backup;\n• ou confira se a sincronização da sua conta (Microsoft, Google, Firefox…) está ligada."
                )
            )
        );
        if (csvIncluded)
            body.Children.Add(
                Para(
                    L.T(
                        "Este backup já levou os .csv de senhas que você adicionou. Se exportou há muito tempo, exporte de novo para pegar as senhas novas."
                    ),
                    muted: true
                )
            );
        var d = Base(
            root,
            L.T("Lembre-se das senhas"),
            body,
            L.T("Exportar senhas agora"),
            L.T("Já fiz"),
            warn: true
        );
        return await d.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>Fim do backup: abrir as listas do que reinstalar à mão (cada uma no seu botão; a janela continua aberta).</summary>
    public static async Task OpenListsAsync(
        XamlRoot root,
        IReadOnlyList<(string Label, string Hint, string Path)> lists
    )
    {
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(
            Para(
                L.T(
                    "O backup gerou estas listas para usar depois de formatar. Quer abrir alguma agora?"
                ),
                muted: true
            )
        );
        foreach (var (label, hint, path) in lists)
        {
            var g = new Grid
            {
                ColumnSpacing = 12,
                Padding = new Thickness(14, 10, 10, 10),
                CornerRadius = new CornerRadius(10),
                Background = UI.Brush("#0a1120"),
                BorderBrush = UI.Brush("#16223d"),
                BorderThickness = new Thickness(1),
            };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var t = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            t.Children.Add(
                new TextBlock
                {
                    Text = label,
                    FontSize = 13.5,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                }
            );
            t.Children.Add(
                new TextBlock
                {
                    Text = hint,
                    FontSize = 12,
                    Foreground = UI.Muted,
                    TextWrapping = TextWrapping.Wrap,
                }
            );
            g.Children.Add(t);
            var open = new Button
            {
                Style = S("SmallButton"),
                Content = L.T("Abrir"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
                open,
                L.T("Abrir") + " " + label
            );
            open.Click += (_, _) => Sys.Open(path);
            Grid.SetColumn(open, 1);
            g.Children.Add(open);
            body.Children.Add(g);
        }
        var d = Base(root, L.T("Abrir as listas do backup?"), body, L.T("Fechar"), null);
        await d.ShowAsync();
    }
}
