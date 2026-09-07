using FractalViewer.Core;

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
    private const int W = 1000;
    private const int H = 750;
    private const float S = 4f / W;      // complex units per pixel

    private static readonly Complex[] Roots = [Complex.One, Complex.OneI, -Complex.OneI];

    private readonly FractalRenderer _renderer;
    private readonly WriteableBitmap _bitmap;

    public MainWindow()
    {
      InitializeComponent();

      try
      {
        _renderer = new FractalRenderer(W, H, S, Roots);
      }
      catch (Exception ex)
      {
        System.Diagnostics.Debug.WriteLine(ex.ToString());
        throw;
      }

      _bitmap = new WriteableBitmap(W, H, 96, 96, PixelFormats.Bgra32, null);
      fractal.Source = _bitmap;

      BuildRootList();

      Loaded += async (_, _) =>
      {
        DrawGrid();
        DrawRootMarkers();
        await RenderAsync();
      };

      Closed += (_, _) => _renderer.Dispose();
    }

    private async Task RenderAsync()
    {
      byte[] pixels = await Task.Run(() => _renderer.Render());
      _bitmap.WritePixels(new Int32Rect(0, 0, W, H), pixels, W * 4, 0);
    }

    // ---------- roots panel ----------

    private void BuildRootList()
    {
      rootList.ItemsSource = _renderer.Roots
        .Select(r => new RootView
        {
          Value = FormatComplex(r.Root),
          Label = $"root {r.Index}",
          Swatch = MakeBrush(Color.FromRgb(r.R, r.G, r.B)),
        })
        .ToList();
    }

    private static string FormatComplex(Complex z)
    {
      double re = z.Real;
      double im = z.Imaginary;

      string sign = im < 0 ? "-" : "+";
      return $"{Fmt(re)} {sign} {Fmt(Math.Abs(im))}i";

      static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private sealed class RootView
    {
      public required string Value { get; init; }
      public required string Label { get; init; }
      public required Brush Swatch { get; init; }
    }


    private static double ReToX(double re) => re / S + W * 0.5;
    private static double ImToY(double im) => im / S + H * 0.5;

    private void DrawGrid(double step = 0.5)
    {
      overlay.Children.Clear();

      Brush minor = MakeBrush(Color.FromArgb(45, 255, 255, 255));
      Brush axis = MakeBrush(Color.FromArgb(170, 255, 255, 255));

      int nx = (int)(W * 0.5 * S / step);
      int ny = (int)(H * 0.5 * S / step);

      for (int k = -nx; k <= nx; k++)
      {
        double x = Math.Round(ReToX(k * step)) + 0.5;   // +0.5 keeps 1px lines sharp
        overlay.Children.Add(NewLine(x, 0, x, H, k == 0 ? axis : minor, k == 0 ? 1.5 : 1.0));

        if (k != 0)
          AddLabel(x + 3, ImToY(0) + 2, k * step);
      }

      for (int k = -ny; k <= ny; k++)
      {
        double y = Math.Round(ImToY(k * step)) + 0.5;
        overlay.Children.Add(NewLine(0, y, W, y, k == 0 ? axis : minor, k == 0 ? 1.5 : 1.0));

        if (k != 0)
          AddLabel(ReToX(0) + 3, y + 2, k * step, imaginary: true);
      }
    }

    private const double MarkerRadius = 6.0;

    private void DrawRootMarkers()
    {
      Brush halo = MakeBrush(Color.FromArgb(210, 18, 18, 22));
      Brush ring = MakeBrush(Color.FromArgb(235, 255, 255, 255));

      foreach (RootInfo r in _renderer.Roots)
      {
        double cx = ReToX(r.Root.Real);
        double cy = ImToY(r.Root.Imaginary);

        // Dark halo underneath so the marker reads against its own basin colour.
        overlay.Children.Add(NewCircle(cx, cy, MarkerRadius + 1.5, null, halo, 3.0));
        overlay.Children.Add(NewCircle(cx, cy, MarkerRadius, MakeBrush(Color.FromRgb(r.R, r.G, r.B)), ring, 2.0));
      }
    }

    private static Ellipse NewCircle(double cx, double cy, double radius, Brush? fill, Brush stroke, double thickness)
    {
      Ellipse e = new()
      {
        Width = radius * 2,
        Height = radius * 2,
        Fill = fill,
        Stroke = stroke,
        StrokeThickness = thickness,
      };

      Canvas.SetLeft(e, cx - radius);
      Canvas.SetTop(e, cy - radius);
      return e;
    }

    private static SolidColorBrush MakeBrush(Color color)
    {
      SolidColorBrush brush = new(color);
      brush.Freeze();
      return brush;
    }

    private static Line NewLine(double x1, double y1, double x2, double y2, Brush stroke, double thickness) =>
      new() { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = stroke, StrokeThickness = thickness };

    private void AddLabel(double x, double y, double value, bool imaginary = false)
    {
      TextBlock tb = new()
      {
        Text = value.ToString("0.##", CultureInfo.InvariantCulture) + (imaginary ? "i" : ""),
        Foreground = MakeBrush(Color.FromArgb(200, 255, 255, 255)),
        FontFamily = new FontFamily("Consolas"),
        FontSize = 10,
      };

      Canvas.SetLeft(tb, x);
      Canvas.SetTop(tb, y);
      overlay.Children.Add(tb);
    }
  }
}