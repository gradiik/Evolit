namespace Evolit.Core;

public static class TriangleWinding
{
    public static bool RequiresSwapToClockwise(
        CoreVector3 a,
        CoreVector3 b,
        CoreVector3 c,
        CoreVector3 outward)
    {
        var cross = CoreVector3.Cross(b - a, c - a);
        return CoreVector3.Dot(cross, outward) >= 0f;
    }
}
