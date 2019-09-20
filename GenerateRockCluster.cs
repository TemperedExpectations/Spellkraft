using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GenerateRockCluster : MonoBehaviour {

    [System.Serializable]
    public struct VertexRow {
        public Vector2Int points;
        public Vector2 height;
        public Vector2 radius;
    }

    public bool genAtStart;
    public bool autoUpdate;
    public bool randomizeSeed;
    public int seed;
    public Vector2Int genCount;
    public Vector2 genRadius;
    public Vector2 angleOffset;
    public Vector2 shearStrength;
    public VertexRow[] vertexRows;

    MeshFilter filter;
    float toGen;

    private void Start() {
        if (genAtStart) {
            if (randomizeSeed) seed = Random.Range(-999, 1000);
            Random.State state = Random.state;
            GenCluster(seed);
            Random.state = state;
        }
    }

    public void GenCluster(int seed) {
        if (genCount.x <= 0 || genCount.y <= 0) return;

        Random.State state = Random.state;
        Random.InitState(seed);
        Mesh mesh = new Mesh();
        toGen = GetRandom(genCount);
        filter = GetComponent<MeshFilter>();

        if (toGen == 1) {
             GenSingle(mesh, Vector3.zero);
        }
        else {
            for (int i = 0; i < toGen; i++) {
                Vector3 offset = Quaternion.AngleAxis(360 * (i / toGen) + GetRandom(angleOffset), Vector3.up) * Vector3.forward * GetRandom(genRadius);
                GenSingle(mesh, offset, i);
            }
        }

        Random.state = state;

        mesh.RecalculateNormals();

        filter.sharedMesh = mesh;
    }

    void GenSingle(Mesh mesh, Vector3 offset, int count = 0) {
        if (vertexRows == null || vertexRows.Length < 2) return;

        List<Vector3[]> rows = new List<Vector3[]>();
        Vector2 angleRange = new Vector2(360f * count / toGen - 180f * 1f / toGen, 360f * count / toGen + 180f * 1f / toGen);
        Quaternion shear = Quaternion.AngleAxis(GetRandom(angleRange), Vector3.up) * Quaternion.AngleAxis(GetRandom(shearStrength), Vector3.right);

        foreach (VertexRow row in vertexRows) {
            int numPoints = GetRandom(row.points);
            Vector3[] verts = new Vector3[numPoints];

            for (int j = 0; j < numPoints; j++) {
                verts[j] = Quaternion.AngleAxis(360 * ((float)j / numPoints), Vector3.up) * Vector3.forward * GetRandom(row.radius) + Vector3.up * GetRandom(row.height);
            }

            rows.Add(verts);
        }

        List<Vector3> meshVerts = new List<Vector3>(mesh.vertices);
        List<int> meshTris = new List<int>(mesh.triangles);
        int vertStart = meshVerts.Count;

        for (int i = 0; i < rows.Count - 1; i++) {
            if (rows[i].Length == 0 || rows[i + 1].Length == 0) return;

            float increment = rows[i].Length > rows[i + 1].Length ? 1f / rows[i].Length : (1f / rows[i + 1].Length);

            if (increment <= 0) return;

            for (float p = 0; p < 1; p += increment) {
                int t1bot = Mathf.RoundToInt(p * rows[i].Length) % rows[i].Length;
                int t2bot = Mathf.RoundToInt((p + increment) * rows[i].Length) % rows[i].Length;

                int t1top = Mathf.RoundToInt(p * rows[i + 1].Length) % rows[i + 1].Length;
                int t2top = Mathf.RoundToInt((p + increment) * rows[i + 1].Length) % rows[i + 1].Length;

                int vertCount = meshVerts.Count;

                if (t1bot == t2bot) {
                    meshVerts.Add(rows[i][t1bot]);
                    meshVerts.Add(rows[i + 1][t1top]);
                    meshVerts.Add(rows[i + 1][t2top]);
                    meshTris.AddRange(new int[] { vertCount + 1, vertCount, vertCount + 2 });
                }
                else if (t1top == t2top) {
                    meshVerts.Add(rows[i][t1bot]);
                    meshVerts.Add(rows[i][t2bot]);
                    meshVerts.Add(rows[i + 1][t1top]);
                    meshTris.AddRange(new int[] { vertCount, vertCount + 1, vertCount + 2 });
                }
                else {
                    meshVerts.Add(rows[i][t1bot]);
                    meshVerts.Add(rows[i][t2bot]);
                    meshVerts.Add(rows[i + 1][t1top]);
                    meshVerts.Add(rows[i + 1][t2top]);
                    if (Vector3.Distance(meshVerts[vertCount], meshVerts[vertCount + 2]) < Vector3.Distance(meshVerts[vertCount + 1], meshVerts[vertCount + 3])) {
                        meshTris.AddRange(new int[] { vertCount, vertCount + 1, vertCount + 2, vertCount + 2, vertCount + 1, vertCount + 3 });
                    }
                    else {
                        meshTris.AddRange(new int[] { vertCount, vertCount + 3, vertCount + 2, vertCount, vertCount + 1, vertCount + 3 });
                    }
                }
            }
        }

        for (int i = vertStart; i < meshVerts.Count; i++) {
            meshVerts[i] += shear * Vector3.up * meshVerts[i].y - Vector3.up * meshVerts[i].y + offset;
        }

        mesh.vertices = meshVerts.ToArray();
        mesh.triangles = meshTris.ToArray();
    }

    int GetRandom(Vector2Int range) {
        return range.x == range.y ? range.x : (Mathf.Min(range.x, range.y) + Mathf.RoundToInt(Random.value * (Mathf.Max(range.x, range.y) - Mathf.Min(range.x, range.y))));
    }

    float GetRandom(Vector2 range) {
        return range.x == range.y ? range.x : (Mathf.Min(range.x, range.y) + Random.value * (Mathf.Max(range.x, range.y) - Mathf.Min(range.x, range.y)));
    }
    
    private void OnValidate() {
        if (autoUpdate) {
            if (filter == null) filter = GetComponent<MeshFilter>();

            if (randomizeSeed) seed = Random.Range(-999, 1000);
            Random.State state = Random.state;
            GenCluster(seed);
            Random.state = state;
        }
    }
}
