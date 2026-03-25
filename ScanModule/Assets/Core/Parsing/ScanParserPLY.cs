using UnityEngine;
using System.IO;

using System.Text;
using System.Collections.Generic;

public class ScanParserPLY : ScanParser
{
    protected override MeshData ParseScan(string path)
    {
        using FileStream fs = new(path, FileMode.Open, FileAccess.Read);
        using BinaryReader br = new(fs);

        int vertexCount = 0, faceCount = 0, vertexStride = 0; // the stride is the total size of the entire data block in bytes (for color, position, unused normals...)
        int rOffset = -1, gOffset = -1, bOffset = -1, aOffset = -1;

        // parse header for vertex color attributes and other properties we might need, like position, and those we do not want, like normals, confidence
        string line;
        while ((line = fs.ReadLine()) != "end_header")
        {
            if (line.StartsWith("format") && !line.Contains("binary_little_endian"))
            {
                Debug.LogError("Only binary Little Endian encoding of .ply files has been implemented.");
                return new MeshData();
            }

            string[] tokens = line.Split(' ');
            if (tokens.Length == 0) continue;

            if (tokens[0] == "element")
            {
                if (tokens[1] == "vertex") vertexCount = int.Parse(tokens[2]);
                else if (tokens[1] == "face") faceCount = int.Parse(tokens[2]);
            }
            // the vertex element is defined before the face element, hence vertexCount > 0, but faceCount == 0
            else if (tokens[0] == "property" && vertexCount > 0 && faceCount == 0)
            {
                // add up the stride for every wanted and unwanted non-color property
                if (tokens[1] == "float" || tokens[1] == "float32")
                {
                    vertexStride += 4;
                }
                // if we are working with rgb(a), set the right order for all channels
                else if (tokens[1] == "uchar" || tokens[1] == "uint8")
                {
                         if (tokens[2] == "red")    rOffset = vertexStride;
                    else if (tokens[2] == "green")  gOffset = vertexStride;
                    else if (tokens[2] == "blue")   bOffset = vertexStride;
                    else if (tokens[2] == "alpha")  aOffset = vertexStride;

                    vertexStride++;
                }
            }
        }

        // read the actual data I - vertices, vertex colors
        Vector3[] vertices = new Vector3[vertexCount];
        Color32[] colors = (rOffset != -1 && gOffset != -1 && bOffset != -1) ? new Color32[vertexCount] : null;

        for (int i = 0; i < vertexCount; i++)
        {
            long startPosition = fs.Position;

            // read the position (always the initial 12 B)
            float vertexX = br.ReadSingle();
            float vertexY = br.ReadSingle();
            float vertexZ = br.ReadSingle();

            // .ply is Z-UP, but Unity is Y-UP, so we need to swap the Y and Z values
            vertices[i] = new Vector3(vertexX, vertexZ, vertexY);

            // if we have rgb
            if (colors != null)
            {
                byte r = 255, g = 255, b = 255, a = 255;

                fs.Position = startPosition + rOffset;
                r = br.ReadByte();

                fs.Position = startPosition + gOffset;
                g = br.ReadByte();

                fs.Position = startPosition + bOffset;
                b = br.ReadByte();

                // we are not guaranteed the alpha channel, so check for it
                if (aOffset != -1)
                {
                    fs.Position = startPosition + aOffset;
                    a = br.ReadByte();
                }

                colors[i] = new Color32(r, g, b, a);
            }

            // jump to the end of the vertex block
            fs.Position = startPosition + vertexStride;
        }

        // read the actual data II - faces
        int[] triangles = new int[faceCount * 3];

        for (int i = 0; i < faceCount; i++)
        {
            byte points = br.ReadByte();
            // if we have 3 points, we have a triangle
            if (points == 3)
            {
                // we have to flip the Y and Z, once again, hence 0-2-1 order, instead of 0-1-2
                int vertex1 = br.ReadInt32();
                triangles[i * 3 + 0] = vertex1;
                
                int vertex2 = br.ReadInt32();
                triangles[i * 3 + 2] = vertex2;

                int vertex3 = br.ReadInt32();
                triangles[i * 3 + 1] = vertex3;
            }
            // not a triangle, so skip all vertices of this n-gon (all are 32 bits, hence 32/8 = 4 Bytes)
            else
            {
                fs.Position += points * 4;
            }    
        }

        return new MeshData
        {
            vertices = vertices,
            triangles = triangles,
            colors = colors,
        };
    }
}

public static class FileStreamExtensions
{
    public static string ReadLine(this FileStream fs)
    {
        List<byte> bytes = new();

        int b;
        while ((b = fs.ReadByte()) != -1)
        {
            if (b == '\n') break;
            if (b != '\r') bytes.Add((byte)b);
        }

        return Encoding.ASCII.GetString(bytes.ToArray());
    }
}
