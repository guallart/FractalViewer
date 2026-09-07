using FractalViewer.Core;
using FractalViewer.NewtonRaphson;

using ILGPU;
using ILGPU.Runtime;

public class FractalRenderer : IDisposable
{
  private int Width { get; }
  private int Height { get; }
  private float Scale { get; }
  private Poly Poly { get; set; }

  private Accelerator Accelerator { get; }
  private Context Context { get; }
  private MemoryBuffer1D<byte, Stride1D.Dense> RgbaBuffer { get; }
  private MemoryBuffer1D<byte, Stride1D.Dense> PaletteBuffer { get; }
  private byte[] Rgba { get; }
  private Action<Index1D, ArrayView<byte>, ArrayView<byte>, Poly, int, int, float> Kernel { get; }

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

  private int PixelCount => Width * Height;

  public FractalRenderer(int width, int height, float scale)
  {
    Width = width;
    Height = height;
    Scale = scale;

    Context = Context.CreateDefault();
    Accelerator = Context.GetPreferredDevice(preferCPU: false).CreateAccelerator(Context);

    Poly = new Poly([Complex.One, Complex.OneI, -Complex.OneI]);

    Rgba = new byte[PixelCount * 4];
    RgbaBuffer = Accelerator.Allocate1D<byte>(PixelCount * 4);
    PaletteBuffer = Accelerator.Allocate1D(Palette);

    Kernel = Accelerator.LoadAutoGroupedStreamKernel
      <Index1D, ArrayView<byte>, ArrayView<byte>, Poly, int, int, float>(ComputeKernel);
  }

  public byte[] Render()
  {
    Kernel(PixelCount, RgbaBuffer.View, PaletteBuffer.View, Poly, Width, Height, Scale);
    RgbaBuffer.CopyToCPU(Rgba);
    return Rgba;
  }

  private static void ComputeKernel(Index1D i, ArrayView<byte> buffer, ArrayView<byte> palette, Poly poly, int width, int height, float scale)
  {
    int xi = i % width;
    int yi = i / width;

    float x = (xi - width * 0.5f) * scale;
    float y = (height * 0.5f - yi) * scale;

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