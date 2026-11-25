using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
namespace Empiro.App;
public partial class MainWindow : Window
{
    private readonly ShellViewModel viewModel;
    private bool mayClose;
    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent(); DataContext = this.viewModel = viewModel;
        Closed += (_, _) => viewModel.Dispose();
        Closing += async (_, e) =>
        {
            if (mayClose || (!viewModel.IsScanning && !viewModel.IsMonitoring)) return;
            e.Cancel = true; IsEnabled = false;
            await viewModel.StopAsync(); mayClose = true; Close();
        };
    }
    private void ZoomIn(object sender, RoutedEventArgs e) => NetworkMap.Zoom(1.15);
    private void ZoomOut(object sender, RoutedEventArgs e) => NetworkMap.Zoom(1 / 1.15);
    private void ResetMap(object sender, RoutedEventArgs e) => NetworkMap.ResetLayout();
    private void ExportMap(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { FileName = viewModel.IsDemo ? "Empiro-demo-map.png" : "Empiro-network-map.png", Filter = "PNG image|*.png" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var bitmap = new RenderTargetBitmap((int)NetworkMap.ActualWidth, (int)NetworkMap.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(NetworkMap);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(dialog.FileName); encoder.Save(file);
            viewModel.Status = "Network map exported.";
        }
        catch (Exception ex) { viewModel.Error(ex); }
    }
}
