using System.Windows;
using System.Windows.Input;
using UltraArchive.App.Resources;
using Wpf.Ui.Controls;

namespace UltraArchive.App.Views;

/// <summary>
/// Diálogo modal para pedir al usuario la contraseña de un archivo protegido. La contraseña se lee
/// de un <see cref="System.Windows.Controls.PasswordBox"/> (nunca de un TextBox: WPF evita mantenerla
/// en el árbol visual como texto plano) y solo vive en memoria mientras dura esta ventana y la
/// operación que la solicitó; ver <see cref="Services.WpfPasswordProvider"/>.
/// </summary>
public partial class PasswordDialog : FluentWindow
{
    public PasswordDialog(string archiveDisplayName, bool isRetryAfterFailure)
    {
        InitializeComponent();

        PromptText.Text = isRetryAfterFailure
            ? Strings.Format(Strings.PasswordDialogPromptRetry, archiveDisplayName)
            : Strings.Format(Strings.PasswordDialogPrompt, archiveDisplayName);

        Loaded += (_, _) => PasswordBox.Focus();
    }

    /// <summary>Contraseña introducida, o null si el usuario canceló el diálogo.</summary>
    public string? EnteredPassword { get; private set; }

    private void Ok_Click(object sender, RoutedEventArgs e) => Accept();

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Accept();
        }
    }

    private void Accept()
    {
        EnteredPassword = PasswordBox.Password;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        EnteredPassword = null;
        DialogResult = false;
        Close();
    }
}
