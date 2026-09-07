using FractalViewer.Core;

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FractalViewer.Test")]

namespace FractalViewer.NewtonRaphson;

public struct Poly
{
  public const int MaxDegree = 8;
  private const int MaxIters = 100;
  private const float ToleranceSquared = 1e-6f;

  private readonly int Degree;

  private Complex c0, c1, c2, c3, c4, c5, c6, c7, c8;   // MaxDegree + 1
  private Complex r0, r1, r2, r3, r4, r5, r6, r7;       // MaxDegree

  public Poly(Complex[] roots)
  {
    if (roots.Length > MaxDegree)
      throw new ArgumentException($"Degree exceeds {MaxDegree}.", nameof(roots));

    this = default;
    Degree = roots.Length;

    for (int i = 0; i < Degree; i++)
      SetRoot(i, roots[i]);

    SetCoeff(0, Complex.One);
    int currentDegree = 0;

    foreach (Complex r in roots)
    {
      for (int i = currentDegree + 1; i >= 1; i--)
        SetCoeff(i, GetCoeff(i - 1) - r * GetCoeff(i));

      SetCoeff(0, -r * GetCoeff(0));
      currentDegree++;
    }
  }

  private readonly Complex GetCoeff(int i) => i switch
  {
    0 => c0,
    1 => c1,
    2 => c2,
    3 => c3,
    4 => c4,
    5 => c5,
    6 => c6,
    7 => c7,
    _ => c8,
  };

  private readonly Complex GetRoot(int i) => i switch
  {
    0 => r0,
    1 => r1,
    2 => r2,
    3 => r3,
    4 => r4,
    5 => r5,
    6 => r6,
    _ => r7,
  };

  private void SetCoeff(int i, Complex v)
  {
    switch (i)
    {
      case 0: c0 = v; break;
      case 1: c1 = v; break;
      case 2: c2 = v; break;
      case 3: c3 = v; break;
      case 4: c4 = v; break;
      case 5: c5 = v; break;
      case 6: c6 = v; break;
      case 7: c7 = v; break;
      default: c8 = v; break;
    }
  }

  private void SetRoot(int i, Complex v)
  {
    switch (i)
    {
      case 0: r0 = v; break;
      case 1: r1 = v; break;
      case 2: r2 = v; break;
      case 3: r3 = v; break;
      case 4: r4 = v; break;
      case 5: r5 = v; break;
      case 6: r6 = v; break;
      default: r7 = v; break;
    }
  }

  private readonly Complex NewtonStep(Complex x)
  {
    Complex value = GetCoeff(Degree);
    Complex deriv = Complex.Zero;

    for (int i = Degree - 1; i >= 0; i--)
    {
      deriv = deriv * x + value;
      value = value * x + GetCoeff(i);
    }

    return value / deriv;
  }

  public readonly int FindRoot(Complex z)
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
      float distSquared = (z - GetRoot(i)).MagnitudeSquared();

      if (distSquared < bestDistSquared)
      {
        bestDistSquared = distSquared;
        bestIndex = i;
      }
    }

    return bestIndex;
  }
}