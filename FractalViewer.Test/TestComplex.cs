using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FractalViewer.Core;

namespace FractalViewer.Test;

[TestClass]
public class ComplexTests
{
  private const float Tolerance = 1e-5f;

  private static void AssertComplexAreEqual(Complex expected, Complex actual, float tolerance = Tolerance)
  {
    Assert.AreEqual(expected.Real, actual.Real, tolerance, $"Real part mismatch. Expected {expected.Real}, got {actual.Real}.");
    Assert.AreEqual(expected.Imaginary, actual.Imaginary, tolerance, $"Imaginary part mismatch. Expected {expected.Imaginary}, got {actual.Imaginary}.");
  }

  #region Constructor

  [TestMethod]
  public void Constructor_SetsRealAndImaginary()
  {
    var c = new Complex(3.0f, -4.0f);

    Assert.AreEqual(3.0f, c.Real);
    Assert.AreEqual(-4.0f, c.Imaginary);
  }

  [TestMethod]
  public void Constructor_DefaultStruct_HasZeroFields()
  {
    var c = default(Complex);

    Assert.AreEqual(0.0f, c.Real);
    Assert.AreEqual(0.0f, c.Imaginary);
  }

  #endregion

  #region Static Properties

  [TestMethod]
  public void One_HasExpectedValues()
  {
    Assert.AreEqual(1.0f, Complex.One.Real);
    Assert.AreEqual(0.0f, Complex.One.Imaginary);
  }

  [TestMethod]
  public void Zero_HasExpectedValues()
  {
    Assert.AreEqual(0.0f, Complex.Zero.Real);
    Assert.AreEqual(0.0f, Complex.Zero.Imaginary);
  }

  #endregion

  #region FromPolarRads / FromPolarDegs

  [TestMethod]
  public void FromPolarRads_ZeroAngle_ReturnsRealValueOnly()
  {
    var c = Complex.FromPolarRads(5.0f, 0.0f);

    AssertComplexAreEqual(new Complex(5.0f, 0.0f), c);
  }

  [TestMethod]
  public void FromPolarRads_HalfPiAngle_ReturnsImaginaryValueOnly()
  {
    var c = Complex.FromPolarRads(2.0f, MathF.PI / 2.0f);

    AssertComplexAreEqual(new Complex(0.0f, 2.0f), c, 1e-4f);
  }

  [TestMethod]
  public void FromPolarRads_PiAngle_ReturnsNegativeRealValue()
  {
    var c = Complex.FromPolarRads(3.0f, MathF.PI);

    AssertComplexAreEqual(new Complex(-3.0f, 0.0f), c, 1e-4f);
  }

  [TestMethod]
  public void FromPolarRads_ArbitraryAngle_MatchesTrigExpectation()
  {
    float r = 4.0f;
    float angle = 0.7f;
    var c = Complex.FromPolarRads(r, angle);

    AssertComplexAreEqual(new Complex(r * MathF.Cos(angle), r * MathF.Sin(angle)), c);
  }

  [TestMethod]
  public void FromPolarDegs_ZeroDegrees_ReturnsRealValueOnly()
  {
    var c = Complex.FromPolarDegs(7.0f, 0.0f);

    AssertComplexAreEqual(new Complex(7.0f, 0.0f), c);
  }

  [TestMethod]
  public void FromPolarDegs_NinetyDegrees_ReturnsImaginaryValueOnly()
  {
    var c = Complex.FromPolarDegs(1.0f, 90.0f);

    AssertComplexAreEqual(new Complex(0.0f, 1.0f), c, 1e-4f);
  }

  [TestMethod]
  public void FromPolarDegs_OneEightyDegrees_ReturnsNegativeRealValue()
  {
    var c = Complex.FromPolarDegs(2.0f, 180.0f);

    AssertComplexAreEqual(new Complex(-2.0f, 0.0f), c, 1e-4f);
  }

  [TestMethod]
  public void FromPolarDegs_MatchesFromPolarRads_ForEquivalentAngle()
  {
    float r = 6.0f;
    float degrees = 37.0f;

    var fromDegs = Complex.FromPolarDegs(r, degrees);
    var fromRads = Complex.FromPolarRads(r, degrees * MathF.PI / 180.0f);

    AssertComplexAreEqual(fromRads, fromDegs);
  }

  #endregion

  #region Magnitude / MagnitudeSquared

  [TestMethod]
  public void Magnitude_ClassicThreeFourFiveTriangle_ReturnsFive()
  {
    var c = new Complex(3.0f, 4.0f);

    Assert.AreEqual(5.0f, c.Magnitude(), Tolerance);
  }

  [TestMethod]
  public void Magnitude_Zero_ReturnsZero()
  {
    Assert.AreEqual(0.0f, Complex.Zero.Magnitude(), Tolerance);
  }

  [TestMethod]
  public void Magnitude_NegativeComponents_ReturnsPositiveValue()
  {
    var c = new Complex(-3.0f, -4.0f);

    Assert.AreEqual(5.0f, c.Magnitude(), Tolerance);
  }

  [TestMethod]
  public void MagnitudeSquared_ClassicThreeFourFiveTriangle_ReturnsTwentyFive()
  {
    var c = new Complex(3.0f, 4.0f);

    Assert.AreEqual(25.0f, c.MagnitudeSquared(), Tolerance);
  }

  [TestMethod]
  public void MagnitudeSquared_MatchesMagnitudeSquared()
  {
    var c = new Complex(1.5f, -2.5f);

    Assert.AreEqual(c.Magnitude() * c.Magnitude(), c.MagnitudeSquared(), 1e-4f);
  }

  #endregion

  #region Operator +

  [TestMethod]
  public void Addition_AddsRealAndImaginaryParts()
  {
    var a = new Complex(1.0f, 2.0f);
    var b = new Complex(3.0f, -5.0f);

    var result = a + b;

    AssertComplexAreEqual(new Complex(4.0f, -3.0f), result);
  }

  [TestMethod]
  public void Addition_WithZero_ReturnsOriginalValue()
  {
    var a = new Complex(2.5f, -1.5f);

    var result = a + Complex.Zero;

    AssertComplexAreEqual(a, result);
  }

  #endregion

  #region Operator -

  [TestMethod]
  public void Subtraction_SubtractsRealAndImaginaryParts()
  {
    var a = new Complex(5.0f, 3.0f);
    var b = new Complex(2.0f, 7.0f);

    var result = a - b;

    AssertComplexAreEqual(new Complex(3.0f, -4.0f), result);
  }

  [TestMethod]
  public void Subtraction_FromItself_ReturnsZero()
  {
    var a = new Complex(9.0f, -2.0f);

    var result = a - a;

    AssertComplexAreEqual(Complex.Zero, result);
  }

  [TestMethod]
  public void UnaryNegation_NegatesBothComponents()
  {
    var a = new Complex(3.0f, -4.0f);

    var result = -a;

    AssertComplexAreEqual(new Complex(-3.0f, 4.0f), result);
  }

  [TestMethod]
  public void UnaryNegation_OfZero_ReturnsZero()
  {
    var result = -Complex.Zero;

    AssertComplexAreEqual(Complex.Zero, result);
  }

  #endregion

  #region Operator * (Complex * Complex)

  [TestMethod]
  public void Multiplication_TwoComplexNumbers_ReturnsExpectedResult()
  {
    // (1 + 2i) * (3 + 4i) = (3 - 8) + (4 + 6)i = -5 + 10i
    var a = new Complex(1.0f, 2.0f);
    var b = new Complex(3.0f, 4.0f);

    var result = a * b;

    AssertComplexAreEqual(new Complex(-5.0f, 10.0f), result);
  }

  [TestMethod]
  public void Multiplication_ByOne_ReturnsOriginalValue()
  {
    var a = new Complex(6.0f, -3.0f);

    var result = a * Complex.One;

    AssertComplexAreEqual(a, result);
  }

  [TestMethod]
  public void Multiplication_ByZero_ReturnsZero()
  {
    var a = new Complex(6.0f, -3.0f);

    var result = a * Complex.Zero;

    AssertComplexAreEqual(Complex.Zero, result);
  }

  [TestMethod]
  public void Multiplication_ImaginaryUnitSquared_ReturnsNegativeOne()
  {
    var i = new Complex(0.0f, 1.0f);

    var result = i * i;

    AssertComplexAreEqual(new Complex(-1.0f, 0.0f), result);
  }

  #endregion

  #region Operator * (Complex * float)

  [TestMethod]
  public void ScalarMultiplication_ScalesBothComponents()
  {
    var a = new Complex(2.0f, -3.0f);

    var result = a * 2.5f;

    AssertComplexAreEqual(new Complex(5.0f, -7.5f), result);
  }

  [TestMethod]
  public void ScalarMultiplication_ByZero_ReturnsZero()
  {
    var a = new Complex(2.0f, -3.0f);

    var result = a * 0.0f;

    AssertComplexAreEqual(Complex.Zero, result);
  }

  [TestMethod]
  public void ScalarMultiplication_ByOne_ReturnsOriginalValue()
  {
    var a = new Complex(2.0f, -3.0f);

    var result = a * 1.0f;

    AssertComplexAreEqual(a, result);
  }

  [TestMethod]
  public void ScalarMultiplication_ByNegative_NegatesResult()
  {
    var a = new Complex(2.0f, -3.0f);

    var result = a * -1.0f;

    AssertComplexAreEqual(new Complex(-2.0f, 3.0f), result);
  }

  #endregion

  #region Operator /

  [TestMethod]
  public void Division_TwoComplexNumbers_ReturnsExpectedResult()
  {
    // (1 + 2i) / (3 + 4i) = (1+2i)(3-4i) / 25 = (11 + 2i) / 25 = 0.44 + 0.08i
    var a = new Complex(1.0f, 2.0f);
    var b = new Complex(3.0f, 4.0f);

    var result = a / b;

    AssertComplexAreEqual(new Complex(0.44f, 0.08f), result, 1e-4f);
  }

  [TestMethod]
  public void Division_ByOne_ReturnsOriginalValue()
  {
    var a = new Complex(5.0f, -2.0f);

    var result = a / Complex.One;

    AssertComplexAreEqual(a, result);
  }

  [TestMethod]
  public void Division_ByItself_ReturnsOne()
  {
    var a = new Complex(3.0f, 4.0f);

    var result = a / a;

    AssertComplexAreEqual(Complex.One, result, 1e-4f);
  }

  [TestMethod]
  public void Division_ZeroByNonZero_ReturnsZero()
  {
    var b = new Complex(3.0f, 4.0f);

    var result = Complex.Zero / b;

    AssertComplexAreEqual(Complex.Zero, result);
  }

  [TestMethod]
  public void Division_ByZero_ProducesNaNComponents()
  {
    var a = new Complex(1.0f, 1.0f);

    var result = a / Complex.Zero;

    Assert.IsTrue(float.IsNaN(result.Real));
    Assert.IsTrue(float.IsNaN(result.Imaginary));
  }

  #endregion
}
