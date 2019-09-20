using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubes {

    public class Chunk : MonoBehaviour {

        public Vector3Int coord;

        [HideInInspector]
        public Mesh mesh;
        [HideInInspector]
        public Vector4[] pointList;

        int numPoints;
        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        MeshCollider meshCollider;
        bool generateCollider;

        public float[] this[int x, int y, int z] {
            get {
                return new float[]{
                            pointList[x * numPoints * numPoints + y * numPoints + z].w,
                            pointList[(x + 1) * numPoints * numPoints + y * numPoints + z].w,
                            pointList[(x + 1) * numPoints * numPoints + y * numPoints + z + 1].w,
                            pointList[x * numPoints * numPoints + y * numPoints + z + 1].w,
                            pointList[x * numPoints * numPoints + (y + 1) * numPoints + z].w,
                            pointList[(x + 1) * numPoints * numPoints + (y + 1) * numPoints + z].w,
                            pointList[(x + 1) * numPoints * numPoints + (y + 1) * numPoints + z + 1].w,
                            pointList[x * numPoints * numPoints + (y + 1) * numPoints + z + 1].w
                        };
            }
        }

        public void DestroyOrDisable() {
            if (Application.isPlaying) {
                mesh.Clear();
                pointList = null;
                gameObject.SetActive(false);
            }
            else {
                DestroyImmediate(gameObject, false);
            }
        }

        // Add components/get references in case lost (references can be lost when working in the editor)
        public void SetUp(Material mat, bool generateCollider, int size) {
            this.generateCollider = generateCollider;

            numPoints = size;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();

            if (meshFilter == null) {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            if (meshRenderer == null) {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            if (meshCollider == null && generateCollider) {
                meshCollider = gameObject.AddComponent<MeshCollider>();
            }
            if (meshCollider != null && !generateCollider) {
                DestroyImmediate(meshCollider);
            }

            mesh = meshFilter.sharedMesh;
            if (mesh == null) {
                mesh = new Mesh {
                    indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
                };
                meshFilter.sharedMesh = mesh;
            }

            if (generateCollider) {
                if (meshCollider.sharedMesh == null) {
                    meshCollider.sharedMesh = mesh;
                }
                // force update
                meshCollider.enabled = false;
                meshCollider.enabled = true;
            }

            meshRenderer.material = mat;
        }

        public void UpdateWeights(CreateNoise noiseSettings) {
            pointList = noiseSettings.GetNoise(coord);
        }
    }
}
