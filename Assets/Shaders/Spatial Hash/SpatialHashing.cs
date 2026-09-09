using System;
using UnityEngine;

public class SpatialHashSystem : IDisposable
{
    public ComputeBuffer SortBuffer { get; private set; }
    public ComputeBuffer CellOffsetsBuffer { get; private set; }
    public int TableSize => _paddedCount;

    private ComputeShader _hashShader;
    private ComputeShader _sortShader;

    private int _kernelClear;
    private int _kernelHash;
    private int _kernelOffsets;

    private int _particleCount;
    private int _paddedCount; // Степень двойки для Bitonic Sort

    public SpatialHashSystem(ComputeShader hashShader, ComputeShader sortShader, int maxParticles)
    {
        _hashShader = hashShader;
        _sortShader = sortShader;

        _particleCount = Mathf.Max(maxParticles, 1);
        // Находим ближайшую степень двойки (например, для 50 000 это будет 65 536)
        _paddedCount = Mathf.Max(Mathf.NextPowerOfTwo(_particleCount), 512);

        // Инициализируем буферы (размер элемента uint2 = 8 байт)
        SortBuffer = new ComputeBuffer(_paddedCount, 8);
        CellOffsetsBuffer = new ComputeBuffer(_paddedCount, 8);

        _kernelClear = _hashShader.FindKernel("ClearOffsets");
        _kernelHash = _hashShader.FindKernel("HashPositions");
        _kernelOffsets = _hashShader.FindKernel("BuildOffsets");

    }

    /// <summary>
    /// Вызывать каждый кадр из MonoBehaviour.Update()
    /// </summary>
    public void Dispatch(GraphicsBuffer particleDataBuffer, float searchRadius)
    {
        if (particleDataBuffer == null || searchRadius <= 0.0f)
        {
            return;
        }

        _hashShader.SetInt("_ParticleCount", _particleCount);
        _hashShader.SetFloat("_Radius", searchRadius);
        _hashShader.SetInt("_TableSize", _paddedCount);

        int groups = _paddedCount / 256;
        int sortGroups = _paddedCount / 512;
        int sortKernel = _sortShader.FindKernel("BitonicSort");

        _hashShader.SetBuffer(_kernelClear, "_CellOffsets", CellOffsetsBuffer);
        _hashShader.Dispatch(_kernelClear, groups, 1, 1);

        _hashShader.SetBuffer(_kernelHash, "_Positions", particleDataBuffer);
        _hashShader.SetBuffer(_kernelHash, "_SortBuffer", SortBuffer);
        _hashShader.Dispatch(_kernelHash, groups, 1, 1);

        _sortShader.SetBuffer(sortKernel, "_SortBuffer", SortBuffer);
        for (int dim = 2; dim <= _paddedCount; dim <<= 1)
        {
            for (int block = dim >> 1; block > 0; block >>= 1)
            {
                _sortShader.SetInt("_Block", block);
                _sortShader.SetInt("_Dim", dim);
                _sortShader.Dispatch(sortKernel, sortGroups, 1, 1);
            }
        }

        _hashShader.SetBuffer(_kernelOffsets, "_SortBuffer", SortBuffer);
        _hashShader.SetBuffer(_kernelOffsets, "_CellOffsets", CellOffsetsBuffer);
        _hashShader.Dispatch(_kernelOffsets, groups, 1, 1);
    }

    public void Dispose()
    {
        SortBuffer?.Release();
        CellOffsetsBuffer?.Release();
    }
}