using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

/* Todo:
 * - normal generation for terrain and water surfaces is equivalent right now, but
 * the code can't be shared yet. Fix.
 */

public struct TerrainData {
    public float Height;
    public Vector3 Normal;
    public float Water;
}

public struct Vertex {
    public Vector3 Position;
    public Vector3 Normal;
    public Vector4 Tangent;
    public Vector2 Uv;

    public Vertex(Vector3 position, Vector3 normal, Vector4 tangent, Vector2 uv) {
        Position = position;
        Normal = normal;
        Tangent = tangent;
        Uv = uv;
    }
}

public class Erosion : MonoBehaviour {
    [SerializeField] private Camera _camera;
    [SerializeField] private ComputeShader _eroder;
    [SerializeField] private Material _terrainMaterial;
    [SerializeField] private Material _waterMaterial;

    [SerializeField] private int _res = 512;
    [SerializeField] private float _noiseFreq = 0.1f;
    [SerializeField] private float _maxHeight = 128f;

    private int _noiseKernel;
    private int _simulateKernel;
    private int _normalKernel;
    private int _terrainMeshKernel;
    private int _waterMeshKernel;

    private ComputeBuffer _terrainBufferA;
    private ComputeBuffer _terrainBufferB;
    private ComputeBuffer _terrainMeshBuffer;
    private ComputeBuffer _waterMeshBuffer;

    private int _numVerts;

    private CommandBuffer _commandBuffer;

    void Awake() {
        // Create buffers

        _terrainBufferA = new ComputeBuffer(_res * _res, Marshal.SizeOf(typeof(TerrainData)));
        _terrainBufferB = new ComputeBuffer(_res * _res, Marshal.SizeOf(typeof(TerrainData)));

        _numVerts = (_res - 1) * (_res - 1) * 6;
        _terrainMeshBuffer = new ComputeBuffer(_numVerts, Marshal.SizeOf(typeof(Vertex)), ComputeBufferType.Default);
        _waterMeshBuffer = new ComputeBuffer(_numVerts, Marshal.SizeOf(typeof(Vertex)), ComputeBufferType.Default);

        // Find kernels

        _noiseKernel = _eroder.FindKernel("GenerateNoise");
        _simulateKernel = _eroder.FindKernel("Simulate");
        _terrainMeshKernel = _eroder.FindKernel("MeshTerrain");
        _waterMeshKernel = _eroder.FindKernel("MeshWater");
        _normalKernel = _eroder.FindKernel("GenerateNormals");

        // Set all kernel state

        _eroder.SetInt("_heightRes", _res);
        _eroder.SetFloat("_noiseFreq", _noiseFreq);
        _eroder.SetFloat("_maxHeight", _maxHeight);

        SetBuffers();

        _terrainMaterial.SetBuffer("verts", _terrainMeshBuffer);
        _waterMaterial.SetBuffer("verts", _waterMeshBuffer);

        // Initial terrain generation

        const int noiseKSize = 32;
        int numNoiseGroups = _res / noiseKSize;
        _eroder.Dispatch(_noiseKernel, numNoiseGroups, numNoiseGroups, 1);

        const int normalKSize = 32;
        int numNormalGroups = _res / normalKSize;
        _eroder.Dispatch(_normalKernel, numNormalGroups, numNormalGroups, 1);

        // Command buffer for meshing and rendering

        const int meshKSize = 32;
        int numMeshGroups = (_res - 1) / meshKSize;

        _commandBuffer = new CommandBuffer();
        _commandBuffer.DispatchCompute(_eroder, _terrainMeshKernel, numMeshGroups, numMeshGroups, 1);
        _commandBuffer.DispatchCompute(_eroder, _waterMeshKernel, numMeshGroups, numMeshGroups, 1);
        _commandBuffer.CreateGPUFence();
        // Todo: issue normal generation call
        _commandBuffer.DrawProcedural(transform.localToWorldMatrix, _terrainMaterial, 0, MeshTopology.Triangles, _numVerts);
        _commandBuffer.DrawProcedural(transform.localToWorldMatrix, _waterMaterial, 0, MeshTopology.Triangles, _numVerts);
        _camera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, _commandBuffer);
    }

    private void OnDestroy() {
        _terrainBufferA.Release();
        _terrainBufferB.Release();

        _terrainMeshBuffer.Release();
        _waterMeshBuffer.Release();
    }

    private void Update() {
        SwapBuffers();
        SetBuffers();
        
        // Simulate

        const int kSize = 32;
        int groups = _res / kSize;
        _eroder.Dispatch(_simulateKernel, groups, groups, 1);
    }

    private void SwapBuffers() {
        ComputeBuffer tmp = _terrainBufferB;
        _terrainBufferB = _terrainBufferA;
        _terrainBufferA = tmp;
    }

    private void SetBuffers() {
        _eroder.SetBuffer(_noiseKernel, "_dataB", _terrainBufferB);

        _eroder.SetBuffer(_simulateKernel, "_dataA", _terrainBufferA);
        _eroder.SetBuffer(_simulateKernel, "_dataB", _terrainBufferB);

        _eroder.SetBuffer(_terrainMeshKernel, "_dataB", _terrainBufferB);
        _eroder.SetBuffer(_terrainMeshKernel, "_terrainMesh", _terrainMeshBuffer);

        _eroder.SetBuffer(_waterMeshKernel, "_dataB", _terrainBufferB);
        _eroder.SetBuffer(_waterMeshKernel, "_waterMesh", _waterMeshBuffer);

        _eroder.SetBuffer(_normalKernel, "_dataB", _terrainBufferB);
    }
}

public struct Int3 {
    public int X;
    public int Y;
    public int Z;

    public Int3(int x, int y, int z) {
        X = x;
        Y = y;
        Z = z;
    }
}