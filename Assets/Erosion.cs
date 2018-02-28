using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

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
    [SerializeField] private ComputeShader _eroder;
    [SerializeField] private Material _terrainMaterial;
    [SerializeField] private Material _waterMaterial;

    [SerializeField] private int _res = 512;
    [SerializeField] private float _noiseFreq = 0.1f;
    [SerializeField] private float _maxHeight = 128f;

    private int _noiseKernel;
    private int _normalKernel;
    private int _textureKernel;
    private int _terrainMeshKernel;
    private int _waterMeshKernel;

    private ComputeBuffer _terrainBuffer;
    private ComputeBuffer _terrainMeshBuffer;
    private ComputeBuffer _waterMeshBuffer;

    private RenderTexture _tex;
    
    private int _numVerts;

    void Awake() {
        _noiseKernel = _eroder.FindKernel("GenerateNoise");
        _normalKernel = _eroder.FindKernel("GenerateNormals");
        _textureKernel = _eroder.FindKernel("ToTexture");
        _terrainMeshKernel = _eroder.FindKernel("MeshTerrain");
        _waterMeshKernel = _eroder.FindKernel("MeshWater");
        
        _terrainBuffer = new ComputeBuffer(_res * _res, Marshal.SizeOf(typeof(TerrainData)));

        _numVerts = (_res - 1) * (_res - 1) * 6;
        _terrainMeshBuffer = new ComputeBuffer(_numVerts, Marshal.SizeOf(typeof(Vertex)), ComputeBufferType.Default);
        _waterMeshBuffer = new ComputeBuffer(_numVerts, Marshal.SizeOf(typeof(Vertex)), ComputeBufferType.Default);

        _tex = new RenderTexture(_res, _res, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        _tex.enableRandomWrite = true;
        _tex.Create();

        _eroder.SetInt("_heightRes", _res);
        _eroder.SetFloat("_noiseFreq", _noiseFreq);
        _eroder.SetFloat("_maxHeight", _maxHeight);

        _eroder.SetBuffer(_noiseKernel, "_data", _terrainBuffer);

        _eroder.SetBuffer(_terrainMeshKernel, "_data", _terrainBuffer);
        _eroder.SetBuffer(_terrainMeshKernel, "_terrainMesh", _terrainMeshBuffer);

        _eroder.SetBuffer(_terrainMeshKernel, "_data", _terrainBuffer);
        _eroder.SetBuffer(_terrainMeshKernel, "_waterMesh", _waterMeshBuffer);

        _eroder.SetBuffer(_normalKernel, "_data", _terrainBuffer);

        _eroder.SetBuffer(_textureKernel, "_data", _terrainBuffer);
        _eroder.SetTexture(_textureKernel, "_texture", _tex);
        
        _terrainMaterial.SetBuffer("verts", _terrainMeshBuffer);
        _waterMaterial.SetBuffer("verts", _waterMeshBuffer);

        const int noiseKSize = 32;
        int numNoiseGroups = _res / noiseKSize;
        _eroder.Dispatch(_noiseKernel, numNoiseGroups, numNoiseGroups, 1);

        const int normalKSize = 32;
        int numNormalGroups = _res / normalKSize;
        _eroder.Dispatch(_normalKernel, numNormalGroups, numNormalGroups, 1);

        const int meshKSize = 32;
        int numMeshGroups = (_res - 1) / meshKSize;
        _eroder.Dispatch(_terrainMeshKernel, numMeshGroups, numMeshGroups, 1);
        _eroder.Dispatch(_waterMeshKernel, numMeshGroups, numMeshGroups, 1);

        const int textureKSize = 32;
        int numTextureGroups = _res / textureKSize;
        _eroder.Dispatch(_textureKernel, numTextureGroups, numTextureGroups, 1);
    }

    private void OnDestroy() {
        _terrainBuffer.Release();
        _terrainMeshBuffer.Release();
        _waterMeshBuffer.Release();
    }

    private void OnRenderObject() {
        // Todo: How to draw second mesh? Command buffer?

        _terrainMaterial.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Triangles, _numVerts);
        _waterMaterial.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Triangles, _numVerts);
    }

    private void OnGUI() {
        GUI.DrawTexture(new Rect(10, 10, 512, 512), _tex, ScaleMode.ScaleAndCrop);
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