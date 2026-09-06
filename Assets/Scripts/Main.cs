using UnityEngine;
using UnityEngine.InputSystem;

struct ParticleData
{
    public Vector3 position;
    public Vector3 velocity;
    public Color color;
}

public class Main : MonoBehaviour
{
    public ComputeShader computeShader;
    public ComputeShader setPositionsShader;

    // Spatial hash
    public ComputeShader initShader;
    public ComputeShader sortShader;

    private SpatialHashSystem spatialHashSystem;
    private ComputeBuffer _sortedBuffer;
    private ComputeBuffer _cellOffsetsBuffer;

    public float searchRadius = 0.1f;

    // Particles
    public int size = 100;

    private ComputeBuffer _particlesDataBuffer;
    private ComputeBuffer _nextParticlesDataBuffer;

    private int dimensionSize = 4;
    private int threadGroupSize;

    public ParticleControl particleControl;
    public float glow = 1.0f;
    public float particleSize = 0.02f;

    public float _repulsionStrength = 10.0f;
    public float _cohesionStrength = 0.1f;

    // Landscape

    public Transform landscapePlaneTransform;
    public Texture landscapeTexture;

    private Plane landscapePlane;

    void Start()
    {
        size = Mathf.Max(size, 1);
        threadGroupSize = Mathf.CeilToInt(size / (float)dimensionSize);

        spatialHashSystem = new SpatialHashSystem(initShader, sortShader, size * size);

        computeShader.SetInt("_TableSize", spatialHashSystem.TableSize);
        computeShader.SetFloat("_Radius", searchRadius);

        setPositionsShader.SetInt("size", size);
        setPositionsShader.SetFloat("spread", 12.0f);

        computeShader.SetTexture(0, "_LandscapeTexture", landscapeTexture != null ? landscapeTexture : Texture2D.whiteTexture);
        _particlesDataBuffer = new ComputeBuffer(size * size, sizeof(float) * 3 * 2 + sizeof(float) * 4);
        _nextParticlesDataBuffer = new ComputeBuffer(size * size, sizeof(float) * 3 * 2 + sizeof(float) * 4);
        setPositionsShader.SetBuffer(0, "_particlesData", _particlesDataBuffer);

        setPositionsShader.Dispatch(0, threadGroupSize, 1, threadGroupSize);

        landscapePlane = new Plane(landscapePlaneTransform.up, landscapePlaneTransform.position);
    }

    void Update()
    {
        computeShader.SetFloat("deltaTime", Time.deltaTime);
        computeShader.SetFloat("time", Time.time);
        computeShader.SetInt("size", size);
        computeShader.SetFloat("_Radius", searchRadius);
        computeShader.SetFloat("_repulsionStrength", _repulsionStrength);
        computeShader.SetFloat("_cohesionStrength", _cohesionStrength);
        computeShader.SetBuffer(0, "_particlesDataRead", _particlesDataBuffer);
        computeShader.SetBuffer(0, "_particlesDataWrite", _nextParticlesDataBuffer);
        computeShader.SetBuffer(0, "_sortBuffer", spatialHashSystem.SortBuffer);
        computeShader.SetBuffer(0, "_cellOffsetsBuffer", spatialHashSystem.CellOffsetsBuffer);

        if (Mouse.current.leftButton.isPressed)
        {
            computeShader.SetInt("_isMouseClicked", 1);
            if (landscapePlane.Raycast(Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue()), out float enter))
            {
                Vector3 hitPoint = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue()).GetPoint(enter);
                computeShader.SetVector("_mousePosition", new Vector2(hitPoint.x, hitPoint.z));
                computeShader.SetInt("_isMouseClicked", 1);
            }
        }
        else
        {
            computeShader.SetInt("_isMouseClicked", 0);
        }

        // Spatial hashing pass
        spatialHashSystem.Dispatch(_particlesDataBuffer, searchRadius);

        computeShader.Dispatch(0, threadGroupSize, 1, threadGroupSize);

        (_particlesDataBuffer, _nextParticlesDataBuffer) = (_nextParticlesDataBuffer, _particlesDataBuffer);
        particleControl.DrawInstances(_particlesDataBuffer, size * size, glow, particleSize);
    }

    void OnDestroy()
    {
        if (_particlesDataBuffer != null)
        {
            _particlesDataBuffer.Release();
        }

        if (_nextParticlesDataBuffer != null)
        {
            _nextParticlesDataBuffer.Release();
        }

        spatialHashSystem?.Dispose();
    }
}
