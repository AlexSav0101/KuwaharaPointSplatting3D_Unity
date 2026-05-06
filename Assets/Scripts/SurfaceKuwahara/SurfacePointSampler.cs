using System.Collections.Generic;
using UnityEngine;

namespace SurfaceKuwahara
{
    [RequireComponent(typeof(MeshFilter))]
    public class SurfacePointSampler : MonoBehaviour
    {
        public enum MarkerShape
        {
            Sphere,
            Disc
        }

        [System.Serializable]
        public class SurfaceSample
        {
            public Vector3 localPosition;
            public Vector3 worldPosition;
            public Vector3 localNormal;
            public Vector3 worldNormal;
            public Vector2 uv;
            public Color baseColor;
            public Color filteredColor;
            public GameObject markerObject;
        }

        [SerializeField] private int sampleCount = 256;
        [SerializeField] private int randomSeed = 12345;
        [SerializeField] private float pointSize = 0.05f;
        [SerializeField] private float surfaceOffset = 0.001f;
        [SerializeField] private MarkerShape markerShape = MarkerShape.Sphere;
        [SerializeField] private Material markerMaterial;
        [SerializeField] private List<SurfaceSample> samples = new List<SurfaceSample>();

        private const string MarkerContainerName = "Surface Point Samples";
        private const int DiscSegmentCount = 16;

        [ContextMenu("Regenerate Samples")]
        private void RegenerateSamples()
        {
            ClearSamples();

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            Mesh mesh = meshFilter.sharedMesh;

            if (mesh == null)
            {
                Debug.LogWarning("SurfacePointSampler requires a MeshFilter with a valid mesh.", this);
                return;
            }

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;

            if (vertices == null || vertices.Length == 0 || triangles == null || triangles.Length < 3)
            {
                Debug.LogWarning("SurfacePointSampler could not sample because the mesh has no triangles.", this);
                return;
            }

            float[] cumulativeTriangleAreas = BuildCumulativeTriangleAreas(vertices, triangles, out float totalArea);

            if (totalArea <= Mathf.Epsilon)
            {
                Debug.LogWarning("SurfacePointSampler could not sample because the mesh surface area is zero.", this);
                return;
            }

            Random.InitState(randomSeed);

            for (int i = 0; i < sampleCount; i++)
            {
                int triangleIndex = PickTriangleWeightedByArea(cumulativeTriangleAreas, totalArea);
                samples.Add(CreateSurfaceSample(vertices, triangles, normals, uvs, triangleIndex));
            }

            AssignBaseColors();
            CreateMarkers();
        }

        public void AssignBaseColors()
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            Material sourceMaterial = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            Color baseColor = GetMaterialColor(sourceMaterial, Color.white);

            for (int i = 0; i < samples.Count; i++)
            {
                samples[i].baseColor = baseColor;
                samples[i].filteredColor = baseColor;
            }
        }

        private void ClearSamples()
        {
            samples.Clear();

            Transform existingContainer = transform.Find(MarkerContainerName);

            if (existingContainer == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(existingContainer.gameObject);
            }
            else
            {
                DestroyImmediate(existingContainer.gameObject);
            }
        }

        private Transform CreateMarkerContainer()
        {
            GameObject container = new GameObject(MarkerContainerName);
            container.transform.SetParent(transform, false);
            return container.transform;
        }

        private static float[] BuildCumulativeTriangleAreas(Vector3[] vertices, int[] triangles, out float totalArea)
        {
            int triangleCount = triangles.Length / 3;
            float[] cumulativeAreas = new float[triangleCount];
            totalArea = 0f;

            for (int i = 0; i < triangleCount; i++)
            {
                Vector3 a = vertices[triangles[i * 3]];
                Vector3 b = vertices[triangles[i * 3 + 1]];
                Vector3 c = vertices[triangles[i * 3 + 2]];

                // A triangle's area is half the magnitude of the cross product of two of its edges.
                // Adding each area to a running total builds a cumulative distribution that lets
                // larger triangles receive proportionally more samples than smaller triangles.
                float triangleArea = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                totalArea += triangleArea;
                cumulativeAreas[i] = totalArea;
            }

            return cumulativeAreas;
        }

        private static int PickTriangleWeightedByArea(float[] cumulativeTriangleAreas, float totalArea)
        {
            float targetArea = Random.value * totalArea;

            for (int i = 0; i < cumulativeTriangleAreas.Length; i++)
            {
                if (targetArea <= cumulativeTriangleAreas[i])
                {
                    return i;
                }
            }

            return cumulativeTriangleAreas.Length - 1;
        }

        private SurfaceSample CreateSurfaceSample(
            Vector3[] vertices,
            int[] triangles,
            Vector3[] normals,
            Vector2[] uvs,
            int triangleIndex)
        {
            int vertexIndexA = triangles[triangleIndex * 3];
            int vertexIndexB = triangles[triangleIndex * 3 + 1];
            int vertexIndexC = triangles[triangleIndex * 3 + 2];

            Vector3 a = vertices[vertexIndexA];
            Vector3 b = vertices[vertexIndexB];
            Vector3 c = vertices[vertexIndexC];

            // These two random values are converted into barycentric weights.
            // Reflecting values above the diagonal keeps the distribution uniform over the full triangle.
            float u = Random.value;
            float v = Random.value;

            if (u + v > 1f)
            {
                u = 1f - u;
                v = 1f - v;
            }

            float w = 1f - u - v;
            Vector3 localPosition = a * w + b * u + c * v;
            Vector3 localNormal = CalculateSampleNormal(vertices, triangles, normals, triangleIndex, vertexIndexA, vertexIndexB, vertexIndexC, w, u, v);
            Vector2 uv = CalculateSampleUv(uvs, vertexIndexA, vertexIndexB, vertexIndexC, w, u, v);
            Vector3 worldNormal = TransformNormalToWorld(localNormal);

            return new SurfaceSample
            {
                localPosition = localPosition,
                worldPosition = transform.TransformPoint(localPosition),
                localNormal = localNormal,
                worldNormal = worldNormal,
                uv = uv,
                baseColor = Color.white,
                filteredColor = Color.white,
                markerObject = null
            };
        }

        private static Vector3 CalculateTriangleNormal(Vector3[] vertices, int[] triangles, int triangleIndex)
        {
            Vector3 a = vertices[triangles[triangleIndex * 3]];
            Vector3 b = vertices[triangles[triangleIndex * 3 + 1]];
            Vector3 c = vertices[triangles[triangleIndex * 3 + 2]];

            return Vector3.Cross(b - a, c - a).normalized;
        }

        private static Vector3 CalculateSampleNormal(
            Vector3[] vertices,
            int[] triangles,
            Vector3[] normals,
            int triangleIndex,
            int vertexIndexA,
            int vertexIndexB,
            int vertexIndexC,
            float weightA,
            float weightB,
            float weightC)
        {
            if (normals != null && normals.Length == vertices.Length)
            {
                Vector3 interpolatedNormal =
                    normals[vertexIndexA] * weightA +
                    normals[vertexIndexB] * weightB +
                    normals[vertexIndexC] * weightC;

                if (interpolatedNormal != Vector3.zero)
                {
                    return interpolatedNormal.normalized;
                }
            }

            return CalculateTriangleNormal(vertices, triangles, triangleIndex);
        }

        private static Vector2 CalculateSampleUv(
            Vector2[] uvs,
            int vertexIndexA,
            int vertexIndexB,
            int vertexIndexC,
            float weightA,
            float weightB,
            float weightC)
        {
            if (uvs != null && uvs.Length > vertexIndexA && uvs.Length > vertexIndexB && uvs.Length > vertexIndexC)
            {
                return uvs[vertexIndexA] * weightA + uvs[vertexIndexB] * weightB + uvs[vertexIndexC] * weightC;
            }

            return Vector2.zero;
        }

        private Vector3 TransformNormalToWorld(Vector3 localNormal)
        {
            // Normals should be transformed by the inverse-transpose matrix so they stay correct
            // even if the sampled object has non-uniform scale.
            Matrix4x4 normalMatrix = transform.localToWorldMatrix.inverse.transpose;
            Vector3 worldNormal = normalMatrix.MultiplyVector(localNormal).normalized;
            return worldNormal == Vector3.zero ? transform.up : worldNormal;
        }

        private void CreateMarkers()
        {
            Transform container = CreateMarkerContainer();

            // Samples are stored separately from their marker GameObjects so later filtering passes can
            // read and update surface data without depending on temporary visualization objects.
            for (int i = 0; i < samples.Count; i++)
            {
                samples[i].markerObject = CreateMarker(container, samples[i], i);
            }
        }

        private GameObject CreateMarker(Transform container, SurfaceSample sample, int index)
        {
            Vector3 normalizedNormal = sample.worldNormal.normalized;
            GameObject marker = markerShape == MarkerShape.Sphere
                ? CreateSphereMarker()
                : CreateDiscMarker();

            marker.name = $"Surface Sample {index:0000}";
            marker.transform.SetParent(container, true);

            if (markerShape == MarkerShape.Sphere)
            {
                marker.transform.position = sample.worldPosition;
                marker.transform.localScale = Vector3.one * pointSize;
            }
            else
            {
                // Disc meshes lie flat in local XZ, which makes local Vector3.up their face normal.
                // Aligning local up to the sampled surface normal orients each disc tangent to the mesh.
                marker.transform.position = sample.worldPosition + normalizedNormal * surfaceOffset;
                marker.transform.rotation = Quaternion.FromToRotation(Vector3.up, normalizedNormal);
            }

            ApplyMarkerMaterial(marker, sample.filteredColor);
            return marker;
        }

        private GameObject CreateSphereMarker()
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            return marker;
        }

        private GameObject CreateDiscMarker()
        {
            GameObject marker = new GameObject("Surface Sample Disc");
            MeshFilter meshFilter = marker.AddComponent<MeshFilter>();
            marker.AddComponent<MeshRenderer>();

            meshFilter.sharedMesh = CreateDiscMesh(pointSize * 0.5f);

            return marker;
        }

        private void ApplyMarkerMaterial(GameObject marker, Color color)
        {
            Renderer renderer = marker.GetComponent<Renderer>();

            if (renderer == null)
            {
                return;
            }

            Material sourceMaterial = markerMaterial != null ? markerMaterial : renderer.sharedMaterial;
            Material coloredMaterial = CreateMarkerMaterial(sourceMaterial);

            if (coloredMaterial == null)
            {
                return;
            }

            SetMaterialColor(coloredMaterial, color);
            renderer.sharedMaterial = coloredMaterial;
        }

        private static Material CreateMarkerMaterial(Material sourceMaterial)
        {
            if (sourceMaterial != null)
            {
                return new Material(sourceMaterial);
            }

            Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit");
            defaultShader = defaultShader != null ? defaultShader : Shader.Find("Standard");

            return defaultShader != null ? new Material(defaultShader) : null;
        }

        private static Color GetMaterialColor(Material material, Color fallback)
        {
            if (material == null)
            {
                return fallback;
            }

            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }

            return fallback;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static Mesh CreateDiscMesh(float radius)
        {
            Vector3[] vertices = new Vector3[DiscSegmentCount + 1];
            int[] triangles = new int[DiscSegmentCount * 3];

            vertices[0] = Vector3.zero;

            // The disc lies in the local XZ plane, so its local up direction is its surface normal.
            // Aligning local up to the sampled world normal makes every disc sit tangent to the mesh.
            for (int i = 0; i < DiscSegmentCount; i++)
            {
                float angle = i * Mathf.PI * 2f / DiscSegmentCount;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }

            for (int i = 0; i < DiscSegmentCount; i++)
            {
                int triangleStart = i * 3;
                triangles[triangleStart] = 0;
                triangles[triangleStart + 1] = i == DiscSegmentCount - 1 ? 1 : i + 2;
                triangles[triangleStart + 2] = i + 1;
            }

            Mesh mesh = new Mesh
            {
                name = "Surface Sample Disc Mesh",
                vertices = vertices,
                triangles = triangles
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
