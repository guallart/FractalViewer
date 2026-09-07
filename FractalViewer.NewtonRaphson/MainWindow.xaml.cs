using FractalViewer.Core;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FractalViewer.NewtonRaphson
{
  public partial class MainWindow : Window
  {
    private const int W = 1000;
    private const int H = 750;

    private const float DefaultScale = 4f / W;   // complex units per pixel
    private const float MinScale = 1e-6f;        // float precision gives out below this
    private const float MaxScale = 1f;
    private const double ZoomPerNotch = 1.15;

    private static readonly Complex[] Roots = [Complex.One, Complex.OneI, -Complex.OneI];

    private readonly FractalRenderer _renderer;
    private readonly WriteableBitmap _bitmap;

    private bool _rendering;
    private bool _renderPending;

    private bool _dragging;
    private Point _dragLast;

    public MainWindow()
    {
      InitializeComponent();

      try
      {
        _renderer = new FractalRenderer(W, H, DefaultScale, Roots);
      }
      catch (Exception ex)
      {
        System.Diagnostics.Debug.WriteLine(ex.ToString());
        throw;
      }

      _bitmap = new WriteableBitmap(W, H, 96, 96, PixelFormats.Bgra32, null);
      fractal.Source = _bitmap;

      BuildRootList();

      surface.MouseWheel += OnMouseWheel;
      surface.MouseLeftButtonDown += OnMouseLeftButtonDown;
      surface.MouseMove += OnMouseMove;
      surface.MouseLeftButtonUp += OnMouseLeftButtonUp;
      KeyDown += OnKeyDown;

      Loaded += async (_, _) =>
      {
        RedrawOverlay();
        await RequestRenderAsync();
      };

      Closed += (_, _) => _renderer.Dispose();
    }

    // Pan and zoom fire far faster than a full frame takes. Only one render runs at
    // a time; anything requested meanwhile collapses into a single follow-up pass.
    private async Task RequestRenderAsync()
    {
      if (_rendering)
      {
        _renderPending = true;
        return;
      }

      _rendering = true;

      try
      {
        do
        {
          _renderPending = false;
          byte[] pixels = await Task.Run(_renderer.Render);
          _bitmap.WritePixels(new Int32Rect(0, 0, W, H), pixels, W * 4, 0);
        }
        while (_renderPending);
      }
      finally
      {
        _rendering = false;
      }
    }

    private double Scale => _renderer.Scale;

    private double ReToX(double re) => (re - _renderer.CentreRe) / Scale + W * 0.5;
    private double ImToY(double im) => (im - _renderer.CentreIm) / Scale + H * 0.5;

    private double XToRe(double x) => _renderer.CentreRe + (x - W * 0.5) * Scale;
    private double YToIm(double y) => _renderer.CentreIm + (y - H * 0.5) * Scale;

    private void SetView(double centreRe, double centreIm, double scale)
    {
      _renderer.Scale = (float)Math.Clamp(scale, MinScale, MaxScale);
      _renderer.CentreRe = (float)centreRe;
      _renderer.CentreIm = (float)centreIm;

      RedrawOverlay();
      _ = RequestRenderAsync();
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
      Point p = e.GetPosition(surface);

      // Anchor the zoom on the point under the cursor: whatever complex value sits
      // there before the zoom must still sit there afterwards.
      double re = XToRe(p.X);
      double im = YToIm(p.Y);

      double scale = Math.Clamp(Scale * Math.Pow(ZoomPerNotch, -e.Delta / 120.0), MinScale, MaxScale);

      SetView(re - (p.X - W * 0.5) * scale,
              im - (p.Y - H * 0.5) * scale,
              scale);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      _dragging = true;
      _dragLast = e.GetPosition(surface);
      surface.CaptureMouse();
      surface.Cursor = Cursors.ScrollAll;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
      if (!_dragging)
        return;

      Point p = e.GetPosition(surface);
      Vector d = p - _dragLast;
      _dragLast = p;

      SetView(_renderer.CentreRe - d.X * Scale,
              _renderer.CentreIm - d.Y * Scale,
              Scale);
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
      _dragging = false;
      surface.ReleaseMouseCapture();
      surface.Cursor = Cursors.Arrow;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.R)
        SetView(0, 0, DefaultScale);
    }

    private void RedrawOverlay()
    {
      overlay.Children.Clear();
      DrawGrid();
      DrawRootMarkers();
      UpdateViewText();
    }

    private void UpdateViewText()
    {
      viewText.Text = $"centre {FormatComplex(_renderer.CentreRe, _renderer.CentreIm)}\n" +
                      $"width  {W * Scale:G4}";
    }

    private void DrawGrid()
    {
      Brush minor = MakeBrush(Color.FromArgb(45, 255, 255, 255));
      Brush axis = MakeBrush(Color.FromArgb(170, 255, 255, 255));

      double step = NiceStep(W * Scale / 8.0);
      int decimals = Math.Max(0, (int)Math.Ceiling(-Math.Log10(step)));
      string format = "0." + new string('#', decimals + 1);

      double minRe = XToRe(0), maxRe = XToRe(W);
      double minIm = YToIm(0), maxIm = YToIm(H);

      for (int k = (int)Math.Ceiling(minRe / step); k * step <= maxRe; k++)
      {
        double re = k * step;
        double x = Math.Round(ReToX(re)) + 0.5;   // +0.5 keeps 1px lines sharp
        overlay.Children.Add(NewLine(x, 0, x, H, k == 0 ? axis : minor, k == 0 ? 1.5 : 1.0));

        if (k != 0)
          AddLabel(x + 3, Math.Clamp(ImToY(0), 0, H - 14) + 2, re, format);
      }

      for (int k = (int)Math.Ceiling(minIm / step); k * step <= maxIm; k++)
      {
        double im = k * step;
        double y = Math.Round(ImToY(im)) + 0.5;
        overlay.Children.Add(NewLine(0, y, W, y, k == 0 ? axis : minor, k == 0 ? 1.5 : 1.0));

        if (k != 0)
          AddLabel(Math.Clamp(ReToX(0), 0, W - 40) + 3, y + 2, im, format, imaginary: true);
      }
    }

    /// <summary>Rounds a raw spacing up to the nearest 1, 2 or 5 times a power of ten.</summary>
    private static double NiceStep(double raw)
    {
      double exponent = Math.Floor(Math.Log10(raw));
      double magnitude = Math.Pow(10, exponent);
      double fraction = raw / magnitude;

      double nice = fraction < 1.5 ? 1
                  : fraction < 3.0 ? 2
                  : fraction < 7.0 ? 5
                  : 10;

      return nice * magnitude;
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

        // Skip markers well outside the view so deep zooms stay cheap.
        if (cx < -50 || cx > W + 50 || cy < -50 || cy > H + 50)
          continue;

        // Dark halo underneath so the marker reads against its own basin colour.
        overlay.Children.Add(NewCircle(cx, cy, MarkerRadius + 1.5, null, halo, 3.0));
        overlay.Children.Add(NewCircle(cx, cy, MarkerRadius, MakeBrush(Color.FromRgb(r.R, r.G, r.B)), ring, 2.0));
      }
    }

    private void BuildRootList()
    {
      rootList.ItemsSource = _renderer.Roots
        .Select(r => new RootView
        {
          Value = FormatComplex(r.Root.Real, r.Root.Imaginary),
          Label = $"root {r.Index}",
          Swatch = MakeBrush(Color.FromRgb(r.R, r.G, r.B)),
        })
        .ToList();
    }

    private static string FormatComplex(double re, double im)
    {
      string sign = im < 0 ? "-" : "+";
      return $"{Fmt(re)} {sign} {Fmt(Math.Abs(im))}i";

      static string Fmt(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private sealed class RootView
    {
      public required string Value { get; init; }
      public required string Label { get; init; }
      public required Brush Swatch { get; init; }
    }

    private static SolidColorBrush MakeBrush(Color color)
    {
      SolidColorBrush brush = new(color);
      brush.Freeze();
      return brush;
    }

    private static Line NewLine(double x1, double y1, double x2, double y2, Brush stroke, double thickness) =>
      new() { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = stroke, StrokeThickness = thickness };

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

    private void AddLabel(double x, double y, double value, string format, bool imaginary = false)
    {
      TextBlock tb = new()
      {
        Text = value.ToString(format, CultureInfo.InvariantCulture) + (imaginary ? "i" : ""),
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