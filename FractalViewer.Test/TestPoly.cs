using FractalViewer.Core;
using FractalViewer.NewtonRaphson;

namespace FractalViewer.Test;

[TestClass]
public class PolyTests
{
  #region Single root

  [TestMethod]
  public void FindRoot_SingleRealRoot_StartingNearby_ReturnsIndexZero()
  {
    var poly = new Poly(new[] { new Complex(2.0f, 0.0f) });

    int index = poly.FindRoot(new Complex(2.5f, 0.3f));

    Assert.AreEqual(0, index);
  }

  [TestMethod]
  public void FindRoot_SingleRoot_StartingExactlyAtRoot_ReturnsIndexZeroImmediately()
  {
    var root = new Complex(3.0f, -1.5f);
    var poly = new Poly(new[] { root });

    int index = poly.FindRoot(root);

    Assert.AreEqual(0, index);
  }

  [TestMethod]
  public void FindRoot_SinglePurelyImaginaryRoot_ConvergesToIt()
  {
    var poly = new Poly(new[] { new Complex(0.0f, 3.0f) });

    int index = poly.FindRoot(new Complex(0.4f, 2.6f));

    Assert.AreEqual(0, index);
  }

  #endregion

  #region Two roots

  [TestMethod]
  public void FindRoot_TwoRealRoots_ConvergesToNearestStartingPoint()
  {
    // (x - 1)(x + 1) = x^2 - 1, roots at +1 and -1
    var poly = new Poly(new[]
    {
              new Complex(1.0f, 0.0f),
              new Complex(-1.0f, 0.0f)
          });

    int indexNearPositive = poly.FindRoot(new Complex(10.0f, 0.0f));
    int indexNearNegative = poly.FindRoot(new Complex(-10.0f, 0.0f));

    Assert.AreEqual(0, indexNearPositive);
    Assert.AreEqual(1, indexNearNegative);
  }

  [TestMethod]
  public void FindRoot_TwoRoots_StartingAtSecondRootExactly_ReturnsIndexOne()
  {
    var rootA = new Complex(5.0f, 0.0f);
    var rootB = new Complex(-2.0f, 4.0f);
    var poly = new Poly(new[] { rootA, rootB });

    int index = poly.FindRoot(rootB);

    Assert.AreEqual(1, index);
  }

  [TestMethod]
  public void FindRoot_RepeatedRoot_ReturnsOneOfTheMatchingIndices()
  {
    // (x - 1)^2, a double root at 1 -> derivative vanishes at the root,
    // so convergence is linear rather than quadratic, but it should
    // still land near 1 within the iteration budget.
    var root = new Complex(1.0f, 0.0f);
    var poly = new Poly(new[] { root, root });

    int index = poly.FindRoot(new Complex(1.5f, 0.0f));

    Assert.IsTrue(index == 0 || index == 1, $"Expected index 0 or 1, got {index}.");
  }

  #endregion

  #region Three or more roots / complex roots

  [TestMethod]
  public void FindRoot_CubeRootsOfUnity_ConvergeToExpectedRoots()
  {
    // x^3 - 1 = 0 -> roots are the three cube roots of unity
    var root0 = new Complex(1.0f, 0.0f);
    var root1 = Complex.FromPolarDegs(1.0f, 120.0f);
    var root2 = Complex.FromPolarDegs(1.0f, 240.0f);
    var poly = new Poly(new[] { root0, root1, root2 });

    int index0 = poly.FindRoot(root0 * 1.2f);
    int index1 = poly.FindRoot(root1 * 1.2f);
    int index2 = poly.FindRoot(root2 * 1.2f);

    Assert.AreEqual(0, index0);
    Assert.AreEqual(1, index1);
    Assert.AreEqual(2, index2);
  }

  [TestMethod]
  public void FindRoot_MixOfRealAndComplexRoots_ReturnsCorrectIndex()
  {
    var roots = new[]
    {
              new Complex(4.0f, 0.0f),
              new Complex(0.0f, 2.0f),
              new Complex(-3.0f, -1.0f)
          };
    var poly = new Poly(roots);

    for (int i = 0; i < roots.Length; i++)
    {
      // Nudge slightly off the exact root so Newton's method has to do real work.
      var start = roots[i] + new Complex(0.05f, -0.05f);
      int index = poly.FindRoot(start);

      Assert.AreEqual(i, index, $"Root at index {i} was not correctly identified.");
    }
  }

  [TestMethod]
  public void FindRoot_MaxDegreeRoots_AllRootsIdentifiedCorrectly()
  {
    var roots = new Complex[Poly.MaxDegree];
    for (int i = 0; i < roots.Length; i++)
    {
      // Spread roots around the unit circle so they're well separated.
      roots[i] = Complex.FromPolarDegs(2.0f, i * (360.0f / roots.Length));
    }

    var poly = new Poly(roots);

    for (int i = 0; i < roots.Length; i++)
    {
      int index = poly.FindRoot(roots[i]);
      Assert.AreEqual(i, index, $"Root at index {i} was not correctly identified.");
    }
  }

  #endregion

  #region Edge cases

  [TestMethod]
  public void FindRoot_NoRoots_ReturnsNegativeOne()
  {
    var poly = new Poly(Array.Empty<Complex>());

    int index = poly.FindRoot(new Complex(1.0f, 1.0f));

    Assert.AreEqual(-1, index);
  }

  [TestMethod]
  public void FindRoot_StartingFarFromAllRoots_StillReturnsClosestKnownRoot()
  {
    var roots = new[]
    {
              new Complex(1.0f, 0.0f),
              new Complex(100.0f, 100.0f)
          };
    var poly = new Poly(roots);

    // Starting near the far-away root should converge toward it,
    // not the nearby one.
    int index = poly.FindRoot(new Complex(90.0f, 95.0f));

    Assert.AreEqual(1, index);
  }

  #endregion
}
