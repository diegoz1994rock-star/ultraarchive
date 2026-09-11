using System.Reflection;
using System.Windows;
using UltraArchive.App.Resources;
using Wpf.Ui.Controls;

namespace UltraArchive.App.Views;

/// <summary>
/// Diálogo "Acerca de": logo, versión y marca ASTRIM. Sin ViewModel — es contenido estático,
/// no hay estado que testear ni que sobreviva más allá de estar abierto.
/// </summary>
public partial class AboutDialog : FluentWindow
{
    public AboutDialog()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var display = version is null ? "?" : $"{version.Major}.{version.Minor}.{version.Build}";
        VersionText.Text = Strings.Format(Strings.AboutVersion, display);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
