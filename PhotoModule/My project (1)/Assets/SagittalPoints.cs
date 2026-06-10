using UnityEngine;
using System.IO;

using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;

public class SagittalPoints : MonoBehaviour
{
    [Tooltip("The trained model as raw binary data, i.e. 'face_landmarker.bytes'!")]
    [SerializeField] private TextAsset modelAsset;

    [Tooltip("Image Texture2D with Read/Write enabled.")]
    [SerializeField] private Texture2D inputTexture;

    private FaceLandmarker _faceLandmarker;

    // MediaPipe FaceMesh face oval / outline indices
    private readonly int[] faceOvalIndices =
    {
        10, 338, 297, 332, 284, 251, 389, 356,
        454, 323, 361, 288, 397, 365, 379, 378,
        400, 377, 152, 148, 176, 149, 150, 136,
        172, 58, 132, 93, 234, 127, 162, 21,
        54, 103, 67, 109
    };

    // Approximate anatomical landmarks using MediaPipe FaceMesh indices.
    // True trichion = hairline, which MediaPipe does not directly detect.
    private const int TRICHION_INDEX = 10;
    private const int GLABELLA_INDEX = 9;
    private const int SUBNASALE_INDEX = 2;
    private const int MENTON_INDEX = 152;

    void Start()
    {
        InitializeLandmarker();

        if (inputTexture != null)
        {
            ProcessImage(inputTexture);
        }
        else
        {
            Debug.LogWarning("Missing an input Texture2D for analysis!");
        }
    }

    private void InitializeLandmarker()
    {
        if (modelAsset == null)
        {
            Debug.LogError("Missing face landmarker model asset!");
            return;
        }

        var baseOptions = new BaseOptions
        (
            BaseOptions.Delegate.CPU,
            modelAssetBuffer: modelAsset.bytes
        );

        var options = new FaceLandmarkerOptions
        (
            baseOptions: baseOptions,
            runningMode: RunningMode.IMAGE,
            numFaces: 1
        );

        _faceLandmarker = FaceLandmarker.CreateFromOptions(options);
    }

    private void ProcessImage(Texture2D texture)
    {
        int width = texture.width;
        int height = texture.height;

        Color32[] originalPixels = texture.GetPixels32();
        Color32[] flippedPixels = new Color32[originalPixels.Length];

        // Vertical flip for MediaPipe input
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                flippedPixels[(height - 1 - y) * width + x] = originalPixels[y * width + x];
            }
        }

        Texture2D flippedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        flippedTexture.SetPixels32(flippedPixels);
        flippedTexture.Apply();

        Texture2D displayTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        displayTexture.SetPixels32(originalPixels);
        displayTexture.Apply();

        using var mpImage = new Mediapipe.Image(flippedTexture);
        FaceLandmarkerResult result = _faceLandmarker.Detect(mpImage);

        Destroy(flippedTexture);

        if (result.faceLandmarks != null && result.faceLandmarks.Count > 0)
        {
            var firstFace = result.faceLandmarks[0];

            Vector2Int[] outlinePoints = new Vector2Int[faceOvalIndices.Length];

            int minX = width;
            int maxX = 0;
            int minY = height;
            int maxY = 0;

            // Calculate face outline points and face bounding area
            for (int i = 0; i < faceOvalIndices.Length; i++)
            {
                int index = faceOvalIndices[i];

                float normX = firstFace.landmarks[index].x;
                float normY = firstFace.landmarks[index].y;

                Vector2Int point = NormalizedToPixel(normX, normY, width, height);
                outlinePoints[i] = point;

                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            // Draw connected face outline
            for (int i = 0; i < outlinePoints.Length; i++)
            {
                Vector2Int a = outlinePoints[i];
                Vector2Int b = outlinePoints[(i + 1) % outlinePoints.Length];

                DrawLine(displayTexture, a.x, a.y, b.x, b.y, Color.yellow, 3);
            }

            // Get anatomical landmark points
            Vector2Int trichion = NormalizedToPixel(
                firstFace.landmarks[TRICHION_INDEX].x,
                firstFace.landmarks[TRICHION_INDEX].y,
                width,
                height
            );

            Vector2Int glabella = NormalizedToPixel(
                firstFace.landmarks[GLABELLA_INDEX].x,
                firstFace.landmarks[GLABELLA_INDEX].y,
                width,
                height
            );

            Vector2Int subnasale = NormalizedToPixel(
                firstFace.landmarks[SUBNASALE_INDEX].x,
                firstFace.landmarks[SUBNASALE_INDEX].y,
                width,
                height
            );

            Vector2Int menton = NormalizedToPixel(
                firstFace.landmarks[MENTON_INDEX].x,
                firstFace.landmarks[MENTON_INDEX].y,
                width,
                height
            );

            // Distances along the vertical sagittal direction
            float trichionToGlabella = Mathf.Abs(glabella.y - trichion.y);
            float glabellaToSubnasale = Mathf.Abs(subnasale.y - glabella.y);
            float subnasaleToMenton = Mathf.Abs(menton.y - subnasale.y);

            Debug.Log("Trichion point: " + trichion);
            Debug.Log("Glabella point: " + glabella);
            Debug.Log("Subnasale point: " + subnasale);
            Debug.Log("Menton point: " + menton);

            Debug.Log("Trichion → Glabella distance: " + trichionToGlabella + " px");
            Debug.Log("Glabella → Subnasale distance: " + glabellaToSubnasale + " px");
            Debug.Log("Subnasale → Menton distance: " + subnasaleToMenton + " px");

            // Highlight anatomical landmark points
            DrawPoint(displayTexture, trichion.x, trichion.y, Color.red, 11);
            DrawPoint(displayTexture, glabella.x, glabella.y, Color.green, 11);
            DrawPoint(displayTexture, subnasale.x, subnasale.y, Color.blue, 11);
            DrawPoint(displayTexture, menton.x, menton.y, Color.magenta, 11);

            // Draw sagittal line segments
            DrawLine(displayTexture, trichion.x, trichion.y, glabella.x, glabella.y, Color.white, 2);
            DrawLine(displayTexture, glabella.x, glabella.y, subnasale.x, subnasale.y, Color.white, 2);
            DrawLine(displayTexture, subnasale.x, subnasale.y, menton.x, menton.y, Color.white, 2);

            displayTexture.Apply();

            // Square crop based on face height
            int faceHeight = maxY - minY;
            int cropSize = faceHeight * 2;

            int centerX = (minX + maxX) / 2;
            int centerY = (minY + maxY) / 2;

            Debug.Log("Face height in pixels: " + faceHeight);
            Debug.Log("Square crop size: " + cropSize + " x " + cropSize);

            Texture2D squareCrop = CropSquareWithPadding(displayTexture, centerX, centerY, cropSize);

            string outlinePath = Application.dataPath + "/face_outline_sagittal_result.png";
            string cropPath = Application.dataPath + "/face_square_crop_sagittal.png";

            File.WriteAllBytes(outlinePath, displayTexture.EncodeToPNG());
            File.WriteAllBytes(cropPath, squareCrop.EncodeToPNG());

            Debug.Log("Saved outline image to: " + outlinePath);
            Debug.Log("Saved square crop to: " + cropPath);

            Destroy(squareCrop);
        }
        else
        {
            Debug.LogWarning("NO face detected!");
        }

        Destroy(displayTexture);
    }

    private Vector2Int NormalizedToPixel(float normX, float normY, int width, int height)
    {
        int x = Mathf.RoundToInt(normX * (width - 1));
        int y = Mathf.RoundToInt((1f - normY) * (height - 1));

        return new Vector2Int(x, y);
    }

    private void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color color, int thickness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);

        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;

        int err = dx - dy;

        while (true)
        {
            DrawPoint(tex, x0, y0, color, thickness);

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;

            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private void DrawPoint(Texture2D tex, int x, int y, Color color, int size)
    {
        int radius = size / 2;

        for (int i = -radius; i <= radius; i++)
        {
            for (int j = -radius; j <= radius; j++)
            {
                int px = x + i;
                int py = y + j;

                if (px >= 0 && px < tex.width && py >= 0 && py < tex.height)
                {
                    tex.SetPixel(px, py, color);
                }
            }
        }
    }

    private Texture2D CropSquareWithPadding(Texture2D source, int centerX, int centerY, int size)
    {
        Texture2D cropped = new Texture2D(size, size, TextureFormat.RGBA32, false);

        Color32[] sourcePixels = source.GetPixels32();
        Color32[] cropPixels = new Color32[size * size];

        int startX = centerX - size / 2;
        int startY = centerY - size / 2;

        Color32 transparent = new Color32(0, 0, 0, 0);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int sourceX = startX + x;
                int sourceY = startY + y;

                int cropIndex = y * size + x;

                if (sourceX >= 0 && sourceX < source.width && sourceY >= 0 && sourceY < source.height)
                {
                    int sourceIndex = sourceY * source.width + sourceX;
                    cropPixels[cropIndex] = sourcePixels[sourceIndex];
                }
                else
                {
                    cropPixels[cropIndex] = transparent;
                }
            }
        }

        cropped.SetPixels32(cropPixels);
        cropped.Apply();

        return cropped;
    }

    void OnDestroy()
    {
        _faceLandmarker?.Close();
    }
}
