using System;
using MeshSampling;
using UnityEngine;

/// <summary>
/// Example usage of MeshSampling with GPU backend.
/// Demonstrates sampling static, dynamic, and skinned meshes with various sampling methods.
/// Visualizes sampled points as billboards through compute buffers sent to a shader.
/// </summary>
public class MeshSamplerGPU : MonoBehaviour
{
    [Header("Mesh Objects")]
    public MeshFilter staticMeshFilter;
    public MeshFilter dynamicMeshFilter;
    public SkinnedMeshRenderer skinnedMeshRenderer;
    
    [Header("Sampling Settings")]
    public float density = 1000f;
    public RigidSamplerType rigidSamplerType;
    public SkinnedSamplerType skinnedSamplerType;
    
    [Header("Rendering Settings")]
    public Material billboardMaterial;
    public bool showSampledPoints = true;
    public bool enableOcclusion = true;
    
    // Samplers
    private ISampler staticMeshSampler;
    private ISampler dynamicMeshSampler;
    private ISampler skinnedMeshSampler;
    
    // Buffers
    private ComputeBuffer staticPointsBuffer;
    private ComputeBuffer dynamicPointsBuffer;
    private ComputeBuffer skinnedPointsBuffer;
    
    // Point counts
    private int staticPointCount;
    private int dynamicPointCount;
    private int skinnedPointCount;
    
    // State tracking
    private RenderingSystem renderingSystem;
    private RigidSamplerType lastRigidSamplerType;
    private SkinnedSamplerType lastSkinnedSamplerType;
    private bool lastShowSampledPoints;
    private bool lastEnableOcclusion;
    
    // WebGPU readback IDs
    private int dynamicReadbackId = -1;
    private int skinnedReadbackId = -1;

    private void Start()
    {
        renderingSystem = new RenderingSystem(billboardMaterial);
        
        // Initialize samplers
        SampleRigidObjects();
        SampleSkinnedObjects();
        
        // Initialize visualization
        UpdatePointsVisualizationStatus();
        UpdateOcclusionStatus();
    }

    private void LateUpdate()
    {
        // Clear the command buffer at the beginning of each frame
        if (showSampledPoints) renderingSystem.ClearBuffer();
        
        // Update dynamic and skinned meshes
#if UNITY_EDITOR
        UpdateDynamicMeshSamples();
        UpdateSkinnedMeshSamples();
#else
        UpdateDynamicMeshSamplesWebGPU();
        UpdateSkinnedMeshSamplesWebGPU();
#endif
        
        // Render points
        if (showSampledPoints) RenderPoints();
        if (enableOcclusion) renderingSystem.CheckForScreenResolutionChange();
        
        // Check for changes in settings
        CheckForVariableChange();
    }

    private void OnDestroy()
    {
        ReleaseRigidResources();
        ReleaseSkinnedResources();
    }
    
    private void SampleRigidObjects()
    {
        // Setup static mesh sampler
        if (staticMeshFilter != null)
        {
            staticMeshSampler = SamplerFactory.CreateSampler(rigidSamplerType, SamplerBackend.GPU);
            if (!staticMeshSampler.Sample(staticMeshFilter, density, staticMeshFilter.transform))
                staticMeshSampler.Cleanup();
            
            staticPointsBuffer = staticMeshSampler.GetSamplePointsBuffer();
            staticPointCount = staticMeshSampler.GetSampleCount();
        }
        
        // Setup dynamic mesh sampler
        if (dynamicMeshFilter != null)
        {
            dynamicMeshSampler = SamplerFactory.CreateSampler(rigidSamplerType, SamplerBackend.GPU);
            if (!dynamicMeshSampler.Sample(dynamicMeshFilter, density, dynamicMeshFilter.transform))
                dynamicMeshSampler.Cleanup();
            
            dynamicPointsBuffer = dynamicMeshSampler.GetSamplePointsBuffer();
            dynamicPointCount = dynamicMeshSampler.GetSampleCount();
        }
        
        lastRigidSamplerType = rigidSamplerType;
    }
    
    private void SampleSkinnedObjects()
    {
        // Setup skinned mesh sampler
        if (skinnedMeshRenderer != null)
        {
            skinnedMeshSampler = SamplerFactory.CreateSampler(skinnedSamplerType, SamplerBackend.GPU);
            if (!skinnedMeshSampler.Sample(skinnedMeshRenderer, density, skinnedMeshRenderer.transform))
                skinnedMeshSampler.Cleanup();
            
            skinnedPointsBuffer = skinnedMeshSampler.GetSamplePointsBuffer();
            skinnedPointCount = skinnedMeshSampler.GetSampleCount();
        }
        
        lastSkinnedSamplerType = skinnedSamplerType;
    }
    
    private void UpdateDynamicMeshSamples()
    {
        if (dynamicMeshSampler == null) return;
        
        var hasUpdated = dynamicMeshSampler.Update(dynamicMeshFilter.transform);
        
        if (hasUpdated)
        {
            var points = dynamicMeshSampler.GetSamplePointsArray();
            dynamicPointsBuffer.SetData(points, 0, 0, dynamicPointCount);
        }
    }
    
    private void UpdateSkinnedMeshSamples()
    {
        if (skinnedMeshSampler == null) return;
        
        var hasUpdated = skinnedMeshSampler.Update(skinnedMeshRenderer.transform);
        
        if (hasUpdated)
        {
            var points = skinnedMeshSampler.GetSamplePointsArray();
            skinnedPointsBuffer.SetData(points, 0, 0, skinnedPointCount);
        }
    }
    
    private void UpdateDynamicMeshSamplesWebGPU()
    {
        if (dynamicMeshSampler == null) return;
        
        var hasUpdated = dynamicMeshSampler.Update(dynamicMeshFilter.transform);
        
        // Only start new readbacks if not already in progress and if data has changed
        if (dynamicReadbackId == -1 && hasUpdated && dynamicPointCount > 0)
        {
            dynamicReadbackId = BufferUtility.SetVector3DataWebGPU(
                dynamicMeshSampler.GetSamplePointsBuffer(),
                dynamicPointsBuffer,
                0, // Source start
                0, // Target start
                dynamicPointCount,
                success =>
                {
                    dynamicReadbackId = -1; // Reset ID when complete
                });
        }
    }
    
    private void UpdateSkinnedMeshSamplesWebGPU()
    {
        if (skinnedMeshSampler == null) return;
        
        var hasUpdated = skinnedMeshSampler.Update(skinnedMeshRenderer.transform);
        
        // Only start new readbacks if not already in progress and if data has changed
        if (skinnedReadbackId == -1 && hasUpdated && skinnedPointCount > 0)
        {
            skinnedReadbackId = BufferUtility.SetVector3DataWebGPU(
                skinnedMeshSampler.GetSamplePointsBuffer(),
                skinnedPointsBuffer,
                0, // Source start
                0, // Target start
                skinnedPointCount,
                success =>
                {
                    skinnedReadbackId = -1; // Reset ID when complete
                });
        }
    }
    
    private void RenderPoints()
    {
        // Render static mesh points
        if (staticMeshSampler != null)
            renderingSystem.RenderPoints(staticPointsBuffer, staticPointCount, RenderPass.Static);
        
        // Render dynamic mesh points
        if (dynamicMeshSampler != null)
            renderingSystem.RenderPoints(dynamicPointsBuffer, dynamicPointCount, RenderPass.Dynamic);
        
        // Render skinned mesh points
        if (skinnedMeshSampler != null)
            renderingSystem.RenderPoints(skinnedPointsBuffer, skinnedPointCount, RenderPass.Skinned);
    }
    
    private void CheckForVariableChange()
    {
        // Check for sampler type changes
        if (rigidSamplerType != lastRigidSamplerType)
        {
            ReleaseRigidResources();
            SampleRigidObjects();
        }
        
        if (skinnedSamplerType != lastSkinnedSamplerType)
        {
            ReleaseSkinnedResources();
            SampleSkinnedObjects();
        }
        
        // Check for visualization changes
        if (showSampledPoints != lastShowSampledPoints)
            UpdatePointsVisualizationStatus();
        
        if (enableOcclusion != lastEnableOcclusion)
            UpdateOcclusionStatus();
    }
    
    private void UpdatePointsVisualizationStatus()
    {
        renderingSystem.SetSamplePointsVisibility(showSampledPoints);
        if (showSampledPoints)
            RenderPoints();
        
        lastShowSampledPoints = showSampledPoints;
    }
    
    private void UpdateOcclusionStatus()
    {
        renderingSystem.SetOcclusion(enableOcclusion);
        lastEnableOcclusion = enableOcclusion;
    }

    private void ReleaseRigidResources()
    {
#if !UNITY_EDITOR
            // Cancel any pending operations
            if (dynamicReadbackId != -1) BufferUtility.CancelSetDataWebGPU(dynamicReadbackId);
            dynamicReadbackId = -1;
#endif
            
        // Cleanup rigid samplers
        if (staticMeshSampler != null) staticMeshSampler.Cleanup();
        if (dynamicMeshSampler != null) dynamicMeshSampler.Cleanup();
            
        // Release buffers
        BufferUtility.ReleaseBuffer(ref staticPointsBuffer);
        BufferUtility.ReleaseBuffer(ref dynamicPointsBuffer);
        renderingSystem.ClearBuffer();
    }

    private void ReleaseSkinnedResources()
    {
#if !UNITY_EDITOR
            // Cancel any pending operations
            if (skinnedReadbackId != -1) BufferUtility.CancelSetDataWebGPU(skinnedReadbackId);
            skinnedReadbackId = -1;
#endif
            
        // Cleanup skinned sampler
        if (skinnedMeshSampler != null) skinnedMeshSampler.Cleanup();
            
        // Release buffer
        BufferUtility.ReleaseBuffer(ref skinnedPointsBuffer);
        renderingSystem.ClearBuffer();
    }
}