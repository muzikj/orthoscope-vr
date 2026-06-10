using UnityEngine;
using System.IO;

using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
 
public class Intrapupilar : MonoBehaviour
{
    [Tooltip("The trained model as raw binary data, i.e. 'face_landmarker.bytes'!")]
    [SerializeField] private TextAsset modelAsset;

    [Tooltip("Image Texture2D with Read/Write enabled.")]
    [SerializeField] private Texture2D inputTexture;

    private FaceLandmarker _faceLandmarker;

    // Iris center landmarks
    // MediaPipe iris landmarks:
    // Right iris center = 468
    // Left iris center  = 473
    private const int RIGHT_IRIS_CENTER_INDEX = 468;
    private const int LEFT_IRIS_CENTER_INDEX = 473;

    // Mouth corner landmarks
    private const int LEFT_LIP_CORNER_INDEX = 61;
    private const int RIGHT_LIP_CORNER_INDEX = 291;

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

            if (firstFace.landmarks == null || firstFace.landmarks.Count <= RIGHT_LIP_CORNER_INDEX)
            {
                Debug.LogWarning("Not enough landmarks detected!");
                Destroy(displayTexture);
                return;
            }

            Vector2Int leftIris = NormalizedToPixel(
                firstFace.landmarks[LEFT_IRIS_CENTER_INDEX].x,
                firstFace.landmarks[LEFT_IRIS_CENTER_INDEX].y,
                width,
                height
            );

            Vector2Int rightIris = NormalizedToPixel(
                firstFace.landmarks[RIGHT_IRIS_CENTER_INDEX].x,
                firstFace.landmarks[RIGHT_IRIS_CENTER_INDEX].y,
                width,
                height
            );

            Vector2Int leftLipCorner = NormalizedToPixel(
                firstFace.landmarks[LEFT_LIP_CORNER_INDEX].x,
                firstFace.landmarks[LEFT_LIP_CORNER_INDEX].y,
                width,
                height
            );

            Vector2Int rightLipCorner = NormalizedToPixel(
                firstFace.landmarks[RIGHT_LIP_CORNER_INDEX].x,
                firstFace.landmarks[RIGHT_LIP_CORNER_INDEX].y,
                width,
                height
            );

            // Draw intrapupillary line
            DrawLine(displayTexture, leftIris.x, leftIris.y, rightIris.x, rightIris.y, Color.cyan, 3);

            // Draw lip line
            DrawLine(displayTexture, leftLipCorner.x, leftLipCorner.y, rightLipCorner.x, rightLipCorner.y, Color.yellow, 3);

            // Draw points
            DrawPoint(displayTexture, leftIris.x, leftIris.y, Color.red, 11);
            DrawPoint(displayTexture, rightIris.x, rightIris.y, Color.red, 11);
            DrawPoint(displayTexture, leftLipCorner.x, leftLipCorner.y, Color.magenta, 11);
            DrawPoint(displayTexture, rightLipCorner.x, rightLipCorner.y, Color.magenta, 11);

            // Calculate line angles
            float interpupillaryAngle = GetLineAngleDegrees(leftIris, rightIris);
            float lipLineAngle = GetLineAngleDegrees(leftLipCorner, rightLipCorner);

            // Deviation from parallel
            float deviationAngle = GetParallelDeviationDegrees(interpupillaryAngle, lipLineAngle);

            Debug.Log("Left iris: " + leftIris);
            Debug.Log("Right iris: " + rightIris);
            Debug.Log("Left lip corner: " + leftLipCorner);
            Debug.Log("Right lip corner: " + rightLipCorner);

            Debug.Log("Interpupillary line angle: " + interpupillaryAngle + " degrees");
            Debug.Log("Lip line angle: " + lipLineAngle + " degrees");
            Debug.Log("Deviation from parallel: " + deviationAngle + " degrees");

            displayTexture.Apply();

            string outputPath = Application.dataPath + "/iris_lip_line_result.png";
            File.WriteAllBytes(outputPath, displayTexture.EncodeToPNG());

            Debug.Log("Saved output image to: " + outputPath);
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

    private float GetLineAngleDegrees(Vector2Int a, Vector2Int b)
    {
        float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;

        // Normalize to 0..180 because line direction does not matter
        if (angle < 0f)
        {
            angle += 180f;
        }

        return angle;
    }

    private float GetParallelDeviationDegrees(float angleA, float angleB)
    {
        float difference = Mathf.Abs(angleA - angleB);

        if (difference > 90f)
        {
            difference = 180f - difference;
        }

        return difference;
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

    void OnDestroy()
    {
        _faceLandmarker?.Close();
    }
}