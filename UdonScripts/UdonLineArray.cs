
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class UdonLineArray : UdonSharpBehaviour
{
    private readonly Vector3 Average = new Vector3(1f / 3f, 1f / 3f, 1f / 3f);

    #region udon parameters
    /// <summary>
    /// Light saber factor
    /// </summary>
    [SerializeField]
    [Range(0.0f, 1.0f), FieldChangeCallback(nameof(LightSaberEffect))]
    private float _lightSaberEffect;
    private bool _hasSaberEffect = false;
    /// <summary>
    /// Get or set the light saber factor of this volumetric line's _material
    /// </summary>
    public float LightSaberEffect
    {
        get { return _lightSaberEffect; }
        set
        {
            _lightSaberEffect = value;
            if (_material != null)
            {
                _material.SetFloat("_LightSaberFactor", _lightSaberEffect);
            }
        }
    }

    [SerializeField, Range(0f, 0.25f), FieldChangeCallback(nameof(LineWidth))]
    private float lineWidth = 0.03f;
    public float LineWidth
    {
        get => lineWidth;
        set
        {
            //Debug.Log(string.Format("{0}: lineWidth {1:F2}", gameObject.name, value));
            lineWidth = value;
            if (_material != null)
            {
                _material.SetFloat("_LineWidth", lineWidth);
            }
            UpdateBounds();
        }
    }

    [SerializeField]
    float lineScale = 1f;
    public float LineScale
    {
        get => lineScale;
        set
        {
            lineScale = value;
            if (_material != null)
                _material.SetFloat("_LineScale", lineScale);
        }
    }

    [SerializeField]
    private Vector3[] _lineVertices;
    /// <summary>
    /// Gets the vertices of this line strip
    /// </summary>
    public Vector3[] LineVertices
    {
        get
        {
            return _lineVertices;
        }
        set
        {
            if (value != null && value.Length >= 2)
            {
                _lineVertices = value;
                BuildMeshFromVertices();
                SetMaterialProperties();
            }
        }
    }

    [SerializeField, FieldChangeCallback(nameof(LineColour))]
    public Color lineColour = Color.cyan;

    private Color currentColour = Color.white;
    [SerializeField, Range(0f, 1f), FieldChangeCallback(nameof(Alpha))]
    private float alpha = 1;

    public Color LineColour
    {
        get => lineColour;
        set
        {
            lineColour = value;
            if (_material != null)
                _material.color = lineColour;
        }
    }

    private float _alpha = -1;
    public float Alpha
    {
        get => alpha;
        set
        {
            //Debug.Log(string.Format("{0}: alpha {1:F2}", gameObject.name, value));
            alpha = value;
            if (_material != null && _alpha != alpha)
            {
                _material.SetFloat("_Intensity", alpha);
                _alpha = alpha;
            }
        }
    }

    /// <summary>
    /// This GameObject's specific _material
    /// </summary>
    [SerializeField]
    private Material _material;
    /// <summary>
    /// This GameObject's _mesh filter
    /// </summary>
    private MeshFilter _meshFilter;
    private Mesh _mesh;

    /// <summary>
    /// Template material
    /// </summary>
    [SerializeField]
    private Material templateMaterial;
    #endregion
    //[SerializeField]
    private Vector3[] _starts;
    //[SerializeField]
    private Vector3[] _ends;
    //[SerializeField]

    /// <summary>
    /// Updates the bounds of this line according to the current properties, 
    /// which there are: start point, end point, line width, scaling of the object.
    /// </summary>

    #region mesh calculations
    private bool UpdateBounds()
    {
        if (_mesh == null || _lineVertices == null || _lineVertices.Length == 0)
            return false;

        Vector3 min = _lineVertices[0];
        Vector3 max = _lineVertices[0];
        for (int i = 1; i < _lineVertices.Length; ++i)
        {
            min = new Vector3(
                Mathf.Min(min.x, _lineVertices[i].x),
                Mathf.Min(min.y, _lineVertices[i].y),
                Mathf.Min(min.z, _lineVertices[i].z)
            );
            max = new Vector3(
                Mathf.Max(max.x, _lineVertices[i].x),
                Mathf.Max(max.y, _lineVertices[i].y),
                Mathf.Max(max.z, _lineVertices[i].z)
            );
        }
        _mesh.bounds.SetMinMax(min, max);
        return true;
    }


    /// <summary>
    /// Sets the start and end points - updates the data of the Mesh.
    /// </summary>
    /// 
    //[SerializeField]
    private Vector3[] _vertexPositions;
    private Vector3[] _otherPositions;

    private int appendVertices(int idx, Vector3 start, Vector3 end)
    {
        for (int i = 0; i < 4; i++, idx++)
        {
            _vertexPositions[idx] = start;
            _otherPositions[idx] = end;
        }
        for (int i = 0; i < 4; i++, idx++)
        {
            _vertexPositions[idx] = end;
            _otherPositions[idx] = start;
        }
        return idx;
    }

    int prevVertexCount = -1;
    [SerializeField]
    int vertexCount = 0;
    public void BuildMeshFromVertices()
    {
        int lineCount = (_lineVertices != null) ? _lineVertices.Length / 2 : 0;
        _starts = new Vector3[lineCount];
        _ends = new Vector3[lineCount];
        // Initialize the start and end points from the provided vertices
        int vertexIndex = 0;
        for (int i = 0; i < lineCount; i++)
        {
            _starts[i] = _lineVertices[vertexIndex++];
            _ends[i] = _lineVertices[vertexIndex++];
        }
        // float theta = thetaDegrees*Mathf.Deg2Rad;
        vertexCount = lineCount * 8;
        if (vertexCount != prevVertexCount)
        {
            _vertexPositions = new Vector3[vertexCount];
            _otherPositions = new Vector3[vertexCount];
        }
        int vertIdx = 0;
        for (int i = 0; i < lineCount; i++)
            vertIdx = appendVertices(vertIdx, _starts[i], _ends[i]);
        if (_mesh == null)
            return;
        if (prevVertexCount != vertexCount)
            _mesh.Clear();
        _mesh.vertices = _vertexPositions;
        _mesh.normals = _otherPositions;
        UpdateBounds();
        if (prevVertexCount != vertexCount)
            initUVs(vertexCount, lineCount);
        prevVertexCount = vertexCount;
    }

    private bool initUVs(int numVertices, int lineCount)
    {
        if (_mesh == null)
            return false;
        Vector2[] uvs = new Vector2[numVertices];
        Vector2[] uv2 = new Vector2[numVertices];
        int t = 0;
        int o = 0;
        for (int i = 0; i < lineCount; i++)
        {
            uvs[t++] = new Vector2(1.0f, 1.0f);
            uvs[t++] = new Vector2(1.0f, 0.0f);
            uvs[t++] = new Vector2(0.5f, 1.0f);
            uvs[t++] = new Vector2(0.5f, 0.0f);
            uvs[t++] = new Vector2(0.5f, 0.0f);
            uvs[t++] = new Vector2(0.5f, 1.0f);
            uvs[t++] = new Vector2(0.0f, 0.0f);
            uvs[t++] = new Vector2(0.0f, 1.0f);

            uv2[o++] = new Vector2(1.0f, 1.0f);
            uv2[o++] = new Vector2(1.0f, -1.0f);
            uv2[o++] = new Vector2(0.0f, 1.0f);
            uv2[o++] = new Vector2(0.0f, -1.0f);
            uv2[o++] = new Vector2(0.0f, 1.0f);
            uv2[o++] = new Vector2(0.0f, -1.0f);
            uv2[o++] = new Vector2(1.0f, 1.0f);
            uv2[o++] = new Vector2(1.0f, -1.0f);
        }
        _mesh.uv = uvs;
        _mesh.uv2 = uv2;
        int idx = 0;
        int[] indices = new int[lineCount * 18];
        for (int i = 0; i < lineCount; i++)
        {
            int offs = i * 8;
            // 2, 1, 0,
            indices[idx++] = offs + 2; indices[idx++] = offs + 1; indices[idx++] = offs + 0;
            // 3, 1, 2,
            indices[idx++] = offs + 3; indices[idx++] = offs + 1; indices[idx++] = offs + 2;
            // 4, 3, 2,
            indices[idx++] = offs + 4; indices[idx++] = offs + 3; indices[idx++] = offs + 2;
            // 5, 4, 2,
            indices[idx++] = offs + 5; indices[idx++] = offs + 4; indices[idx++] = offs + 2;
            // 4, 5, 6,
            indices[idx++] = offs + 4; indices[idx++] = offs + 5; indices[idx++] = offs + 6;
            // 6, 5, 7
            indices[idx++] = offs + 6; indices[idx++] = offs + 5; indices[idx++] = offs + 7;
        }
        _mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        return true;
    }

    /// <summary>
    /// Calculates the (approximated) _LineScale factor based on the object's scale.
    /// </summary>
    private float CalculateScale()
    {
        return Vector3.Dot(transform.lossyScale, Average);
    }

    public void UpdateScale()
    {
        if (_material != null)
        {
            _material.SetFloat("_LineScale", CalculateScale());
        }
    }

    private void OnEnable()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _mesh = new Mesh();
        _meshFilter.mesh = _mesh;
        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.material = templateMaterial;
        _material = mr.material;
    }

    private void Start()
    {
        if (_lineVertices != null && _lineVertices.Length >= 2)
        {
            BuildMeshFromVertices();
        }
        SetMaterialProperties();
    }

#if UNITY_EDITOR
    private void OnValidate()

    /// <summary>
    /// 
    /// Sets all _material properties (color, width, light saber factor, start-, endpos)
    /// </summary>
    {
        if (_meshFilter == null)
            _meshFilter = GetComponent<MeshFilter>();
        SetMaterialProperties();
    }
#endif
    private void SetMaterialProperties()
    {
        if (_material == null)
            return;
        _material.color = lineColour;
        _material.SetFloat("_Intensity", alpha);
        _material.SetFloat("_LineWidth", lineWidth);
        if (_hasSaberEffect)
            _material.SetFloat("_LightSaberFactor", _lightSaberEffect);
        UpdateScale();
    }
    #endregion
}
