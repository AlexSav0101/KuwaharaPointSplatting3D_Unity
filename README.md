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

## Next Steps

- Implement a spatial grid for neighbor lookup.
- For each sample, find nearby surface samples within a radius.
- Split the local neighborhood into sectors.
- Compute mean color and variance per sector.
- Assign the lowest-variance sector color to the current sample.
- Improve sampling distribution toward Poisson-disk spacing.
