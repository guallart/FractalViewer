using FractalViewer.Core;

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FractalViewer.Test")]

namespace FractalViewer.NewtonRaphson;

[InlineArray(Poly.MaxDegree + 1)]
public struct ComplexBuffer
{
#pragma warning disable S1144
  private Complex _element0;
#pragma warning restore S1144
}

public struct Poly
{
  public const int MaxDegree = 8;
  private const int MaxIters = 100;
  private const float ToleranceSquared = 1e-6f;

  private readonly int Degree;
  private ComplexBuffer Coeffs;
  private ComplexBuffer Roots; 

  public Poly(Complex[] roots)
  {
    Degree = roots.Length;
    Coeffs = default; // all zeros
    Roots = default; // all zeros
    Coeffs[0] = Complex.One;
    int currentDegree = 0;

    for (int i = 0; i < Degree; i++)
      Roots[i] = roots[i];

    foreach (Complex r in roots)
    {
      for (int i = currentDegree + 1; i >= 1; i--)
        Coeffs[i] = Coeffs[i - 1] - r * Coeffs[i];

      Coeffs[0] = -r * Coeffs[0];
      currentDegree++;
    }
  }

  private Complex NewtonStep(Complex x)
  {
    Complex value = Coeffs[Degree];
    Complex deriv = Complex.Zero;

    for (int i = Degree - 1; i >= 0; i--)
    {
      deriv = deriv * x + value;
      value = value * x + Coeffs[i];
    }

    return value / deriv; // f(x) / f'(x)
  }

  public int FindRoot(Complex z)
  {
    for (int i = 0; i < MaxIters; i++)
    {
      Complex step = NewtonStep(z);
      z -= step;

      if (step.MagnitudeSquared() < ToleranceSquared)
        break;
    }

    int bestIndex = -1;
    float bestDistSquared = float.MaxValue;

    for (int i = 0; i < Degree; i++)
    {
      float distSquared = (z - Roots[i]).MagnitudeSquared();

      if (distSquared < bestDistSquared)
      {
        bestDistSquared = distSquared;
        bestIndex = i;
      }
    }

    return bestIndex;
  }
}
