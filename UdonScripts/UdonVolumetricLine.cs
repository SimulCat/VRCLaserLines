
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class UdonVolumetricLine : UdonSharpBehaviour
{
    // Used to compute the average value of all the Vector3's components:
    private readonly Vector3 Average = new Vector3(1f / 3f, 1f / 3f, 1f / 3f);

    #region private variables
    /// <summary>
    /// The start position relative to the GameObject's origin
    /// </summary>
    [SerializeField]
    private Vector3 _startPos;

    /// <summary>
    /// The end position relative to the GameObject's origin
    /// </summary>
    [SerializeField]
    private Vector3 _endPos = new Vector3(0f, 0f, 100f);

    /// <summary>
    /// Line Color
    /// </summary>
    [SerializeField]
    private Color _lineColor;

    /// <summary>
    /// The width of the line
    /// </summary>
    [SerializeField,FieldChangeCallback(nameof(LineWidth))]
    private float _lineWidth;

    /// <summary>
    /// Light saber factor
    /// </summary>
    [SerializeField]
    [Range(0.0f, 1.0f), FieldChangeCallback(nameof(LightSaberFactor))]
    private float _lightSaberEffect;
    [SerializeField]
    private bool _hasSaberEffect = false;

    /// <summary>
    /// Template material
    /// </summary>
    [SerializeField]
    private Material templateMaterial;

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
    [SerializeField]
    private bool iHaveComponents = false;
    #endregion

    #region properties

    /// <summary>
    /// Get or set the line color of this volumetric line's _material
    /// </summary>
    public Color LineColor
    {
        get { return _lineColor; }
        set
        {
            if (_material != null)
            {
                _lineColor = value;
                _material.color = _lineColor;
            }
        }
    }

    /// <summary>
    /// Get or set the line width of this volumetric line's _material
    /// </summary>
    public float LineWidth
    {
        get { return _lineWidth; }
        set
        {
            if (_material != null)
            {
                _lineWidth = value;
                _material.SetFloat("_LineWidth", _lineWidth);
            }
            UpdateBounds();
        }
    }

    /// <summary>
    /// Get or set the light saber factor of this volumetric line's _material
    /// </summary>
    public float LightSaberFactor
    {
        get { return _lightSaberEffect; }
        set
        {
            if (_material != null)
            {
                _lightSaberEffect = value;
                _material.SetFloat("_LightSaberFactor", _lightSaberEffect);
            }
        }
    }

    /// <summary>
    /// Get or set the start position of this volumetric line's _mesh
    /// </summary>
    public Vector3 StartPos
    {
        get { return _startPos; }
        set
        {
            _startPos = value;
            SetStartAndEndPoints(_startPos, _endPos);
        }
    }

    /// <summary>
    /// Get or set the end position of this volumetric line's _mesh
    /// </summary>
    public Vector3 EndPos
    {
        get { return _endPos; }
        set
        {
            _endPos = value;
            SetStartAndEndPoints(_startPos, _endPos);
        }
    }

    #endregion

    #region methods

    /// <summary>
    /// Calculates the (approximated) _LineScale factor based on the object's scale.
    /// </summary>
    private float CalculateLineScale()
    {
        return Vector3.Dot(transform.lossyScale, Average);
    }

    /// <summary>
    /// Updates the line scaling of this volumetric line based on the current object scaling.
    /// </summary>
    public void UpdateLineScale()
    {
        if (_material != null)
        {
            _material.SetFloat("_LineScale", CalculateLineScale());
        }
    }

    /// <summary>
    /// Sets all _material properties (color, width, light saber factor, start-, endpos)
    /// </summary>
    private void SetAllMaterialProperties()
    {
        if (_material != null)
        {
            _material.color = _lineColor;
            _material.SetFloat("_LineWidth", _lineWidth);
            if (_hasSaberEffect) 
                _material.SetFloat("_LightSaberFactor", _lightSaberEffect);
            UpdateLineScale();
        }
    }

    /// <summary>
    /// Calculate the bounds of this line based on start and end points,
    /// the line width, and the scaling of the object.
    /// </summary>
    private Bounds CalculateBounds()
    {
        var maxWidth = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        var scaledLineWidth = maxWidth * LineWidth * 0.5f;

        var min = new Vector3(
            Mathf.Min(_startPos.x, _endPos.x) - scaledLineWidth,
            Mathf.Min(_startPos.y, _endPos.y) - scaledLineWidth,
            Mathf.Min(_startPos.z, _endPos.z) - scaledLineWidth
        );
        var max = new Vector3(
            Mathf.Max(_startPos.x, _endPos.x) + scaledLineWidth,
            Mathf.Max(_startPos.y, _endPos.y) + scaledLineWidth,
            Mathf.Max(_startPos.z, _endPos.z) + scaledLineWidth
        );
        Bounds bounds = new Bounds();
        bounds.min = min;
        bounds.max = max;
        return bounds;
    }

    /// <summary>
    /// Updates the bounds of this line according to the current properties, 
    /// which there are: start point, end point, line width, scaling of the object.
    /// </summary>
    public void UpdateBounds()
    {
        if (_mesh != null)
        {
            _mesh.bounds = CalculateBounds();
        }
    }

    /// <summary>
    /// Sets the start and end points - updates the data of the Mesh.
    /// </summary>
    public void SetStartAndEndPoints(Vector3 startPoint, Vector3 endPoint)
    {
        _startPos = startPoint;
        _endPos = endPoint;

        Vector3[] vertexPositions = {
                _startPos,
                _startPos,
                _startPos,
                _startPos,
                _endPos,
                _endPos,
                _endPos,
                _endPos,
            };

        Vector3[] other = {
                _endPos,
                _endPos,
                _endPos,
                _endPos,
                _startPos,
                _startPos,
                _startPos,
                _startPos,
            };

        _mesh.vertices = vertexPositions;
        _mesh.normals = other;
        UpdateBounds();
    }
    #endregion

    #region event functions

    private bool initUVs(Mesh _mesh)
    {
        if (_mesh == null )
            return false;
        Vector2[] uvs = new Vector2[8];
        uvs[0] = new Vector2(1.0f, 1.0f);
        uvs[1] = new Vector2(1.0f, 0.0f);
        uvs[2] = new Vector2(0.5f, 1.0f);
        uvs[3] = new Vector2(0.5f, 0.0f);
        uvs[4] = new Vector2(0.5f, 0.0f);
        uvs[5] = new Vector2(0.5f, 1.0f);
        uvs[6] = new Vector2(0.0f, 0.0f);
        uvs[7] = new Vector2(0.0f, 1.0f);
        _mesh.uv = uvs;

        Vector2[] uv2 = new Vector2[8];
        uv2[0] = new Vector2(1.0f, 1.0f);
        uv2[1] = new Vector2(1.0f, -1.0f);
        uv2[2] = new Vector2(0.0f, 1.0f);
        uv2[3] = new Vector2(0.0f, -1.0f);
        uv2[4] = new Vector2(0.0f, 1.0f);
        uv2[5] = new Vector2(0.0f, -1.0f);
        uv2[6] = new Vector2(1.0f, 1.0f);
        uv2[7] = new Vector2(1.0f, -1.0f);

        _mesh.uv2 = uv2;
        int[] indices = new int[18];
        // 2, 1, 0,
        indices[0] = 2; indices[1] = 1; indices[2] = 0;
        // 3, 1, 2,
        indices[3] = 3; indices[4] = 1; indices[5] = 2;
        // 4, 3, 2,
        indices[6] = 4; indices[7] = 3; indices[8] = 2;
        // 5, 4, 2,
        indices[9] = 5; indices[10] = 4; indices[11] = 2;
        // 4, 5, 6,
        indices[12] = 4; indices[13] = 5; indices[14] = 6;
        // 6, 5, 7
        indices[15] = 6; indices[16] = 5; indices[17] = 7;
        _mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        return true;
    }


    void Start()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _mesh = new Mesh();
        _meshFilter.mesh = _mesh;
        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.material = templateMaterial;
        _material = mr.material;
        _hasSaberEffect = _material.HasProperty("_LightSaberFactor");
        SetStartAndEndPoints(_startPos, _endPos);
        initUVs(_mesh);
        SetAllMaterialProperties();
    }

    /* need to port this to Udon?
    void OnDestroy()
    {
        if (null != _meshFilter)
        {
            if (Application.isPlaying)
            {
                Mesh.Destroy(_meshFilter.sharedMesh);
            }
            else // avoid "may not be called from edit mode" error
            {
                Mesh.DestroyImmediate(_meshFilter.sharedMesh);
            }
            _meshFilter.sharedMesh = null;
        }
        DestroyMaterial();
    }
   */

    bool propertyCheck = false;
    void Update()
    {
        if (transform.hasChanged)
        {
            UpdateLineScale();
            UpdateBounds();
        }
        if (!propertyCheck)
            return;
        SetAllMaterialProperties();
        UpdateBounds();
    }

#if UNITY_EDITOR

    private void OnValidate()
    {
        propertyCheck = true;
    }

    void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(gameObject.transform.TransformPoint(_startPos), gameObject.transform.TransformPoint(_endPos));
        }
 
#endif

#endregion
}
