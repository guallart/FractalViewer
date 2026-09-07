using FractalViewer.Core;

using ILGPU;
using ILGPU.Runtime;

namespace FractalViewer.NewtonRaphson;

public readonly record struct RootInfo(int Index, Complex Root, byte R, byte G, byte B);

/// <summary>Everything the kernel needs about the view, bundled to keep the signature short.</summary>
public readonly struct ViewParams(int width, int height, float scale, float centreRe, float centreIm, int maxIterations)
{
  public readonly int Width = width;
  public readonly int Height = height;
  public readonly float Scale = scale;
  public readonly float CentreRe = centreRe;
  public readonly float CentreIm = centreIm;
  public readonly int MaxIterations = maxIterations;
}

public class FractalRenderer : IDisposable
{
  public const int DefaultMaxIterations = 100;
  public const int IterationLimit = 1000;

  private int Width { get; }
  private int Height { get; }

  private readonly Lock _sync = new();
  private readonly List<Complex> _roots = [];
  private readonly List<int> _slots = [];        // palette slot held by each root
  private readonly byte[] _colours;              // colours ordered by root position, uploaded to the GPU
  private bool _coloursDirty = true;
  private Poly _poly;

  /// <summary>Complex units per pixel. Smaller means deeper zoom.</summary>
  public float Scale { get; private set; }

  /// <summary>Complex value at the centre pixel of the view.</summary>
  public float CentreRe { get; private set; }
  public float CentreIm { get; private set; }

  /// <summary>Newton steps allowed per pixel before the nearest root is picked.</summary>
  public int MaxIterations { get; private set; } = DefaultMaxIterations;

  public IReadOnlyList<RootInfo> Roots { get; private set; }

  private Accelerator Accelerator { get; }
  private Context Context { get; }
  private MemoryBuffer1D<byte, Stride1D.Dense> RgbaBuffer { get; }
  private MemoryBuffer1D<byte, Stride1D.Dense> PaletteBuffer { get; }
  private byte[] Rgba { get; }
  private Action<Index1D, ArrayView<byte>, ArrayView<byte>, Poly, ViewParams> Kernel { get; }

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

  /// <summary>Upper bound on roots: whichever runs out first, the palette or the polynomial.</summary>
  public static int MaxRoots => Math.Min(Poly.MaxDegree, PaletteSize);

  private int PixelCount => Width * Height;

  public FractalRenderer(int width, int height, float scale, Complex[] roots)
  {
    ArgumentNullException.ThrowIfNull(roots);

    if (roots.Length is 0 || roots.Length > MaxRoots)
      throw new ArgumentException($"Need 1 to {MaxRoots} roots.", nameof(roots));

    Width = width;
    Height = height;
    Scale = scale;

    Context = Context.CreateDefault();
    Accelerator = Context.GetPreferredDevice(preferCPU: false).CreateAccelerator(Context);

    _colours = new byte[PaletteSize * 3];
    _roots.AddRange(roots);
    _slots.AddRange(Enumerable.Range(0, roots.Length));
    Roots = [];
    Rebuild();

    Rgba = new byte[PixelCount * 4];
    RgbaBuffer = Accelerator.Allocate1D<byte>(PixelCount * 4);
    PaletteBuffer = Accelerator.Allocate1D<byte>(_colours.Length);

    Kernel = Accelerator.LoadAutoGroupedStreamKernel
      <Index1D, ArrayView<byte>, ArrayView<byte>, Poly, ViewParams>(ComputeKernel);
  }

  // ---------- roots ----------

  /// <summary>Moves a single root and rebuilds the polynomial around it.</summary>
  public void SetRoot(int index, Complex value)
  {
    lock (_sync)
    {
      ArgumentOutOfRangeException.ThrowIfNegative(index);
      ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _roots.Count);

      _roots[index] = value;
      Rebuild();
    }
  }

  /// <summary>Appends a root, taking the lowest unused palette colour. Returns its index.</summary>
  public int AddRoot(Complex value)
  {
    lock (_sync)
    {
      if (_roots.Count >= MaxRoots)
        throw new InvalidOperationException($"Already at {MaxRoots} roots.");

      _roots.Add(value);
      _slots.Add(FreeSlot());
      Rebuild();

      return _roots.Count - 1;
    }
  }

  /// <summary>Drops a root, releasing its colour. The last root cannot be removed.</summary>
  public void RemoveRoot(int index)
  {
    lock (_sync)
    {
      ArgumentOutOfRangeException.ThrowIfNegative(index);
      ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _roots.Count);

      if (_roots.Count == 1)
        throw new InvalidOperationException("A polynomial needs at least one root.");

      _roots.RemoveAt(index);
      _slots.RemoveAt(index);
      Rebuild();
    }
  }

  /// <summary>Replaces the whole root set, resetting colours to palette order.</summary>
  public void SetRoots(Complex[] roots)
  {
    ArgumentNullException.ThrowIfNull(roots);

    if (roots.Length is 0 || roots.Length > MaxRoots)
      throw new ArgumentException($"Need 1 to {MaxRoots} roots.", nameof(roots));

    lock (_sync)
    {
      _roots.Clear();
      _roots.AddRange(roots);

      _slots.Clear();
      _slots.AddRange(Enumerable.Range(0, roots.Length));

      Rebuild();
    }
  }

  // ---------- view ----------

  public void SetView(float centreRe, float centreIm, float scale)
  {
    lock (_sync)
    {
      CentreRe = centreRe;
      CentreIm = centreIm;
      Scale = scale;
    }
  }

  /// <summary>Sets the iteration budget, clamped to 0..<see cref="IterationLimit"/>.</summary>
  public void SetMaxIterations(int value)
  {
    lock (_sync)
      MaxIterations = Math.Clamp(value, 0, IterationLimit);
  }

  private int FreeSlot()
  {
    for (int slot = 0; slot < PaletteSize; slot++)
      if (!_slots.Contains(slot))
        return slot;

    return 0;
  }

  // Call under _sync. Roots keep their colour across add and remove, so the GPU needs
  // the colours ordered by root position rather than the raw palette.
  private void Rebuild()
  {
    Complex[] roots = [.. _roots];
    _poly = new Poly(roots);

    RootInfo[] info = new RootInfo[roots.Length];

    for (int i = 0; i < roots.Length; i++)
    {
      int p = _slots[i] * 3;
      info[i] = new RootInfo(i, roots[i], Palette[p], Palette[p + 1], Palette[p + 2]);

      _colours[i * 3 + 0] = Palette[p + 0];
      _colours[i * 3 + 1] = Palette[p + 1];
      _colours[i * 3 + 2] = Palette[p + 2];
    }

    Roots = info;
    _coloursDirty = true;
  }

  // ---------- rendering ----------

  public byte[] Render()
  {
    Poly poly;
    ViewParams view;
    byte[]? colours = null;

    // Snapshot under the lock: the UI thread can edit roots mid-render, and Poly is a
    // large struct that would otherwise be copied while it is being rewritten.
    lock (_sync)
    {
      poly = _poly;
      view = new ViewParams(Width, Height, Scale, CentreRe, CentreIm, MaxIterations);

      if (_coloursDirty)
      {
        colours = [.. _colours];
        _coloursDirty = false;
      }
    }

    // Every GPU call stays on this thread.
    if (colours is not null)
      PaletteBuffer.CopyFromCPU(colours);

    Kernel(PixelCount, RgbaBuffer.View, PaletteBuffer.View, poly, view);
    RgbaBuffer.CopyToCPU(Rgba);
    return Rgba;
  }

  private static void ComputeKernel(Index1D i, ArrayView<byte> buffer, ArrayView<byte> palette, Poly poly, ViewParams view)
  {
    int xi = i % view.Width;
    int yi = i / view.Width;

    float x = view.CentreRe + (xi - view.Width * 0.5f) * view.Scale;
    float y = view.CentreIm + (yi - view.Height * 0.5f) * view.Scale;

    int root = poly.FindRoot(new Complex(x, y), view.MaxIterations);

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