using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FractalViewer.Mandelbrot
{
  public partial class MainWindow : Window
  {
    private const int W = 1000;
    private const int H = 750;

    private const double DefaultScale = 3.5 / W;   // complex units per pixel
    private const double DefaultCentreRe = -0.6;
    private const double DefaultCentreIm = 0.0;

    private const double MinScale = 1e-15;         // double precision gives out below this
    private const double MaxScale = 1.0;
    private const double ZoomPerNotch = 1.15;
    private const double ZoomPerClick = 4.0;

    private const float ColourStep = 0.02f;

    private readonly MandelbrotRenderer _renderer;
    private readonly WriteableBitmap _bitmap;

    private bool _rendering;
    private bool _renderPending;

    private bool _panning;
    private Point _panLast;

    private bool _showGrid = true;

    public MainWindow()
    {
      InitializeComponent();

      try
      {
        _renderer = new MandelbrotRenderer(W, H, DefaultScale, DefaultCentreRe, DefaultCentreIm);
      }
      catch (Exception ex)
      {
        System.Diagnostics.Debug.WriteLine(ex.ToString());
        throw;
      }

      _bitmap = new WriteableBitmap(W, H, 96, 96, PixelFormats.Bgra32, null);
      fractal.Source = _bitmap;

      surface.MouseWheel += OnMouseWheel;
      surface.MouseLeftButtonDown += OnMouseLeftButtonDown;
      surface.MouseMove += OnMouseMove;
      surface.MouseLeftButtonUp += OnMouseLeftButtonUp;
      surface.MouseRightButtonDown += OnMouseRightButtonDown;
      KeyDown += OnKeyDown;

      // Set here rather than in the markup: the Checked handler touches the renderer,
      // which does not exist until InitializeComponent has returned.
      autoIterations.IsChecked = true;
      UpdateIterationControls();

      Loaded += async (_, _) =>
      {
        RedrawOverlay();
        await RequestRenderAsync();
      };

      Closed += (_, _) => _renderer.Dispose();
    }

    #region render_scheduling

    // Dragging fires far faster than a full frame takes. Only one render runs at a
    // time; anything requested meanwhile collapses into a single follow-up pass.
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

    private void Invalidate()
    {
      RedrawOverlay();
      _ = RequestRenderAsync();
    }

    #endregion

    #region iteration_counter

    private bool AutoIterations => autoIterations.IsChecked == true;

    // Small steps where the picture changes fastest, coarser ones further up, so the
    // whole range stays a few clicks away.
    private static int StepFor(int value) => value < 50 ? 5
                                           : value < 200 ? 25
                                           : value < 1000 ? 100
                                           : 500;

    // Each decade of zoom pulls thinner filaments out of the black, and those need more
    // steps before they resolve. This tracks that roughly enough to keep exploring.
    private static int SuggestedIterations(double scale)
    {
      double depth = Math.Max(Math.Log10(DefaultScale / scale), 0);

      return (int)Math.Clamp(200 + 220 * depth,
                             MandelbrotRenderer.DefaultMaxIterations,
                             MandelbrotRenderer.IterationLimit);
    }

    private void SetIterations(int value)
    {
      value = Math.Clamp(value, 1, MandelbrotRenderer.IterationLimit);

      if (value == _renderer.MaxIterations)
        return;

      _renderer.SetMaxIterations(value);
      UpdateIterationControls();
      _ = RequestRenderAsync();
    }

    private void UpdateIterationControls()
    {
      int value = _renderer.MaxIterations;

      iterationsText.Text = value.ToString(CultureInfo.InvariantCulture);
      iterationsDown.IsEnabled = !AutoIterations && value > 1;
      iterationsUp.IsEnabled = !AutoIterations && value < MandelbrotRenderer.IterationLimit;
    }

    private void IterationsUp_Click(object sender, RoutedEventArgs e) =>
      SetIterations(_renderer.MaxIterations + StepFor(_renderer.MaxIterations));

    private void IterationsDown_Click(object sender, RoutedEventArgs e) =>
      SetIterations(_renderer.MaxIterations - StepFor(_renderer.MaxIterations - 1));

    private void AutoIterations_Changed(object sender, RoutedEventArgs e)
    {
      if (AutoIterations)
        SetIterations(SuggestedIterations(Scale));

      UpdateIterationControls();
    }

    #endregion

    #region view_transform

    private double Scale => _renderer.Scale;

    private double ReToX(double re) => (re - _renderer.CentreRe) / Scale + W * 0.5;
    private double ImToY(double im) => (im - _renderer.CentreIm) / Scale + H * 0.5;

    private double XToRe(double x) => _renderer.CentreRe + (x - W * 0.5) * Scale;
    private double YToIm(double y) => _renderer.CentreIm + (y - H * 0.5) * Scale;

    private void SetView(double centreRe, double centreIm, double scale)
    {
      scale = Math.Clamp(scale, MinScale, MaxScale);
      _renderer.SetView(centreRe, centreIm, scale);

      // Applied without rendering, so a zoom stays a single frame.
      if (AutoIterations)
      {
        _renderer.SetMaxIterations(SuggestedIterations(scale));
        UpdateIterationControls();
      }

      Invalidate();
    }

    // Anchors the zoom on a point: whatever complex value sits under it before the
    // zoom must still sit under it afterwards.
    private void ZoomAt(Point p, double factor)
    {
      double re = XToRe(p.X);
      double im = YToIm(p.Y);
      double scale = Math.Clamp(Scale * factor, MinScale, MaxScale);

      SetView(re - (p.X - W * 0.5) * scale,
              im - (p.Y - H * 0.5) * scale,
              scale);
    }

    #endregion

    #region input

    private void OnMouseWheel(object sender, MouseWheelEventArgs e) =>
      ZoomAt(e.GetPosition(surface), Math.Pow(ZoomPerNotch, -e.Delta / 120.0));

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      Point p = e.GetPosition(surface);

      // The first click of a double-click has already started a pan, so undo that.
      if (e.ClickCount == 2)
      {
        EndPan();
        ZoomAt(p, 1.0 / ZoomPerClick);
        return;
      }

      _panning = true;
      _panLast = p;
      surface.CaptureMouse();
      surface.Cursor = Cursors.ScrollAll;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
      if (!_panning)
        return;

      Point p = e.GetPosition(surface);
      Vector d = p - _panLast;
      _panLast = p;

      SetView(_renderer.CentreRe - d.X * Scale,
              _renderer.CentreIm - d.Y * Scale,
              Scale);
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndPan();

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e) =>
      ZoomAt(e.GetPosition(surface), ZoomPerClick);

    private void EndPan()
    {
      _panning = false;
      surface.ReleaseMouseCapture();
      surface.Cursor = Cursors.Arrow;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
      switch (e.Key)
      {
        case Key.R:
          SetView(DefaultCentreRe, DefaultCentreIm, DefaultScale);
          break;

        case Key.G:
          _showGrid = !_showGrid;
          RedrawOverlay();
          break;

        case Key.OemCloseBrackets:
          SetIterations(_renderer.MaxIterations + StepFor(_renderer.MaxIterations));
          break;

        case Key.OemOpenBrackets:
          SetIterations(_renderer.MaxIterations - StepFor(_renderer.MaxIterations - 1));
          break;

        case Key.OemPeriod:
          _renderer.ShiftColours(ColourStep);
          _ = RequestRenderAsync();
          break;

        case Key.OemComma:
          _renderer.ShiftColours(-ColourStep);
          _ = RequestRenderAsync();
          break;
      }
    }

    private void ResetView_Click(object sender, RoutedEventArgs e) =>
      SetView(DefaultCentreRe, DefaultCentreIm, DefaultScale);

    #endregion

    #region overlay

    private void RedrawOverlay()
    {
      overlay.Children.Clear();

      if (_showGrid)
        DrawGrid();

      UpdateViewText();
    }

    private void UpdateViewText()
    {
      viewText.Text = $"centre {FormatComplex(_renderer.CentreRe, _renderer.CentreIm)}\n" +
                      $"width  {W * Scale:G4}\n" +
                      $"zoom   {DefaultScale / Scale:G4}x";
    }

    private void DrawGrid()
    {
      Brush minor = MakeBrush(Color.FromArgb(45, 255, 255, 255));
      Brush axis = MakeBrush(Color.FromArgb(170, 255, 255, 255));

      double step = NiceStep(W * Scale / 8.0);

      // Deep in, the step is tiny and the labels would run to a dozen digits.
      int decimals = Math.Max(0, (int)Math.Ceiling(-Math.Log10(step)));
      string format = decimals <= 6 ? "0." + new string('#', decimals + 1) : "0.###E+0";

      double minRe = XToRe(0), maxRe = XToRe(W);
      double minIm = YToIm(0), maxIm = YToIm(H);

      // The multiplier is in the billions once the zoom is deep, so it has to be a long,
      // and the count is capped in case rounding ever leaves the loop unable to advance.
      long first = (long)Math.Ceiling(minRe / step);

      for (long k = first; k * step <= maxRe && k - first < 64; k++)
      {
        double re = k * step;
        double x = Math.Round(ReToX(re)) + 0.5;   // +0.5 keeps 1px lines sharp
        overlay.Children.Add(NewLine(x, 0, x, H, k == 0 ? axis : minor, k == 0 ? 1.5 : 1.0));

        if (k != 0)
          AddLabel(x + 3, Math.Clamp(ImToY(0), 0, H - 14) + 2, re, format);
      }

      first = (long)Math.Ceiling(minIm / step);

      for (long k = first; k * step <= maxIm && k - first < 64; k++)
      {
        double im = k * step;
        double y = Math.Round(ImToY(im)) + 0.5;
        overlay.Children.Add(NewLine(0, y, W, y, k == 0 ? axis : minor, k == 0 ? 1.5 : 1.0));

        if (k != 0)
          AddLabel(Math.Clamp(ReToX(0), 0, W - 60) + 3, y + 2, im, format, imaginary: true);
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

    private static string FormatComplex(double re, double im)
    {
      string sign = im < 0 ? "-" : "+";
      return $"{Fmt(re)} {sign} {Fmt(Math.Abs(im))}i";

      static string Fmt(double v) => v.ToString("0.############", CultureInfo.InvariantCulture);
    }

    #endregion

    #region shape helpers

    private static SolidColorBrush MakeBrush(Color color)
    {
      SolidColorBrush brush = new(color);
      brush.Freeze();
      return brush;
    }

    private static Line NewLine(double x1, double y1, double x2, double y2, Brush stroke, double thickness) =>
      new() { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = stroke, StrokeThickness = thickness };

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

    #endregion
  }
}