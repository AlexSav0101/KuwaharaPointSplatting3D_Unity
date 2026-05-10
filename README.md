# SurfaceKuwaharaRenderer

A Unity prototype for surface-based Kuwahara-inspired painterly spot rendering on 3D meshes.

## Current Progress

### Milestone 1: Surface Sampling
The project samples points on a mesh surface and visualizes them as markers.

### Milestone 2: Disc Spot Rendering
Each sampled point is rendered as a flat disc aligned to the surface normal.

### Milestone 3: Surface Sample Data Model

Refactored the sampled points into structured surface sample data. Each sample stores:

- local and world position
- local and world normal
- UV coordinate
- base color
- filtered color
- marker object reference

This prepares the project for neighborhood search and Kuwahara-inspired filtering.


### Milestone 4: Spatial Grid Neighbor Search

Implemented a CPU-based uniform 3D spatial grid for querying nearby surface samples. This allows each sampled disc to find neighboring samples within a configurable radius. The neighbor search prepares the project for the Kuwahara-inspired filtering step, where each sample will analyze nearby samples, split them into local regions, compute mean color and variance, and choose the lowest-variance region for the final spot color.

## Next Steps
- Split the local neighborhood into sectors.
- Compute mean color and variance per sector.
- Assign the lowest-variance sector color to the current sample.
- Improve sampling distribution toward Poisson-disk spacing.

