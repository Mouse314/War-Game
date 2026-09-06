using UnityEngine;
using UnityEngine.InputSystem;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
struct ParticleData
{
    public Vector3 position;
    public Vector3 velocity;
    public Color color;
    public uint isAlive;
    public uint fraction;
    public float health;
}

public class Main : MonoBehaviour
{
    public ComputeShader computeShader;
    public ComputeShader setPositionsShader;

    // Spatial hash
    public ComputeShader initShader;
    public ComputeShader sortShader;

    private SpatialHashSystem spatialHashSystem;

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

    // Game logic
    public float _attackStrength = 10.0f;
    private int nextSpawnIndex;
    private bool wasMousePressed;
    private bool isPaused = true;

    void Start()
    {
        size = Mathf.Max(size, 1);
        threadGroupSize = Mathf.CeilToInt(size / (float)dimensionSize);

        spatialHashSystem = new SpatialHashSystem(initShader, sortShader, size * size);

        computeShader.SetInt("_TableSize", spatialHashSystem.TableSize);
        computeShader.SetFloat("_Radius", searchRadius);
        computeShader.SetFloat("_repulsionStrength", _repulsionStrength);
        computeShader.SetFloat("_cohesionStrength", _cohesionStrength);
        computeShader.SetFloat("_attackStrength", _attackStrength);
        computeShader.SetBool("_isPaused", isPaused);

        setPositionsShader.SetInt("size", size);
        setPositionsShader.SetFloat("spread", 12.0f);

        computeShader.SetTexture(0, "_LandscapeTexture", landscapeTexture != null ? landscapeTexture : Texture2D.whiteTexture);
        int particleDataStride = Marshal.SizeOf<ParticleData>();
        _particlesDataBuffer = new ComputeBuffer(size * size, particleDataStride);
        _nextParticlesDataBuffer = new ComputeBuffer(size * size, particleDataStride);
        setPositionsShader.SetBuffer(0, "_particlesData", _particlesDataBuffer);

        setPositionsShader.Dispatch(0, threadGroupSize, 1, threadGroupSize);

        landscapePlane = new Plane(landscapePlaneTransform.up, landscapePlaneTransform.position);
        nextSpawnIndex = 0;
    }

    void Update()
    {
        computeShader.SetFloat("deltaTime", Time.deltaTime);
        computeShader.SetFloat("time", Time.time);
        computeShader.SetInt("size", size);
        computeShader.SetFloat("_Radius", searchRadius);
        computeShader.SetFloat("_repulsionStrength", _repulsionStrength);
        computeShader.SetFloat("_cohesionStrength", _cohesionStrength);
        computeShader.SetFloat("_attackStrength", _attackStrength);
        computeShader.SetBuffer(0, "_particlesDataRead", _particlesDataBuffer);
        computeShader.SetBuffer(0, "_particlesDataWrite", _nextParticlesDataBuffer);
        computeShader.SetBuffer(0, "_sortBuffer", spatialHashSystem.SortBuffer);
        computeShader.SetBuffer(0, "_cellOffsetsBuffer", spatialHashSystem.CellOffsetsBuffer);

        bool isMousePressed = Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed);
        bool spawnParticle = false;
        computeShader.SetInt("_spawnIndex", -1);

        if (isMousePressed)
        {
            if (landscapePlane.Raycast(Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue()), out float enter))
            {
                Vector3 hitPoint = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue()).GetPoint(enter);
                computeShader.SetVector("_mousePosition", new Vector2(hitPoint.x, hitPoint.z));
                spawnParticle = !wasMousePressed && nextSpawnIndex < size * size;
                computeShader.SetInt("_mouseButtonPressed", Mouse.current.leftButton.isPressed ? 0 : 1);
                computeShader.SetInt("_spawnIndex", spawnParticle ? nextSpawnIndex : -1);
            }
        }
        computeShader.SetInt("_isMouseClicked", isMousePressed ? 1 : 0);
        
        computeShader.SetInt("_isShiftPressed", Keyboard.current.shiftKey.isPressed ? 1 : 0);

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            isPaused = !isPaused;
            computeShader.SetBool("_isPaused", isPaused);
        }

        if (spawnParticle)
        {
            nextSpawnIndex++;
        }
        wasMousePressed = isMousePressed;

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
