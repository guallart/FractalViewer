using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FractalViewer.NewtonRaphson
{
  public partial class MainWindow : Window
  {
    private const int W = 800;
    private const int H = 600;

    private readonly FractalRenderer _renderer;
    private readonly WriteableBitmap _bitmap;

    public MainWindow()
    {
      InitializeComponent();

      try
      {
        _renderer = new FractalRenderer(W, H, 4f / W);
      }
      catch (Exception ex)
      {
        System.Diagnostics.Debug.WriteLine(ex.ToString());
        throw;
      }

      _bitmap = new WriteableBitmap(W, H, 96, 96, PixelFormats.Bgra32, null);
      fractal.Source = _bitmap;

      Loaded += async (_, _) => await RenderAsync();
      Closed += (_, _) => _renderer.Dispose();
    }

    private async Task RenderAsync()
    {
      byte[] pixels = await Task.Run(() => _renderer.Render());
      _bitmap.WritePixels(new Int32Rect(0, 0, W, H), pixels, W * 4, 0);
    }
  }
}