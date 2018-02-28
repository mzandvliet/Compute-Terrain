using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

public struct TerrainData {
    public float Height;
    public Vector3 Normal;
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

    [SerializeField] private float _noiseFreq = 0.1f;
    [SerializeField] private float _maxHeight = 128f;

    private int _noiseKernel;
    private int _normalKernel;
    private int _textureKernel;
    private int _meshKernel;

    private ComputeBuffer _terrainBuffer;
    private ComputeBuffer _meshBuffer;

    private RenderTexture _tex;
    private Vertex[] _meshBufferCpu;

    private int _res = 512;
    private int _numVerts;

    void Awake() {
        _noiseKernel = _eroder.FindKernel("GenerateNoise");
        _normalKernel = _eroder.FindKernel("GenerateNormals");
        _textureKernel = _eroder.FindKernel("ToTexture");
        _meshKernel = _eroder.FindKernel("ToMesh");
        
        _terrainBuffer = new ComputeBuffer(_res * _res, Marshal.SizeOf(typeof(TerrainData)));

        _numVerts = (_res - 1) * (_res - 1) * 6;
        _meshBuffer = new ComputeBuffer(_numVerts, Marshal.SizeOf(typeof(Vertex)), ComputeBufferType.Default);
        _meshBufferCpu = new Vertex[_numVerts];

        _tex = new RenderTexture(_res, _res, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        _tex.enableRandomWrite = true;
        _tex.Create();

        _eroder.SetInt("_heightRes", _res);
        _eroder.SetFloat("_noiseFreq", _noiseFreq);
        _eroder.SetFloat("_maxHeight", _maxHeight);

        _eroder.SetBuffer(_noiseKernel, "_data", _terrainBuffer);

        _eroder.SetBuffer(_meshKernel, "_data", _terrainBuffer);
        _eroder.SetBuffer(_meshKernel, "_mesh", _meshBuffer);

        _eroder.SetBuffer(_normalKernel, "_data", _terrainBuffer);

        _eroder.SetBuffer(_textureKernel, "_data", _terrainBuffer);
        _eroder.SetTexture(_textureKernel, "_texture", _tex);
        
        _terrainMaterial.SetBuffer("verts", _meshBuffer);

        const int noiseKSize = 32;
        int numNoiseGroups = _res / noiseKSize;
        _eroder.Dispatch(_noiseKernel, numNoiseGroups, numNoiseGroups, 1);

        const int normalKSize = 32;
        int numNormalGroups = _res / normalKSize;
        _eroder.Dispatch(_normalKernel, numNormalGroups, numNormalGroups, 1);
    }

    private void OnDestroy() {
        _terrainBuffer.Release();
        _meshBuffer.Release();
    }

    private void OnRenderObject() {
        _terrainMaterial.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Triangles, _numVerts);
//        _meshBuffer.GetData(_meshBufferCpu, 0, 0, _numVerts);
    }

    private void Update() {
        const int meshKSize = 32;
        int numMeshGroups = (_res - 1) / meshKSize;
        _eroder.Dispatch(_meshKernel, numMeshGroups, numMeshGroups, 1);

        const int textureKSize = 32;
        int numTextureGroups = _res / textureKSize;
        _eroder.Dispatch(_textureKernel, numTextureGroups, numTextureGroups, 1);
    }

    private void OnGUI() {
        GUI.DrawTexture(new Rect(10, 10, _res, _res), _tex, ScaleMode.ScaleAndCrop);
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

//        Vertex[] buff = new Vertex[6];
//        buff[0] = new Vertex(new Vector3(0, 0, 0), Vector3.up, new Vector4(1, 0, 0, 1), new Vector2(0, 0));
//        buff[1] = new Vertex(new Vector3(0, 0, 1), Vector3.up, new Vector4(1, 0, 0, 1), new Vector2(0, 0));
//        buff[2] = new Vertex(new Vector3(1, 0, 1), Vector3.up, new Vector4(1, 0, 0, 1), new Vector2(0, 0));
//        buff[3] = new Vertex(new Vector3(1, 0, 1), Vector3.up, new Vector4(1, 0, 0, 1), new Vector2(0, 0));
//        buff[4] = new Vertex(new Vector3(1, 0, 0), Vector3.up, new Vector4(1, 0, 0, 1), new Vector2(0, 0));
//        buff[5] = new Vertex(new Vector3(0, 0, 0), Vector3.up, new Vector4(1, 0, 0, 1), new Vector2(0, 0));
//        _meshBuffer.SetData(buff);