using UnityEngine;

[CreateAssetMenu(fileName = "ParticleControl", menuName = "ScriptableObjects/ParticleControl")]
public class ParticleControl : ScriptableObject
{
    public Shader particleShader;
    public Mesh particleMesh;
    public Material particleMaterial;

    public void DrawInstances(GraphicsBuffer particlesDataBuffer, int totalParticles, float glow = 1.0f, float particleSize = 0.02f)
    {
        particleMaterial.SetBuffer("_particlesData", particlesDataBuffer);
        particleMaterial.SetFloat("glow", glow);
        particleMaterial.SetFloat("particleSize", particleSize);
        Graphics.DrawMeshInstancedProcedural(
            particleMesh,
            0,
            particleMaterial,
            new Bounds(Vector3.zero, Vector3.one * 100),
            totalParticles,
            null,
            UnityEngine.Rendering.ShadowCastingMode.Off,
            false
        );
    }
}