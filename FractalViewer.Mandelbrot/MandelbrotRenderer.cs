using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;

namespace FractalViewer.Mandelbrot;

/// <summary>Everything the kernel needs about the view, bundled to keep the signature short.</summary>
public readonly struct ViewParams(
  int width,
  int height,
  double scale,
  double centreRe,
  double centreIm,
  int maxIterations,
  int paletteEntries,
  float colourDensity,
  float colourOffset)
{
  public readonly int Width = width;
  public readonly int Height = height;
  public readonly double Scale = scale;
  public readonly double CentreRe = centreRe;
  public readonly double CentreIm = centreIm;
  public readonly int MaxIterations = maxIterations;
  public readonly int PaletteEntries = paletteEntries;
  public readonly float ColourDensity = colourDensity;
  public readonly float ColourOffset = colourOffset;
}

public class MandelbrotRenderer : IDisposable
{
  public const int DefaultMaxIterations = 200;
  public const int IterationLimit = 10_000;

  private const int PaletteEntries = 2048;

  // A generous escape radius. The smooth-colouring formula assumes the point has already
  // shot well past 2, and a tight bailout leaves visible steps between the bands.
  private const float BailoutSquared = 256f * 256f;
  private const float Log2 = 0.6931472f;

  private int Width { get; }
  private int Height { get; }

  private readonly Lock _sync = new();

  /// <summary>Complex units per pixel. Smaller means deeper zoom.</summary>
  public double Scale { get; private set; }

  /// <summary>Complex value at the centre pixel of the view.</summary>
  public double CentreRe { get; private set; }
  public double CentreIm { get; private set; }

  /// <summary>Squaring steps allowed per pixel before the point is called interior.</summary>
  public int MaxIterations { get; private set; } = DefaultMaxIterations;

  /// <summary>Palette cycles per unit of sqrt(escape time). Larger means tighter bands.</summary>
  public float ColourDensity { get; private set; } = 0.1f;

  /// <summary>Rotation through the palette, in cycles.</summary>
  public float ColourOffset { get; private set; }

  private Accelerator Accelerator { get; }
  private Context Context { get; }
  private MemoryBuffer1D<byte, Stride1D.Dense> RgbaBuffer { get; }
  private MemoryBuffer1D<byte, Stride1D.Dense> PaletteBuffer { get; }
  private byte[] Rgba { get; }
  private Action<Index1D, ArrayView<byte>, ArrayView<byte>, ViewParams> Kernel { get; }

  // The usual Ultra Fractal ramp: deep blue, through white, into orange and back to near
  // black. The last stop repeats the first so the gradient can be cycled seamlessly.
  private static readonly (float Position, byte R, byte G, byte B)[] Stops =
  [
    (0.0000f,   0,   7, 100),
    (0.1600f,  32, 107, 203),
    (0.4200f, 237, 255, 255),
    (0.6425f, 255, 170,   0),
    (0.8575f,   0,   2,   0),
    (1.0000f,   0,   7, 100),
  ];

  private int PixelCount => Width * Height;

  public MandelbrotRenderer(int width, int height, double scale, double centreRe, double centreIm)
  {
    Width = width;
    Height = height;
    Scale = scale;
    CentreRe = centreRe;
    CentreIm = centreIm;

    // EnableAlgorithms brings in XMath, which the kernel needs for logs and floors.
    Context = Context.Create(builder => builder.Default().EnableAlgorithms());
    Accelerator = Context.GetPreferredDevice(preferCPU: false).CreateAccelerator(Context);

    Rgba = new byte[PixelCount * 4];
    RgbaBuffer = Accelerator.Allocate1D<byte>(PixelCount * 4);

    byte[] palette = BuildPalette();
    PaletteBuffer = Accelerator.Allocate1D<byte>(palette.Length);
    PaletteBuffer.CopyFromCPU(palette);

    Kernel = Accelerator.LoadAutoGroupedStreamKernel
      <Index1D, ArrayView<byte>, ArrayView<byte>, ViewParams>(ComputeKernel);
  }

  #region view

  public void SetView(double centreRe, double centreIm, double scale)
  {
    lock (_sync)
    {
      CentreRe = centreRe;
      CentreIm = centreIm;
      Scale = scale;
    }
  }

  /// <summary>Sets the iteration budget, clamped to 1..<see cref="IterationLimit"/>.</summary>
  public void SetMaxIterations(int value)
  {
    lock (_sync)
      MaxIterations = Math.Clamp(value, 1, IterationLimit);
  }

  /// <summary>Rotates the palette. Only the fractional part matters, so it never drifts.</summary>
  public void ShiftColours(float cycles)
  {
    lock (_sync)
      ColourOffset = (ColourOffset + cycles) % 1f;
  }

  #endregion

  #region rendering

  public byte[] Render()
  {
    ViewParams view;

    // Snapshot under the lock: the UI thread can change the view mid-render.
    lock (_sync)
      view = new ViewParams(Width, Height, Scale, CentreRe, CentreIm,
                            MaxIterations, PaletteEntries, ColourDensity, ColourOffset);

    // Every GPU call stays on this thread.
    Kernel(PixelCount, RgbaBuffer.View, PaletteBuffer.View, view);
    RgbaBuffer.CopyToCPU(Rgba);
    return Rgba;
  }

  private static void ComputeKernel(Index1D i, ArrayView<byte> buffer, ArrayView<byte> palette, ViewParams view)
  {
    int xi = i % view.Width;
    int yi = i / view.Width;

    double cr = view.CentreRe + (xi - view.Width * 0.5) * view.Scale;
    double ci = view.CentreIm + (yi - view.Height * 0.5) * view.Scale;

    // The cardioid and the period-2 bulb are the two big interior lobes, and membership in
    // them is a closed form. Testing for that up front saves the full iteration budget over
    // most of the black area, which is where all the time would otherwise go.
    double q = (cr - 0.25) * (cr - 0.25) + ci * ci;
    bool interior = q * (q + cr - 0.25) <= 0.25 * ci * ci
                 || (cr + 1.0) * (cr + 1.0) + ci * ci <= 0.0625;

    double zr = 0.0, zi = 0.0, zr2 = 0.0, zi2 = 0.0;
    int n = 0;

    if (!interior)
    {
      // Carrying the squares forward means one multiply fewer per step than the naive form.
      while (n < view.MaxIterations && zr2 + zi2 <= BailoutSquared)
      {
        zi = 2.0 * zr * zi + ci;
        zr = zr2 - zi2 + cr;
        zr2 = zr * zr;
        zi2 = zi * zi;
        n++;
      }
    }

    byte r = 0, g = 0, b = 0;

    if (!interior && n < view.MaxIterations)
    {
      // Integer escape counts would band the picture into visible steps. Subtracting
      // log2(log|z|) recovers the fraction of a step the point overshot by.
      float magnitude = (float)(zr2 + zi2);
      float logZ = XMath.Log(magnitude) * 0.5f;
      float escape = XMath.Max(n + 1f - XMath.Log(logZ) / Log2, 0f);

      // Escape times crowd together near the boundary and spread out away from it. Taking
      // the square root evens that out, so the bands stay legible at any depth.
      float u = XMath.Sqrt(escape) * view.ColourDensity + view.ColourOffset;
      u -= XMath.Floor(u);

      float f = u * view.PaletteEntries;
      int p0 = XMath.Min((int)f, view.PaletteEntries - 1);
      int p1 = p0 + 1 == view.PaletteEntries ? 0 : p0 + 1;
      float t = f - p0;

      r = Mix(palette[p0 * 3 + 0], palette[p1 * 3 + 0], t);
      g = Mix(palette[p0 * 3 + 1], palette[p1 * 3 + 1], t);
      b = Mix(palette[p0 * 3 + 2], palette[p1 * 3 + 2], t);
    }

    int o = i * 4;
    buffer[o + 0] = b;   // Bgra32
    buffer[o + 1] = g;
    buffer[o + 2] = r;
    buffer[o + 3] = 255;
  }

  private static byte Mix(byte a, byte b, float t) => (byte)(a + (b - a) * t);

  private static byte[] BuildPalette()
  {
    byte[] data = new byte[PaletteEntries * 3];
    int stop = 0;

    for (int i = 0; i < PaletteEntries; i++)
    {
      float t = (float)i / PaletteEntries;

      while (stop < Stops.Length - 2 && t > Stops[stop + 1].Position)
        stop++;

      (float Position, byte R, byte G, byte B) a = Stops[stop];
      (float Position, byte R, byte G, byte B) b = Stops[stop + 1];
      float f = (t - a.Position) / (b.Position - a.Position);

      data[i * 3 + 0] = Mix(a.R, b.R, f);
      data[i * 3 + 1] = Mix(a.G, b.G, f);
      data[i * 3 + 2] = Mix(a.B, b.B, f);
    }

    return data;
  }

  public void Dispose()
  {
    RgbaBuffer.Dispose();
    PaletteBuffer.Dispose();
    Accelerator.Dispose();
    Context.Dispose();
    GC.SuppressFinalize(this);
  }

  #endregion
}