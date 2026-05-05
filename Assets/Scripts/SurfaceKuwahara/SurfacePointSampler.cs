using UnityEngine;

namespace SurfaceKuwahara
{
    [RequireComponent(typeof(MeshFilter))]
    public class SurfacePointSampler : MonoBehaviour
    {
        [SerializeField] private int sampleCount = 256;
        [SerializeField] private int randomSeed = 12345;
        [SerializeField] private float pointSize = 0.05f;
        [SerializeField] private Material markerMaterial;

        private const string MarkerContainerName = "Surface Point Samples";

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
                Vector3 worldPoint = transform.TransformPoint(localPoint);

                CreateMarker(container, worldPoint, i);
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

        private void CreateMarker(Transform container, Vector3 worldPoint, int index)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"Surface Sample {index:0000}";
            marker.transform.SetParent(container, true);
            marker.transform.position = worldPoint;
            marker.transform.localScale = Vector3.one * pointSize;

            if (markerMaterial != null)
            {
                Renderer renderer = marker.GetComponent<Renderer>();
                renderer.sharedMaterial = markerMaterial;
            }
        }
    }
}
