# SurfaceKuwaharaRenderer

A Unity prototype for surface-based Kuwahara-inspired painterly spot rendering on 3D meshes.

## Current Progress

### Milestone 1: Surface Sampling
The project samples points on a mesh surface and visualizes them as markers.

### Milestone 2: Disc Spot Rendering
Each sampled point is rendered as a flat disc aligned to the surface normal.

### Next Step
Store each sample as structured data with position, normal, UV, base color, and filtered color before applying neighborhood-based Kuwahara filtering.
