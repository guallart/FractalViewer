using FractalViewer.Core;

using ILGPU;
using ILGPU.Runtime;

namespace FractalViewer.NewtonRaphson;

public readonly record struct RootInfo(int Index, Complex Root, byte R, byte G, byte B);

public class FractalRenderer : IDisposable
{
  private int Width { get; }
  private int Height { get; }

  private readonly Lock _sync = new();
  private readonly Complex[] _roots;
  private Poly _poly;

  /// <summary>Complex units per pixel. Smaller means deeper zoom.</summary>
  public float Scale { get; private set; }

  /// <summary>Complex value at the centre pixel of the view.</summary>
  public float CentreRe { get; private set; }
  public float CentreIm { get; private set; }

  public IReadOnlyList<RootInfo> Roots { get; private set; }

  private Accelerator Accelerator { get; }
  private Context Context { get; }
  private MemoryBuffer1D<byte, Stride1D.Dense> RgbaBuffer { get; }
  private MemoryBuffer1D<byte, Stride1D.Dense> PaletteBuffer { get; }
  private byte[] Rgba { get; }
  private Action<Index1D, ArrayView<byte>, ArrayView<byte>, Poly, int, int, float, float, float> Kernel { get; }

  private static readonly byte[] Palette = [
     31, 119, 180,
    255, 127,  14,
     44, 160,  44,
    214,  39,  40,
    148, 103, 189,
    140,  86,  75,
    227, 119, 194,
    127, 127, 127,
  ];

  private static int PaletteSize => Palette.Length / 3;

  private int PixelCount => Width * Height;

  public FractalRenderer(int width, int height, float scale, Complex[] roots)
  {
    ArgumentNullException.ThrowIfNull(roots);

    if (roots.Length > PaletteSize)
      throw new ArgumentException($"Palette holds only {PaletteSize} colours.", nameof(roots));

    Width = width;
    Height = height;
    Scale = scale;

    Context = Context.CreateDefault();
    Accelerator = Context.GetPreferredDevice(preferCPU: false).CreateAccelerator(Context);

    _roots = [.. roots];
    _poly = new Poly(_roots);
    Roots = BuildRootInfo(_roots);

    Rgba = new byte[PixelCount * 4];
    RgbaBuffer = Accelerator.Allocate1D<byte>(PixelCount * 4);
    PaletteBuffer = Accelerator.Allocate1D(Palette);

    Kernel = Accelerator.LoadAutoGroupedStreamKernel
      <Index1D, ArrayView<byte>, ArrayView<byte>, Poly, int, int, float, float, float>(ComputeKernel);
  }

  /// <summary>Moves a single root and rebuilds the polynomial around it.</summary>
  public void SetRoot(int index, Complex value)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(index);
    ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _roots.Length);

    lock (_sync)
    {
      _roots[index] = value;
      _poly = new Poly(_roots);
      Roots = BuildRootInfo(_roots);
    }
  }

  /// <summary>Replaces the whole root set. Degree may change; colours follow the palette order.</summary>
  public void SetRoots(Complex[] roots)
  {
    ArgumentNullException.ThrowIfNull(roots);

    if (roots.Length is 0 or > 8 || roots.Length > PaletteSize)
      throw new ArgumentException($"Need 1 to {Math.Min(8, PaletteSize)} roots.", nameof(roots));

    lock (_sync)
    {
      Poly poly = new(roots);          // validates before anything is committed
      Complex[] copy = [.. roots];

      _poly = poly;
      Roots = BuildRootInfo(copy);

      if (copy.Length == _roots.Length)
        copy.CopyTo(_roots, 0);
    }
  }

  public void SetView(float centreRe, float centreIm, float scale)
  {
    lock (_sync)
    {
      CentreRe = centreRe;
      CentreIm = centreIm;
      Scale = scale;
    }
  }

  private static RootInfo[] BuildRootInfo(Complex[] roots) =>
    [.. roots.Select((r, i) => new RootInfo(i, r, Palette[i * 3], Palette[i * 3 + 1], Palette[i * 3 + 2]))];

  public byte[] Render()
  {
    Poly poly;
    float scale, centreRe, centreIm;

    // Snapshot under the lock: the UI thread can move a root mid-render, and Poly is
    // a large struct that would otherwise be copied while it is being rewritten.
    lock (_sync)
    {
      poly = _poly;
      scale = Scale;
      centreRe = CentreRe;
      centreIm = CentreIm;
    }

    Kernel(PixelCount, RgbaBuffer.View, PaletteBuffer.View, poly, Width, Height, scale, centreRe, centreIm);
    RgbaBuffer.CopyToCPU(Rgba);
    return Rgba;
  }

  private static void ComputeKernel(Index1D i, ArrayView<byte> buffer, ArrayView<byte> palette, Poly poly,
                                    int width, int height, float scale, float centreRe, float centreIm)
  {
    int xi = i % width;
    int yi = i / width;

    float x = centreRe + (xi - width * 0.5f) * scale;
    float y = centreIm + (yi - height * 0.5f) * scale;

    int root = poly.FindRoot(new Complex(x, y));

    byte r = 0, g = 0, b = 0;
    if (root >= 0)
    {
      int p = root * 3;
      r = palette[p + 0];
      g = palette[p + 1];
      b = palette[p + 2];
    }

    int o = i * 4;
    buffer[o + 0] = b;   // Bgra32
    buffer[o + 1] = g;
    buffer[o + 2] = r;
    buffer[o + 3] = 255;
  }

  public void Dispose()
  {
    RgbaBuffer.Dispose();
    PaletteBuffer.Dispose();
    Accelerator.Dispose();
    Context.Dispose();
    GC.SuppressFinalize(this);
  }
}