using UnityEngine;
using static UnityEngine.Mathf;

public class BaseShapeGenerator
{
    private readonly float _scale = 1f;
    private readonly float _baseMultiplier = 2.5f;
    private readonly Vector3 _start = new(0f, 0f, 0f);

    private float Rad(float angle) => angle * Deg2Rad;

    public (Vector3[] vertices, int[] triangles) CalculateUpperBaseShape()
    {
        Vector3 B, C, H, E, M, S, U, T;
        B = _start;
        C = B + new Vector3(_baseMultiplier * _scale, 0f, 0f);
        H = C + new Vector3(_scale / 2 / Cos(Rad(55)), 0f, 0f);
        E = C + new Vector3(_scale * Cos(Rad(55)), 0f, _scale * Cos(Rad(35)));
        M = B + new Vector3(0f, 0f, Vector3.Distance(B, H) * Sin(Rad(35)) / Sin(Rad(55)));
        S = M + new Vector3(0f, 0f, Vector3.Distance(M, H) * Sin(Rad(35)) / Sin(Rad(75)) * Cos(Rad(55)));
        U = S + new Vector3(Vector3.Distance(M, S) * Sin(Rad(55)) / Sin(Rad(35)), 0f, 0f);
        T = S + new Vector3(0f, 0f, Vector3.Distance(S, U) * Sin(Rad(25)) / Sin(Rad(65)));

        Vector3 CC, EE, UU;
        CC = B + new Vector3(-(_baseMultiplier * _scale), 0f, 0f);
        EE = CC + new Vector3(-(_scale * Cos(Rad(55))), 0f, _scale * Cos(Rad(35)));
        UU = S + new Vector3(-Vector3.Distance(S, U), 0f, 0f);

        Vector3[] vertices = new[]
        {
            B, C, E, M, S, U, T, CC, EE, UU
        };

        int[] triangles = new int[]
        {
            0, 3, 1,    // B, M, C
            1, 3, 2,    // C, M, E
            3, 5, 2,    // M, U, E
            3, 4, 5,    // M, S, U
            4, 6, 5,    // S, T, U
            4, 9, 6,    // S, UU, T 
            4, 3, 9,    // S, M, UU
            3, 8, 9,    // M, EE, UU
            3, 7, 8,    // M, CC, EE
            3, 0, 7     // M, B, CC
        };

        return (vertices, triangles);
    }

    public (Vector3[] vertices, int[] triangles) CalculateLowerBaseShape()
    {
        Vector3 B, C, H, E, M, S, U, T, A;
        B = _start;
        C = B + new Vector3(_baseMultiplier * _scale, 0f, 0f);
        H = C + new Vector3(_scale, 0f, 0f);
        E = C + new Vector3(_scale * Cos(Rad(50f)), 0f, _scale * Cos(Rad(40f)));
        M = B + new Vector3(0f, 0f, _scale * (_baseMultiplier + 1) * Sin(Rad(40)) / Sin(Rad(50)));
        S = M + new Vector3(0f, 0f, Vector3.Distance(M, H) * Sin(Rad(25)) / Sin(Rad(85)) * Cos(Rad(60f)));
        U = S + new Vector3(Vector3.Distance(M, S) * Sin(Rad(60)) / Sin(Rad(30)), 0f, 0f);
        float distSU = Vector3.Distance(S, U);
        T = S + new Vector3(0f, 0f, distSU / 2 * Sin(Rad(40)) / Sin(Rad(50)));
        A = T + new Vector3(distSU / 2, 0f, 0f);

        Vector3 CC, EE, UU, AA;
        CC = B + new Vector3(-(_baseMultiplier * _scale), 0f, 0f);
        EE = CC + new Vector3(-(_scale * Cos(Rad(50f))), 0f, _scale * Cos(Rad(40f)));
        UU = S + new Vector3(-Vector3.Distance(S, U), 0f, 0f);
        AA = T + new Vector3(-Vector3.Distance(T, A), 0f, 0f);

        Vector3[] vertices = new[]
        {
            B, C, E, M, S, U, T, A, CC, EE, UU, AA
        };

        int[] triangles = new int[]
        {
            0, 3, 1,    // B, M, C
            1, 3, 2,    // C, M, E
            3, 5, 2,    // M, U, E
            3, 4, 5,    // M, S, U
            4, 7, 5,    // S, A, U
            4, 6, 7,    // S, T, A
            4, 11, 6,   // S, AA, T
            4, 10, 11,  // S, UU, AA
            4, 3, 10,   // S, M, UU
            3, 9, 10,   // M, EE, UU
            3, 8, 9,    // M, CC, EE
            3, 0, 8     // M, B, CC
        };

        return (vertices, triangles);
    }
}
