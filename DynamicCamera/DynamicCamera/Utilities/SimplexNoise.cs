// 2D simplex noise, based on Stefan Gustavson's public domain implementation
// (the same algorithm as org.joml.SimplexNoise, which the original CameraOverhaul uses).

namespace DynamicCamera.Utilities;

internal static class SimplexNoise
{
    private const double F2 = 0.36602540378443865; // 0.5 * (sqrt(3) - 1)
    private const double G2 = 0.21132486540518713; // (3 - sqrt(3)) / 6

    // Gradients of the 3D cube edges, z dropped (x, y pairs).
    private static readonly sbyte[] Grad = [1, 1, -1, 1, 1, -1, -1, -1, 1, 0, -1, 0, 1, 0, -1, 0, 0, 1, 0, -1, 0, 1, 0, -1];

    // Ken Perlin's reference permutation.
    private static readonly byte[] P =
    [
        151, 160, 137, 91, 90, 15, 131, 13, 201, 95, 96, 53, 194, 233, 7, 225, 140, 36, 103, 30, 69, 142, 8, 99, 37, 240, 21, 10,
        23, 190, 6, 148, 247, 120, 234, 75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32, 57, 177, 33, 88, 237, 149, 56, 87,
        174, 20, 125, 136, 171, 168, 68, 175, 74, 165, 71, 134, 139, 48, 27, 166, 77, 146, 158, 231, 83, 111, 229, 122, 60, 211,
        133, 230, 220, 105, 92, 41, 55, 46, 245, 40, 244, 102, 143, 54, 65, 25, 63, 161, 1, 216, 80, 73, 209, 76, 132, 187, 208,
        89, 18, 169, 200, 196, 135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186, 3, 64, 52, 217, 226, 250, 124, 123, 5,
        202, 38, 147, 118, 126, 255, 82, 85, 212, 207, 206, 59, 227, 47, 16, 58, 17, 182, 189, 28, 42, 223, 183, 170, 213, 119,
        248, 152, 2, 44, 154, 163, 70, 221, 153, 101, 155, 167, 43, 172, 9, 129, 22, 39, 253, 19, 98, 108, 110, 79, 113, 224, 232,
        178, 185, 112, 104, 218, 246, 97, 228, 251, 34, 242, 193, 238, 210, 144, 12, 191, 179, 162, 241, 81, 51, 145, 235, 249,
        14, 239, 107, 49, 192, 214, 31, 181, 199, 106, 157, 184, 84, 204, 176, 115, 121, 50, 45, 127, 4, 150, 254, 138, 236, 205,
        93, 222, 114, 67, 29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180,
    ];

    private static readonly byte[] Perm = new byte[512];
    private static readonly byte[] PermMod12 = new byte[512];

    static SimplexNoise()
    {
        for (int i = 0; i < 512; i++)
        {
            Perm[i] = P[i & 255];
            PermMod12[i] = (byte)(Perm[i] % 12);
        }
    }

    /// <summary> Samples 2D simplex noise. The result is in the [-1, 1] range. </summary>
    public static double Noise(double xin, double yin)
    {
        // Skew the input space to determine which simplex cell we're in.
        double s = (xin + yin) * F2;
        int i = FastFloor(xin + s);
        int j = FastFloor(yin + s);
        double t = (i + j) * G2;
        // Distances from the cell origin.
        double x0 = xin - (i - t);
        double y0 = yin - (j - t);

        // Determine which simplex (triangle) we're in.
        int i1, j1;
        if (x0 > y0)
        {
            i1 = 1;
            j1 = 0;
        }
        else
        {
            i1 = 0;
            j1 = 1;
        }

        double x1 = x0 - i1 + G2;
        double y1 = y0 - j1 + G2;
        double x2 = x0 - 1.0 + 2.0 * G2;
        double y2 = y0 - 1.0 + 2.0 * G2;

        int ii = i & 255;
        int jj = j & 255;
        int gi0 = PermMod12[ii + Perm[jj]];
        int gi1 = PermMod12[ii + i1 + Perm[jj + j1]];
        int gi2 = PermMod12[ii + 1 + Perm[jj + 1]];

        return 70.0 * (Corner(gi0, x0, y0) + Corner(gi1, x1, y1) + Corner(gi2, x2, y2));
    }

    private static double Corner(int gradientIndex, double x, double y)
    {
        double t = 0.5 - x * x - y * y;

        if (t < 0.0)
        {
            return 0.0;
        }

        t *= t;
        return t * t * (Grad[gradientIndex * 2] * x + Grad[gradientIndex * 2 + 1] * y);
    }

    private static int FastFloor(double x)
    {
        int xi = (int)x;
        return x < xi ? xi - 1 : xi;
    }
}
