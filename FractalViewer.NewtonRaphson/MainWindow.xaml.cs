using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FractalViewer.NewtonRaphson
{
  public partial class MainWindow : Window
  {
    private const int W = 800;
    private const int H = 600;

    private readonly FractalRenderer _renderer;
    private readonly WriteableBitmap _bitmap;

    private const float S = 4f / W; // complex units per pixel

    private static double ReToX(double re) => re / S + W * 0.5;
    private static double ImToY(double im) => H * 0.5 - im / S;

    public MainWindow()
    {
      InitializeComponent();

      _renderer = new FractalRenderer(W, H, 4f / W);
      _bitmap = new WriteableBitmap(W, H, 96, 96, PixelFormats.Bgra32, null);
      fractal.Source = _bitmap;

      Loaded += async (_, _) => { DrawGrid(); await RenderAsync(); };
      Closed += (_, _) => _renderer.Dispose();
    }

    private async Task RenderAsync()
    {
      byte[] pixels = await Task.Run(() => _renderer.Render());
      _bitmap.WritePixels(new Int32Rect(0, 0, W, H), pixels, W * 4, 0);
    }

    private void DrawGrid(double step = 0.5)
    {
      overlay.Children.Clear();

      var minor = MakeBrush(45);
      var axis = MakeBrush(170);

      int nx = (int)(W * 0.5 * S / step);
      int ny = (int)(H * 0.5 * S / step);

      for (int k = -nx; k <= nx; k++)
      {
        double re = k * step;
        double x = Math.Round(ReToX(re)) + 0.5;   // +0.5 keeps 1px lines sharp
        overlay.Children.Add(NewLine(x, 0, x, H, k == 0 ? axis : minor, k == 0 ? 1.5 : 1.0));
        if (k != 0) AddLabel(x + 3, ImToY(0) + 2, re);
      }

      for (int k = -ny; k <= ny; k++)
      {
        double im = k * step;
        double y = Math.Round(ImToY(im)) + 0.5;
        overlay.Children.Add(NewLine(0, y, W, y, k == 0 ? axis : minor, k == 0 ? 1.5 : 1.0));
        if (k != 0) AddLabel(ReToX(0) + 3, y + 2, im, imaginary: true);
      }
    }

    private static SolidColorBrush MakeBrush(byte alpha)
    {
      var b = new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255));
      b.Freeze();
      return b;
    }

    private static Line NewLine(double x1, double y1, double x2, double y2, Brush brush, double thickness) =>
        new() { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, StrokeThickness = thickness };

    private void AddLabel(double x, double y, double value, bool imaginary = false)
    {
      string text = value.ToString("0.##", CultureInfo.InvariantCulture) + (imaginary ? "i" : "");
      var tb = new TextBlock
      {
        Text = text,
        Foreground = MakeBrush(200),
        FontSize = 10,
        FontFamily = new FontFamily("Consolas")
      };
      Canvas.SetLeft(tb, x);
      Canvas.SetTop(tb, y);
      overlay.Children.Add(tb);
    }
  }
}