using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubes {

    [ExecuteInEditMode, RequireComponent(typeof(MinorMeshModifier), typeof(CreateNoise))]
    public class MeshGenerator : MonoBehaviour {

        const int threadGroupSize = 8;
        const int numLevels = 64;

        public CreateNoise noiseSettings;
        public MinorMeshModifier meshMod;

        [Header("Chunk Settings")]
        public Vector3Int numChunks = Vector3Int.one;
        public float chunkSize;
        public int pointsPerChunk;
        public AnimationCurve weightBounds;

        [Header("Mesh Settings")]
        public bool genMesh;
        public bool useCompute;
        public ComputeShader meshCompute;
        public Material mat;
        public bool generateColliders;
        public bool stitchEdges;
        public bool recalculateNormals;

        [Header("Gizmos")]
        public bool showBoundsGizmo = true;
        public Color boundsGizmoCol = Color.white;
        public bool showWeights;

        GameObject chunkHolder;
        const string chunkHolderName = "Chunks Holder";
        List<Chunk> chunks;
        Dictionary<Vector3Int, Chunk> existingChunks;
        Queue<Chunk> recycleableChunks;

        // Buffers
        ComputeBuffer triangleBuffer;
        ComputeBuffer pointsBuffer;
        ComputeBuffer triCountBuffer;
        ComputeBuffer levelsBuffer;

        [Space()]
        public bool settingsUpdated;

        public Vector3Int[] Coords {
            get {
                Vector3Int[] c = new Vector3Int[chunks.Count];
                for (int i = 0; i < chunks.Count; i++) {
                    c[i] = chunks[i].coord;
                }
                return c;
            }
        }

        void OnEnable() {
            if (noiseSettings != null) {
                noiseSettings.OnUpdated += SetUpdate;
            }
            if (meshMod != null) {
                meshMod.OnUpdated += SetUpdate;
            }
        }

        void Update() {
            if (settingsUpdated) {
                settingsUpdated = false;
                RequestMeshUpdate();
            }
        }

        public void SetUpdate() {
            settingsUpdated = true;
        }

        public void RequestMeshUpdate() {
            Run();
        }

        public void Run() {
            if (pointsPerChunk > 1) {
                CreateBuffers();
                if (meshMod) meshMod.SetChunkInfo(numChunks, chunkSize, pointsPerChunk);

                InitChunks();
                UpdateAllChunks();

                // Release buffers immediately in editor
                if (!Application.isPlaying) {
                    ReleaseBuffers();
                }
            }
        }

        void InitChunks() {
            CreateChunkHolder();
            chunks = new List<Chunk>();
            List<Chunk> oldChunks = new List<Chunk>(FindObjectsOfType<Chunk>());

            // Go through all coords and create a chunk there if one doesn't already exist
            for (int x = 0; x < numChunks.x; x++) {
                for (int y = 0; y < numChunks.y; y++) {
                    for (int z = 0; z < numChunks.z; z++) {
                        Vector3Int coord = new Vector3Int(x, y, z);
                        bool chunkAlreadyExists = false;

                        // If chunk already exists, add it to the chunks list, and remove from the old list.
                        for (int i = 0; i < oldChunks.Count; i++) {
                            if (oldChunks[i].coord == coord) {
                                chunks.Add(oldChunks[i]);
                                oldChunks.RemoveAt(i);
                                chunkAlreadyExists = true;
                                break;
                            }
                        }

                        // Create new chunk
                        if (!chunkAlreadyExists) {
                            var newChunk = CreateChunk(coord);
                            chunks.Add(newChunk);
                        }

                        chunks[chunks.Count - 1].SetUp(mat, generateColliders, pointsPerChunk);
                    }
                }
            }

            // Delete all unused chunks
            for (int i = 0; i < oldChunks.Count; i++) {
                oldChunks[i].DestroyOrDisable();
            }
        }

        void UpdateAllChunks() {
            if (noiseSettings == null) {
                Debug.Log("Noise Settings Required");
                return;
            }

            noiseSettings.GenerateNoise(Coords, numChunks, chunkSize, pointsPerChunk);

            // Create mesh for each chunk
            foreach (Chunk chunk in chunks) {
                UpdateChunkMesh(chunk);
            }

            if (stitchEdges && meshMod) {
                meshMod.StitchEdges();
                foreach (Chunk chunk in chunks) {
                    meshMod.ApplyEdgeUpdates(chunk.mesh, chunk.coord);
                }
            }
        }

        public void UpdateChunkMesh(Chunk chunk) {
            int numVoxelsPerChunk = pointsPerChunk - 1;
            int numThreadsPerAxis = Mathf.CeilToInt(numVoxelsPerChunk / (float)threadGroupSize);
            float pointSpacing = chunkSize / numVoxelsPerChunk;
            int maxTriangleCount = (numVoxelsPerChunk * numVoxelsPerChunk * numVoxelsPerChunk) * 5;

            Vector3Int coord = chunk.coord;
            Vector3 centre = CentreFromCoord(coord);

            Vector3 worldBounds = new Vector3(numChunks.x, numChunks.y, numChunks.z) * chunkSize;

            chunk.UpdateWeights(noiseSettings);

            if (genMesh) {
                List<Triangle> triangles = ListPool<Triangle>.Get();
                int numTris;

                if (useCompute && meshCompute != null) {
                    pointsBuffer.SetData(chunk.pointList);
                    triangleBuffer.SetCounterValue(0);
                    meshCompute.SetBuffer(0, "points", pointsBuffer);
                    meshCompute.SetBuffer(0, "triangles", triangleBuffer);
                    meshCompute.SetInt("numPointsPerAxis", pointsPerChunk);

                    float height = numChunks.y * .5f * chunkSize;
                    float yMin = PositionFromCoord(chunk.coord, new Vector3(0, 0, 0)).y;
                    float yMax = PositionFromCoord(chunk.coord, new Vector3(0, pointsPerChunk, 0)).y;
                    float eMin = Mathf.InverseLerp(-height, height, yMin);
                    float eMax = Mathf.InverseLerp(-height, height, yMax);
                    
                    float[] levels = new float[numLevels];
                    //print($"Chunk: {chunk.coord}");
                    for (int e = 0; e < numLevels; e++) {
                        levels[e] = weightBounds.Evaluate(Mathf.Lerp(eMin, eMax, e / (numLevels - 1f)));
                        //print($"{e}: {levels[e]}");
                    }
                    levelsBuffer.SetData(levels);
                    meshCompute.SetBuffer(0, "weightLevels", levelsBuffer);

                    meshCompute.SetFloat("yMin", yMin);
                    meshCompute.SetFloat("yMax", yMax);

                    meshCompute.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);

                    // Get number of triangles in the triangle buffer
                    ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
                    int[] triCountArray = { 0 };
                    triCountBuffer.GetData(triCountArray);
                    numTris = triCountArray[0];

                    // Get triangle data from shader
                    Triangle[] tris = new Triangle[numTris];
                    triangleBuffer.GetData(tris, 0, 0, numTris);
                    triangles.AddRange(tris);
                }
                else {
                    for (int x = 0; x < numVoxelsPerChunk; x++) {
                        for (int y = 0; y < numVoxelsPerChunk; y++) {
                            for (int z = 0; z < numVoxelsPerChunk; z++) {
                                List<Triangle> tris = March(CubeFromCoord(chunk.coord, x, y, z), chunk[x, y, z]);
                                triangles.AddRange(tris);
                                ListPool<Triangle>.Add(tris);
                            }
                        }
                    }

                    numTris = triangles.Count;
                }

                if (meshMod != null)
                    meshMod.GetMesh(chunk.mesh, triangles, chunk.coord);
            }
        }

        List<Triangle> March(Vector3[] positions, float[] weights) {
            if (weights.Length != 8) {
                Debug.LogError("March needs to have 8 inputs");
                return null;
            }

            // Calculate unique index for each cube configuration.
            // There are 256 possible values
            // A value of 0 means cube is entirely inside surface; 255 entirely outside.
            // The value is used to look up the edge table, which indicates which edges of the cube are cut by the isosurface.
            float yMax = numChunks.y * .5f * chunkSize;
            int cubeIndex = 0;
            if (weights[0] < weightBounds.Evaluate(Mathf.InverseLerp(-yMax, yMax, positions[0].y))) cubeIndex |= 1;
            if (weights[1] < weightBounds.Evaluate(Mathf.InverseLerp(-yMax, yMax, positions[1].y))) cubeIndex |= 2;
            if (weights[2] < weightBounds.Evaluate(Mathf.InverseLerp(-yMax, yMax, positions[2].y))) cubeIndex |= 4;
            if (weights[3] < weightBounds.Evaluate(Mathf.InverseLerp(-yMax, yMax, positions[3].y))) cubeIndex |= 8;
            if (weights[4] < weightBounds.Evaluate(Mathf.InverseLerp(-yMax, yMax, positions[4].y))) cubeIndex |= 16;
            if (weights[5] < weightBounds.Evaluate(Mathf.InverseLerp(-yMax, yMax, positions[5].y))) cubeIndex |= 32;
            if (weights[6] < weightBounds.Evaluate(Mathf.InverseLerp(-yMax, yMax, positions[6].y))) cubeIndex |= 64;
            if (weights[7] < weightBounds.Evaluate(Mathf.InverseLerp(-yMax, yMax, positions[7].y))) cubeIndex |= 128;

            List<Triangle> triangles = ListPool<Triangle>.Get();

            // Create triangles for current cube configuration
            for (int i = 0; MarchTables.triangulation[cubeIndex, i] != -1; i += 3) {
                // Get indices of corner points A and B for each of the three edges
                // of the cube that need to be joined to form the triangle.
                int a0 = MarchTables.cornerIndexAFromEdge[MarchTables.triangulation[cubeIndex, i]];
                int b0 = MarchTables.cornerIndexBFromEdge[MarchTables.triangulation[cubeIndex, i]];

                int a1 = MarchTables.cornerIndexAFromEdge[MarchTables.triangulation[cubeIndex, i + 1]];
                int b1 = MarchTables.cornerIndexBFromEdge[MarchTables.triangulation[cubeIndex, i + 1]];

                int a2 = MarchTables.cornerIndexAFromEdge[MarchTables.triangulation[cubeIndex, i + 2]];
                int b2 = MarchTables.cornerIndexBFromEdge[MarchTables.triangulation[cubeIndex, i + 2]];

                Triangle tri;
                tri.a = InterpolateVerts(positions[a0], weights[a0], weightBounds.Evaluate(Mathf.InverseLerp(-yMax, yMax, positions[a0].y)), positions[b0], weights[b0], weightBounds.Evaluate(Mathf.InverseLerp(-yMax, yMax, positions[b0].y)));
                tri.b = InterpolateVerts(positions[a1], weights[a1], weightBounds.Evaluate(Mathf.InverseLerp(-yMax, yMax, positions[a1].y)), positions[b1], weights[b1], weightBounds.Evaluate(Mathf.InverseLerp(-yMax, yMax, positions[b1].y)));
                tri.c = InterpolateVerts(positions[a2], weights[a2], weightBounds.Evaluate(Mathf.InverseLerp(-yMax, yMax, positions[a2].y)), positions[b2], weights[b2], weightBounds.Evaluate(Mathf.InverseLerp(-yMax, yMax, positions[b2].y)));
                tri.n = Vector3.Cross(tri.c - tri.a, tri.b - tri.a).normalized;
                triangles.Add(tri);
            }

            return triangles;
        }

        Vector3 InterpolateVerts(Vector3 v1, float w1, float i1, Vector3 v2, float w2, float i2) {
            float iso = i1;//(i1 + i2) * .5f;
            float t = (iso - w1) / (w2 - w1);
            return Vector3.Lerp(v1, v2, t);
        }

        Chunk CreateChunk(Vector3Int coord) {
            GameObject chunk = new GameObject($"Chunk ({coord.x}, {coord.y}, {coord.z})");
            chunk.transform.parent = chunkHolder.transform;
            Chunk newChunk = chunk.AddComponent<Chunk>();
            newChunk.coord = coord;
            return newChunk;
        }

        bool CompareVerts(Vector3 vert1, Vector3 vert2) {
            return vert1.x == vert2.x && vert1.y == vert2.y && vert1.z == vert2.z;
        }

        void InitVariableChunkStructures() {
            recycleableChunks = new Queue<Chunk>();
            chunks = new List<Chunk>();
            existingChunks = new Dictionary<Vector3Int, Chunk>();
        }

        void CreateChunkHolder() {
            // Create/find mesh holder object for organizing chunks under in the hierarchy
            if (chunkHolder == null) {
                if (GameObject.Find(chunkHolderName)) {
                    chunkHolder = GameObject.Find(chunkHolderName);
                }
                else {
                    chunkHolder = new GameObject(chunkHolderName);
                    chunkHolder.transform.SetParent(transform);
                }
            }
        }

        Vector3 CentreFromCoord(Vector3Int coord) {
            // Centre entire map at origin
            Vector3 totalBounds = (Vector3)numChunks * chunkSize;
            return -totalBounds / 2 + (Vector3)coord * chunkSize + Vector3.one * chunkSize / 2;
        }

        Vector3 PositionFromCoord(Vector3Int coord, Vector3 point) {
            Vector3 offset = point / (pointsPerChunk - 1) - Vector3.one * .5f;

            return CentreFromCoord(coord) + offset * chunkSize;
        }

        Vector3[] CubeFromCoord(Vector3Int coord, int x, int y, int z) {
            return new Vector3[] {
                            PositionFromCoord(coord, new Vector3(x, y, z)),
                            PositionFromCoord(coord, new Vector3(x + 1, y, z)),
                            PositionFromCoord(coord, new Vector3(x + 1, y, z + 1)),
                            PositionFromCoord(coord, new Vector3(x, y, z + 1)),
                            PositionFromCoord(coord, new Vector3(x, y + 1, z)),
                            PositionFromCoord(coord, new Vector3(x + 1, y + 1, z)),
                            PositionFromCoord(coord, new Vector3(x + 1, y + 1, z + 1)),
                            PositionFromCoord(coord, new Vector3(x, y + 1, z + 1))
                        };
        }

        void OnDestroy() {
            if (Application.isPlaying) {
                ReleaseBuffers();
            }
        }

        void CreateBuffers() {
            int numPoints = pointsPerChunk * pointsPerChunk * pointsPerChunk;
            int numVoxelsPerAxis = pointsPerChunk - 1;
            int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
            int maxTriangleCount = numVoxels * 5;

            // Always create buffers in editor (since buffers are released immediately to prevent memory leak)
            // Otherwise, only create if null or if size has changed
            if (!Application.isPlaying || (pointsBuffer == null || numPoints != pointsBuffer.count)) {
                if (Application.isPlaying) {
                    ReleaseBuffers();
                }
                triangleBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 3 * 4, ComputeBufferType.Append);
                pointsBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);
                triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
                levelsBuffer = new ComputeBuffer(numLevels, sizeof(float));
            }

            meshMod.CreateBuffers(pointsPerChunk);
        }

        void ReleaseBuffers() {
            if (triangleBuffer != null) {
                triangleBuffer.Release();
                pointsBuffer.Release();
                triCountBuffer.Release();
                levelsBuffer.Release();
            }
            meshMod.ReleaseBuffers();
        }

        void OnValidate() {
            settingsUpdated = true;
        }

        public struct Triangle {
#pragma warning disable 649 // disable unassigned variable warning
            public Vector3 a;
            public Vector3 b;
            public Vector3 c;
            public Vector3 n;

            public Vector3 this[int i] {
                get {
                    switch (i) {
                        case 0:
                            return a;
                        case 1:
                            return b;
                        case 2:
                            return c;
                        case 3:
                            return n;
                        case 4:
                            return (a + b) * .5f;
                        case 5:
                            return (b + c) * .5f;
                        case 6:
                            return (c + a) * .5f;
                        default:
                            return n;
                    }
                }
            }
        }

        public struct TriangleN {
#pragma warning disable 649 // disable unassigned variable warning
            public Vector3 a;
            public Vector3 b;
            public Vector3 c;
            public Vector3 na;
            public Vector3 nb;
            public Vector3 nc;

            public Vector3 this[int i] {
                get {
                    switch (i) {
                        case 0:
                            return a;
                        case 1:
                            return b;
                        case 2:
                            return c;
                        case 3:
                            return na;
                        case 4:
                            return nb;
                        default:
                            return nc;
                    }
                }
            }
        }

        void OnDrawGizmos() {
            if (showBoundsGizmo || showWeights) {
                Gizmos.color = boundsGizmoCol;

                List<Chunk> chunks = this.chunks ?? new List<Chunk>(FindObjectsOfType<Chunk>());
                foreach (var chunk in chunks) {
                    if (showBoundsGizmo) {
                        Bounds bounds = new Bounds(CentreFromCoord(chunk.coord), Vector3.one * chunkSize);
                        Gizmos.color = boundsGizmoCol;
                        Gizmos.DrawWireCube(CentreFromCoord(chunk.coord), Vector3.one * chunkSize);
                    }
                    if (showWeights) {

                        for (int x = 0; x < pointsPerChunk; x++) {
                            for (int y = 0; y < pointsPerChunk; y++) {
                                for (int z = 0; z < pointsPerChunk; z++) {
                                    Gizmos.color = Color.white * chunk.pointList[x * pointsPerChunk * pointsPerChunk + y * pointsPerChunk + z].w + Color.black;
                                    Gizmos.DrawSphere(PositionFromCoord(chunk.coord, new Vector3(x, y, z)), chunkSize / pointsPerChunk * .1f);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
