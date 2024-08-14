
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// Render a line strip of volumetric lines
/// 
/// Based on the Volumetric lines algorithm by Sebastien Hillaire
/// https://web.archive.org/web/20111202022753/http://sebastien.hillaire.free.fr/index.php?option=com_content&view=article&id=57&Itemid=74
/// 
/// Shaders are by 
/// 
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]

public class UdonLineStrip : UdonSharpBehaviour
{
    private readonly Vector3 Average = new Vector3(1f / 3f, 1f / 3f, 1f / 3f);
    #region private variables
    /// <summary>
    /// Template material to be used
    /// </summary>
    [SerializeField]
    public Material templateMaterial;

    /// <summary>
    /// Line Color
    /// </summary>
    [SerializeField]
    private Color _lineColor;

    /// <summary>
    /// The width of the line
    /// </summary>
    [SerializeField]
    private float _lineWidth;

    /// <summary>
    /// Light saber factor
    /// </summary>
    [SerializeField]
    [Range(0.0f, 1.0f)]
    private float _lightSaberEffect;
    [SerializeField]
    private bool _hasSaberEffect = false;

    /// <summary>
    /// This GameObject's specific material
    /// </summary>
    private Material _material;


    /// <summary>
    /// The vertices of the line
    /// </summary>
    [SerializeField]
    private Vector3[] _lineVertices;
    /// <summary>
    /// This GameObject's _mesh filter
    /// </summary>
    private MeshFilter _meshFilter;

    private Mesh _mesh;

    #endregion
    #region properties
    /// <summary>
    /// Gets or sets the tmplate material.
    /// Setting this will only have an impact once. 
    /// Subsequent changes will be ignored.
    /// </summary>
    public Material TemplateMaterial
    {
        get { return templateMaterial; }
        set { templateMaterial = value; }
    }

    /// <summary>
    /// Get or set the line color of this volumetric line's material
    /// </summary>
    public Color LineColor
    {
        get { return _lineColor; }
        set
        {
            //CreateMaterial();
            if (null != _material)
            {
                _lineColor = value;
                _material.color = _lineColor;
            }
        }
    }

    /// <summary>
    /// Get or set the line width of this volumetric line's material
    /// </summary>
    public float LineWidth
    {
        get { return _lineWidth; }
        set
        {
            //CreateMaterial();
            if (null != _material)
            {
                _lineWidth = value;
                _material.SetFloat("_LineWidth", _lineWidth);
            }
            //UpdateBounds();
        }
    }

    /// <summary>
    /// Get or set the light saber factor of this volumetric line's material
    /// </summary>
    public float LightSaberFactor
    {
        get { return _lightSaberEffect; }
        set
        {
            //CreateMaterial();
            if (null != _material)
            {
                _lightSaberEffect = value;
                _material.SetFloat("_LightSaberFactor", _lightSaberEffect);
            }
        }
    }

    /// <summary>
    /// Gets the vertices of this line strip
    /// </summary>
    public Vector3[] LineVertices
    {
        get { return _lineVertices; }
    }

    #endregion
    #region mesh update
    /// <summary>
    /// Calculate the bounds of this line based on the coordinates of the line vertices,
    /// the line width, and the scaling of the object.
    /// </summary>
    private bool UpdateBounds()
    {
        var maxWidth = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        var scaledLineWidth = maxWidth * LineWidth * 0.5f;
        var scaledLineWidthVec = new Vector3(scaledLineWidth, scaledLineWidth, scaledLineWidth);

        if (_mesh== null || _lineVertices== null || _lineVertices.Length == 0)
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



    public bool BuildMeshFromVertices(Vector3[] newVertexList)
    {
        if (_mesh == null)
        {
            Debug.Log(gameObject.name + ": UdonLineStrip Mesh of Meshfilter component is null");
            return false;
        }

        if (newVertexList == null || newVertexList.Length < 3)
        {
            Debug.Log(gameObject.name + ": Add at least 3 vertices to the UdonLineStrip");
            return false;
        }

        _lineVertices = newVertexList;

        // fill vertex positions, and indices
        // 2 for each position, + 2 for the start, + 2 for the end
        Vector3[] vertexPositions = new Vector3[_lineVertices.Length * 2 + 4];
        // there are #vertices - 2 faces, and 3 indices each
        int[] indices = new int[(_lineVertices.Length * 2 + 2) * 3];
        int v = 0;
        int x = 0;
        vertexPositions[v++] = _lineVertices[0];
        vertexPositions[v++] = _lineVertices[0];
        for (int i = 0; i < _lineVertices.Length; ++i)
        {
            vertexPositions[v++] = _lineVertices[i];
            vertexPositions[v++] = _lineVertices[i];
            indices[x++] = v - 2;
            indices[x++] = v - 3;
            indices[x++] = v - 4;
            indices[x++] = v - 1;
            indices[x++] = v - 2;
            indices[x++] = v - 3;
        }
        vertexPositions[v++] = _lineVertices[_lineVertices.Length - 1];
        vertexPositions[v++] = _lineVertices[_lineVertices.Length - 1];
        indices[x++] = v - 2;
        indices[x++] = v - 3;
        indices[x++] = v - 4;
        indices[x++] = v - 1;
        indices[x++] = v - 2;
        indices[x++] = v - 3;

        // fill texture coordinates and vertex offsets
        Vector2[] texCoords = new Vector2[vertexPositions.Length];
        Vector2[] vertexOffsets = new Vector2[vertexPositions.Length];
        int t = 0;
        int o = 0;
        texCoords[t++] = new Vector2(1.0f, 0.0f);
        texCoords[t++] = new Vector2(1.0f, 1.0f);
        texCoords[t++] = new Vector2(0.5f, 0.0f);
        texCoords[t++] = new Vector2(0.5f, 1.0f);
        vertexOffsets[o++] = new Vector2(1.0f, -1.0f);
        vertexOffsets[o++] = new Vector2(1.0f, 1.0f);
        vertexOffsets[o++] = new Vector2(0.0f, -1.0f);
        vertexOffsets[o++] = new Vector2(0.0f, 1.0f);
        for (int i = 1; i < _lineVertices.Length - 1; ++i)
        {
            if ((i & 0x1) == 0x1)
            {
                texCoords[t++] = new Vector2(0.5f, 0.0f);
                texCoords[t++] = new Vector2(0.5f, 1.0f);
            }
            else
            {
                texCoords[t++] = new Vector2(0.5f, 0.0f);
                texCoords[t++] = new Vector2(0.5f, 1.0f);
            }
            vertexOffsets[o++] = new Vector2(0.0f, 1.0f);
            vertexOffsets[o++] = new Vector2(0.0f, -1.0f);
        }
        texCoords[t++] = new Vector2(0.5f, 0.0f);
        texCoords[t++] = new Vector2(0.5f, 1.0f);
        texCoords[t++] = new Vector2(0.0f, 0.0f);
        texCoords[t++] = new Vector2(0.0f, 1.0f);
        vertexOffsets[o++] = new Vector2(0.0f, 1.0f);
        vertexOffsets[o++] = new Vector2(0.0f, -1.0f);
        vertexOffsets[o++] = new Vector2(1.0f, 1.0f);
        vertexOffsets[o++] = new Vector2(1.0f, -1.0f);


        // fill previous and next positions
        Vector3[] prevPositions = new Vector3[vertexPositions.Length];
        Vector4[] nextPositions = new Vector4[vertexPositions.Length];
        int p = 0;
        int n = 0;
        prevPositions[p++] = _lineVertices[1];
        prevPositions[p++] = _lineVertices[1];
        prevPositions[p++] = _lineVertices[1];
        prevPositions[p++] = _lineVertices[1];
        nextPositions[n++] = _lineVertices[1];
        nextPositions[n++] = _lineVertices[1];
        nextPositions[n++] = _lineVertices[1];
        nextPositions[n++] = _lineVertices[1];
        for (int i = 1; i < _lineVertices.Length - 1; ++i)
        {
            prevPositions[p++] = _lineVertices[i - 1];
            prevPositions[p++] = _lineVertices[i - 1];
            nextPositions[n++] = _lineVertices[i + 1];
            nextPositions[n++] = _lineVertices[i + 1];
        }
        prevPositions[p++] = _lineVertices[_lineVertices.Length - 2];
        prevPositions[p++] = _lineVertices[_lineVertices.Length - 2];
        prevPositions[p++] = _lineVertices[_lineVertices.Length - 2];
        prevPositions[p++] = _lineVertices[_lineVertices.Length - 2];
        nextPositions[n++] = _lineVertices[_lineVertices.Length - 2];
        nextPositions[n++] = _lineVertices[_lineVertices.Length - 2];
        nextPositions[n++] = _lineVertices[_lineVertices.Length - 2];
        nextPositions[n++] = _lineVertices[_lineVertices.Length - 2];

        _mesh.SetIndices(null, MeshTopology.Triangles, 0); // Reset before setting again to prevent a unity error message.
        _mesh.vertices = vertexPositions;
        _mesh.normals = prevPositions;
        _mesh.tangents = nextPositions;
        _mesh.uv = texCoords;
        _mesh.uv2 = vertexOffsets;
        _mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        return UpdateBounds();
    }
    #endregion
    #region event functions

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        if (null == _lineVertices)
        {
            return;
        }
        for (int i = 0; i < _lineVertices.Length - 1; ++i)
        {
            Gizmos.DrawLine(gameObject.transform.TransformPoint(_lineVertices[i]), gameObject.transform.TransformPoint(_lineVertices[i + 1]));
        }
    }
#endif

    void Start()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _mesh = new Mesh();
        _meshFilter.mesh = _mesh;
        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.material = templateMaterial;
        _material = mr.material;
        BuildMeshFromVertices(_lineVertices);
        // Set Properties
    }
    #endregion
}
