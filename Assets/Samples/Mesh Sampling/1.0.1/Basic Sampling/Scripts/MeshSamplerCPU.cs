using System.Collections.Generic;
using MeshSampling;
using UnityEngine;

/// <summary>
/// Example usage of MeshSampling with CPU backend.
/// Demonstrates sampling static, dynamic, and skinned meshes with various sampling methods.
/// Sampled points can be visualized in Unity editor in the scene view and game view when 
/// gizmos are enabled.
/// </summary>
public class MeshSamplerCPU : MonoBehaviour
{
    [Header("Mesh Objects")]
    public MeshFilter staticMeshFilter;
    public MeshFilter dynamicMeshFilter;
    public SkinnedMeshRenderer skinnedMeshRenderer;
    
    [Header("Sampling Settings")]
    public float density = 1000f;
    public RigidSamplerType RigidSamplerType;
    public SkinnedSamplerType SkinnedSamplerType;
    
    // Samplers
    private ISampler staticMeshSampler;
    private ISampler dynamicMeshSampler;
    private ISampler skinnedMeshSampler;
    
    // State Tracking
    private RigidSamplerType lastRigidSamplerType;
    private SkinnedSamplerType lastSkinnedSamplerType;
    
    private void Start()
    {
        SampleRigidObjects();
        SampleSkinnedObjects();
        
        Debug.Log("Gizmos should be enabled to visualize the sampled points.");
    }

    private void LateUpdate()
    {
        if (dynamicMeshSampler != null) dynamicMeshSampler.Update(dynamicMeshFilter.transform);
        if (skinnedMeshSampler != null) skinnedMeshSampler.Update(skinnedMeshRenderer.transform);

        CheckForVariableChange();
    }
    
    private void OnDestroy()
    {
        staticMeshSampler.Cleanup();
        dynamicMeshSampler.Cleanup();
        skinnedMeshSampler.Cleanup();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (staticMeshSampler != null)
            DrawGizmos(staticMeshSampler.GetSamplePoints(), Color.green);
        
        if (dynamicMeshSampler != null)
            DrawGizmos(dynamicMeshSampler.GetSamplePoints(), Color.green);

        if (skinnedMeshSampler != null)
            DrawGizmos(skinnedMeshSampler.GetSamplePoints(), Color.green);
    }
#endif

    private void SampleRigidObjects()
    {
        if (staticMeshFilter != null)
        {
            staticMeshSampler = SamplerFactory.CreateSampler(RigidSamplerType, SamplerBackend.CPU);
            if (!staticMeshSampler.Sample(staticMeshFilter, density, staticMeshFilter.transform))
                staticMeshSampler.Cleanup();
        }

        if (dynamicMeshFilter != null)
        {
            dynamicMeshSampler = SamplerFactory.CreateSampler(RigidSamplerType, SamplerBackend.CPU);
            if (!dynamicMeshSampler.Sample(dynamicMeshFilter, density, dynamicMeshFilter.transform))
                dynamicMeshSampler.Cleanup();
        }
        
        lastRigidSamplerType = RigidSamplerType;
    }

    private void SampleSkinnedObjects()
    {
        if (skinnedMeshRenderer != null)
        {
            skinnedMeshSampler = SamplerFactory.CreateSampler(SkinnedSamplerType, SamplerBackend.CPU);
            if (!skinnedMeshSampler.Sample(skinnedMeshRenderer, density, skinnedMeshRenderer.transform))
                skinnedMeshSampler.Cleanup();
        }
        lastSkinnedSamplerType = SkinnedSamplerType;
    }

    private void CheckForVariableChange()
    {
        // Only update when samplerType changes
        if (RigidSamplerType != lastRigidSamplerType)
        {
            staticMeshSampler.Cleanup();
            dynamicMeshSampler.Cleanup();
            SampleRigidObjects();
        }

        if (SkinnedSamplerType != lastSkinnedSamplerType)
        {
            skinnedMeshSampler.Cleanup();
            SampleSkinnedObjects();
        }
    }

#if UNITY_EDITOR
    private void DrawGizmos(List<Vector3> samplePoints, Color color, float size = 0.01f)
    {
        if (samplePoints == null || samplePoints.Count == 0) return;

        Gizmos.color = color;
        foreach (var point in samplePoints) Gizmos.DrawSphere(point, size);
    }
#endif
}