using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HubLink.Services;
using HubLink.ViewModels;
using HubLink.Views;

namespace HubLink;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var api = new ShipmentApiService();
            var exporter = new CsvExportService();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new HubLinkViewModel(api, exporter)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}