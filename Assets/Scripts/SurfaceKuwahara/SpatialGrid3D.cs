using System.Collections.Generic;
using UnityEngine;

namespace SurfaceKuwahara
{
    public class SpatialGrid3D
    {
        private readonly Dictionary<Vector3Int, List<SurfaceSample>> cells = new Dictionary<Vector3Int, List<SurfaceSample>>();
        private float cellSize = 0.1f;

        public float CellSize => cellSize;

        public SpatialGrid3D(float cellSize)
        {
            Initialize(cellSize);
        }

        public void Initialize(float newCellSize)
        {
            cellSize = Mathf.Max(newCellSize, 0.0001f);
            Clear();
        }

        public void Clear()
        {
            cells.Clear();
        }

        public void Insert(SurfaceSample sample)
        {
            if (sample == null)
            {
                return;
            }

            Vector3Int cell = WorldToCell(sample.worldPosition);

            if (!cells.TryGetValue(cell, out List<SurfaceSample> samplesInCell))
            {
                samplesInCell = new List<SurfaceSample>();
                cells.Add(cell, samplesInCell);
            }

            if (!samplesInCell.Contains(sample))
            {
                samplesInCell.Add(sample);
            }
        }

        public void Build(IEnumerable<SurfaceSample> samples)
        {
            Clear();

            if (samples == null)
            {
                return;
            }

            foreach (SurfaceSample sample in samples)
            {
                Insert(sample);
            }
        }

        public void QueryRadius(Vector3 worldPosition, float radius, List<SurfaceSample> results)
        {
            QueryRadius(worldPosition, radius, results, null);
        }

        public void QueryRadius(Vector3 worldPosition, float radius, List<SurfaceSample> results, SurfaceSample sampleToSkip)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();

            float clampedRadius = Mathf.Max(radius, 0f);
            float radiusSquared = clampedRadius * clampedRadius;
            Vector3 radiusVector = Vector3.one * clampedRadius;
            Vector3Int minCell = WorldToCell(worldPosition - radiusVector);
            Vector3Int maxCell = WorldToCell(worldPosition + radiusVector);
            HashSet<SurfaceSample> uniqueSamples = new HashSet<SurfaceSample>();

            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                for (int y = minCell.y; y <= maxCell.y; y++)
                {
                    for (int z = minCell.z; z <= maxCell.z; z++)
                    {
                        Vector3Int cell = new Vector3Int(x, y, z);

                        if (!cells.TryGetValue(cell, out List<SurfaceSample> samplesInCell))
                        {
                            continue;
                        }

                        for (int i = 0; i < samplesInCell.Count; i++)
                        {
                            SurfaceSample sample = samplesInCell[i];

                            if (sample == null || sample == sampleToSkip || !uniqueSamples.Add(sample))
                            {
                                continue;
                            }

                            Vector3 offset = sample.worldPosition - worldPosition;

                            if (offset.sqrMagnitude <= radiusSquared)
                            {
                                results.Add(sample);
                            }
                        }
                    }
                }
            }
        }

        private Vector3Int WorldToCell(Vector3 worldPosition)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPosition.x / cellSize),
                Mathf.FloorToInt(worldPosition.y / cellSize),
                Mathf.FloorToInt(worldPosition.z / cellSize));
        }
    }
}
