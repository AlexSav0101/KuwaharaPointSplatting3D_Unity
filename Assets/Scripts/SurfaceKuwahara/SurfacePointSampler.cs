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

        [SerializeField] private int sampleCount = 256;
        [SerializeField] private int randomSeed = 12345;
        [SerializeField] private float pointSize = 0.05f;
        [SerializeField] private float surfaceOffset = 0.001f;
        [SerializeField] private MarkerShape markerShape = MarkerShape.Sphere;
        [SerializeField] private Material markerMaterial;

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

            if (vertices == null || vertices.Length == 0 || triangles == null || triangles.Length < 3)
            {
                Debug.LogWarning("SurfacePointSampler could not sample because the mesh has no triangles.", this);
                return;
            }

            Transform container = CreateMarkerContainer();
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
                Vector3 localPoint = SamplePointOnTriangle(vertices, triangles, triangleIndex);
                Vector3 localNormal = CalculateTriangleNormal(vertices, triangles, triangleIndex);
                Vector3 worldPoint = transform.TransformPoint(localPoint);
                Vector3 worldNormal = TransformNormalToWorld(localNormal);

                CreateMarker(container, worldPoint, worldNormal, i);
            }
        }

        private void ClearSamples()
        {
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

        private static Vector3 SamplePointOnTriangle(Vector3[] vertices, int[] triangles, int triangleIndex)
        {
            Vector3 a = vertices[triangles[triangleIndex * 3]];
            Vector3 b = vertices[triangles[triangleIndex * 3 + 1]];
            Vector3 c = vertices[triangles[triangleIndex * 3 + 2]];

            // These two random values are converted into barycentric weights.
            // Reflecting values above the diagonal keeps the distribution uniform over the full triangle.
            float u = Random.value;
            float v = Random.value;

            if (u + v > 1f)
            {
                u = 1f - u;
                v = 1f - v;
            }

            return a + (b - a) * u + (c - a) * v;
        }

        private static Vector3 CalculateTriangleNormal(Vector3[] vertices, int[] triangles, int triangleIndex)
        {
            Vector3 a = vertices[triangles[triangleIndex * 3]];
            Vector3 b = vertices[triangles[triangleIndex * 3 + 1]];
            Vector3 c = vertices[triangles[triangleIndex * 3 + 2]];

            return Vector3.Cross(b - a, c - a).normalized;
        }

        private Vector3 TransformNormalToWorld(Vector3 localNormal)
        {
            // Normals should be transformed by the inverse-transpose matrix so they stay correct
            // even if the sampled object has non-uniform scale.
            Matrix4x4 normalMatrix = transform.localToWorldMatrix.inverse.transpose;
            Vector3 worldNormal = normalMatrix.MultiplyVector(localNormal).normalized;
            return worldNormal == Vector3.zero ? transform.up : worldNormal;
        }

        private void CreateMarker(Transform container, Vector3 worldPoint, Vector3 worldNormal, int index)
        {
            Vector3 normalizedNormal = worldNormal.normalized;
            GameObject marker = markerShape == MarkerShape.Sphere
                ? CreateSphereMarker()
                : CreateDiscMarker();

            marker.name = $"Surface Sample {index:0000}";
            marker.transform.SetParent(container, true);

            if (markerShape == MarkerShape.Sphere)
            {
                marker.transform.position = worldPoint;
                marker.transform.localScale = Vector3.one * pointSize;
            }
            else
            {
                // Disc meshes lie flat in local XZ, which makes local Vector3.up their face normal.
                // Aligning local up to the sampled surface normal orients each disc tangent to the mesh.
                marker.transform.position = worldPoint + normalizedNormal * surfaceOffset;
                marker.transform.rotation = Quaternion.FromToRotation(Vector3.up, normalizedNormal);
            }

            ApplyMarkerMaterial(marker);
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

        private void ApplyMarkerMaterial(GameObject marker)
        {
            if (markerMaterial != null)
            {
                Renderer renderer = marker.GetComponent<Renderer>();
                renderer.sharedMaterial = markerMaterial;
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
