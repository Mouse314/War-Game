using System;
using UnityEngine;
using UnityEngine.Rendering;

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

    private CommandBuffer _cmd;

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

        BuildCommandBuffer();
    }

    // Собираем весь пайплайн сортировки и хеширования в ОДНУ команду для видеокарты
    private void BuildCommandBuffer()
    {
        _cmd = new CommandBuffer { name = "Spatial Hashing Pipeline" };

        int groups = _paddedCount / 256;
        int sortGroups = _paddedCount / 512; // Каждый поток сортировки обрабатывает 2 элемента!

        // 1. Очищаем offsets
        _cmd.SetComputeBufferParam(_hashShader, _kernelClear, "_CellOffsets", CellOffsetsBuffer);
        _cmd.DispatchCompute(_hashShader, _kernelClear, groups, 1, 1);

        // 2. Считаем хэши
        _cmd.SetComputeBufferParam(_hashShader, _kernelHash, "_SortBuffer", SortBuffer);
        _cmd.DispatchCompute(_hashShader, _kernelHash, groups, 1, 1);

        // 3. Bitonic Sort (Запекаем все проходы сортировки)
        int sortKernel = _sortShader.FindKernel("BitonicSort");
        _cmd.SetComputeBufferParam(_sortShader, sortKernel, "_SortBuffer", SortBuffer);

        for (int dim = 2; dim <= _paddedCount; dim <<= 1)
        {
            for (int block = dim >> 1; block > 0; block >>= 1)
            {
                _cmd.SetComputeIntParam(_sortShader, "_Block", block);
                _cmd.SetComputeIntParam(_sortShader, "_Dim", dim);
                _cmd.DispatchCompute(_sortShader, sortKernel, sortGroups, 1, 1);
            }
        }

        // 4. Строим границы ячеек (Offsets)
        _cmd.SetComputeBufferParam(_hashShader, _kernelOffsets, "_SortBuffer", SortBuffer);
        _cmd.SetComputeBufferParam(_hashShader, _kernelOffsets, "_CellOffsets", CellOffsetsBuffer);
        _cmd.DispatchCompute(_hashShader, _kernelOffsets, groups, 1, 1);
    }

    /// <summary>
    /// Вызывать каждый кадр из MonoBehaviour.Update()
    /// </summary>
    public void Dispatch(ComputeBuffer particleDataBuffer, float searchRadius)
    {
        if (particleDataBuffer == null || searchRadius <= 0.0f)
        {
            return;
        }

        _hashShader.SetInt("_ParticleCount", _particleCount);
        _hashShader.SetInt("_TableSize", _paddedCount);
        _hashShader.SetFloat("_Radius", searchRadius);
        _cmd.SetComputeBufferParam(_hashShader, _kernelHash, "_Positions", particleDataBuffer);

        // Исполняем весь пайплайн в порядке команд на GPU.
        Graphics.ExecuteCommandBuffer(_cmd);
    }

    public void Dispose()
    {
        SortBuffer?.Release();
        CellOffsetsBuffer?.Release();
        _cmd?.Release();
    }
}