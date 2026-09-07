namespace FractalViewer.Core;

public struct Complex
{
  public float Real { get; set; }
  public float Imaginary { get; set; }

  public static Complex One { get; } = new Complex(1.0f, 0.0f);
  public static Complex Zero { get; } = new Complex(0.0f, 0.0f);
  public static Complex OneI { get; } = new Complex(0.0f, 1.0f);

  public Complex(float r, float i)
  {
    Real = r;
    Imaginary = i;
  }

  public static Complex FromPolarRads(float r, float angle)
  {
    float x = r * MathF.Cos(angle);
    float y = r * MathF.Sin(angle);
    return new Complex(x, y);
  }

  public static Complex FromPolarDegs(float r, float angle)
  {
    return FromPolarRads(r, angle * MathF.PI / 180.0f);
  }

  public float Magnitude()
  {
    return MathF.Sqrt(Real * Real + Imaginary * Imaginary);
  }

  public float MagnitudeSquared()
  {
    return Real * Real + Imaginary * Imaginary;
  }

  public static Complex operator +(Complex a, Complex b) => new Complex(a.Real + b.Real, a.Imaginary + b.Imaginary);
  public static Complex operator -(Complex a, Complex b) => new Complex(a.Real - b.Real, a.Imaginary - b.Imaginary);
  public static Complex operator -(Complex a) => new Complex(-a.Real, -a.Imaginary);
  public static Complex operator *(Complex a, Complex b)
  {
    return new Complex(
      a.Real * b.Real - a.Imaginary * b.Imaginary,
      a.Real * b.Imaginary + a.Imaginary * b.Real
    );
  }
  public static Complex operator /(Complex a, Complex b)
  {
    float denom = b.Real * b.Real + b.Imaginary * b.Imaginary;
    return new Complex(
      (a.Real * b.Real + a.Imaginary * b.Imaginary) / denom,
      (a.Imaginary * b.Real - a.Real * b.Imaginary) / denom
    );
  }
  public static Complex operator *(Complex a, float x) => new Complex(a.Real * x, a.Imaginary * x);

}
