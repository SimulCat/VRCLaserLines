
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]

public class LaserVectorLine : UdonSharpBehaviour
{
    // Used to compute the average value of all the Vector3's components:
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

    /// <summary>
    /// Line Length
    /// </summary>
    [SerializeField, FieldChangeCallback(nameof(LineLength))]
    private float lineLength = 0.5f;
    public float LineLength
    {
        get => lineLength;
        set
        {
            //Debug.Log(string.Format("{0}: lineLength {1:F2}", gameObject.name, value));
            lineLength = value;
            SetStartAndEndPoints();
        }
    }

    [SerializeField, Range(0f, 0.1f), FieldChangeCallback(nameof(LineWidth))]
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
            //UpdateShaft();
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

    #region properties

    [SerializeField, FieldChangeCallback(nameof(ThetaDegrees))]
    private float thetaDegrees = 0;
    public float ThetaDegrees
    {
        get => thetaDegrees;
        set
        {
            float thetaRad = thetaDegrees * Mathf.Deg2Rad;
            thetaDegrees = value;
            transform.localRotation = Quaternion.Euler(0, 0, thetaDegrees);
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

    public float Alpha
    {
        get => alpha;
        set
        {
            //Debug.Log(string.Format("{0}: alpha {1:F2}", gameObject.name, value));
            alpha = value;
            if (_material != null)
                _material.SetFloat("_Intensity", alpha);
        }
    }

    [SerializeField]
    Vector2 barbLengths = new Vector2(0.05f, 0.05f);
    [SerializeField]
    Vector2 barbAngles = new Vector2(30f, -25f);

    [SerializeField, FieldChangeCallback(nameof(ShowTip))]
    private bool showTip = true;
    public bool ShowTip
    {
        get => showTip;
        set
        {
            if (showTip != value)
            {
                showTip = value;
                //RefreshTips();
            }
        }
    }

    [SerializeField, Range(0f, 1f), Tooltip("Slide pointer position along shaft"), FieldChangeCallback(nameof(TipLocation))]
    private float tipLocation;
    public float TipLocation
    {
        get => tipLocation;
        set
        {
            if (tipLocation != value)
            {
                tipLocation = value;
                //UpdateTipLocations();
            }
        }
    }
    [SerializeField,FieldChangeCallback(nameof(IsIncoming))]
    private bool isIncoming = false;

    public bool IsIncoming
    {
        get => isIncoming;
        set
        {
            isIncoming = value;
            SetStartAndEndPoints();
        }
    }
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
    //[SerializeField]
    private Vector3[] _starts;
    //[SerializeField]
    private Vector3[] _ends;
    //[SerializeField]
    private Vector3 _tipPos = Vector3.right;

    #endregion
    #region mesh calculations
    private Bounds CalculateBounds()
    {
        var maxWidth = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        var scaledLineWidth = maxWidth * LineWidth * 0.5f;

        var min = new Vector3(
            Mathf.Min(_starts[0].x, _ends[0].x) - scaledLineWidth,
            Mathf.Min(_starts[0].y, _ends[0].y) - scaledLineWidth,
            Mathf.Min(_starts[0].z, _ends[0].z) - scaledLineWidth
        );
        var max = new Vector3(
            Mathf.Max(_starts[0].x, _ends[0].x) + scaledLineWidth,
            Mathf.Max(_starts[0].y, _ends[0].y) + scaledLineWidth,
            Mathf.Max(_starts[0].z, _ends[0].z) + scaledLineWidth
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
    public void SetStartAndEndPoints()
    {
        int lineCount = showTip ? 3 : 1;
        _starts = new Vector3[3];
        _ends = new Vector3[3];
        _starts[0] = isIncoming ? Vector3.left * lineLength : Vector3.zero;
        _ends[0] = isIncoming ?  Vector3.zero : Vector3.right * lineLength;
        _tipPos = (isIncoming ? (Vector3.left * (1-tipLocation)) : (Vector3.right * tipLocation)) * lineLength;
        _starts[1] = _tipPos;
        float radians = barbAngles[0]*Mathf.Deg2Rad;
        Vector3 offset = new Vector2(-Mathf.Cos(radians),Mathf.Sin(radians));
        _ends[1] = _tipPos + offset*barbLengths[0];
        _starts[2] = _tipPos;
        radians = barbAngles[1] * Mathf.Deg2Rad;
        offset = new Vector2(-Mathf.Cos(radians), Mathf.Sin(radians));
        _ends[2] = _tipPos + offset * barbLengths[1];
        // float theta = thetaDegrees*Mathf.Deg2Rad;
        vertexCount = lineCount * 8;// * (showTip ? 3 : 1);
        if (vertexCount != prevVertexCount)
        {
            _vertexPositions = new Vector3[vertexCount];
            _otherPositions = new Vector3[vertexCount];
        }
        int vertIdx = 0;
        for (int i = 0; i < lineCount; i++)
            vertIdx = appendVertices(vertIdx,_starts[i], _ends[i]);
        if (prevVertexCount != vertexCount)
            _mesh.Clear();
        _mesh.vertices = _vertexPositions;
        _mesh.normals = _otherPositions;
        UpdateBounds();
        if (prevVertexCount != vertexCount)
            initUVs(vertexCount);
        prevVertexCount = vertexCount;
    }

    private bool initUVs(int numVertices)
    {
        if (_mesh == null)
            return false;
        Vector2[] uvs = new Vector2[numVertices];
        Vector2[] uv2 = new Vector2[numVertices];
        int lineCount = showTip ? 3 : 1;
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
            indices [idx++] = offs + 6; indices[idx++] = offs + 5; indices[idx++] = offs + 7;
        }
        _mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        return true;
    }

    /// <summary>
    /// Calculates the (approximated) _LineScale factor based on the object's scale.
    /// </summary>
    private float CalculateLineScale()
    {
        return Vector3.Dot(transform.lossyScale, Average);
    }

    private void UpdateLineScale()
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
            _material.color = lineColour;
            _material.SetFloat("_Intensity", alpha);
            _material.SetFloat("_LineWidth", lineWidth);
            if (_hasSaberEffect)
                _material.SetFloat("_LightSaberFactor", _lightSaberEffect);
            UpdateLineScale();
        }
    }


    #endregion
    #region events

#if UNITY_EDITOR
    private void OnValidate()
        {
        SetAllMaterialProperties(); 
        }
    #endif

    private void Start()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _mesh = new Mesh();
        _meshFilter.mesh = _mesh;
        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.material = templateMaterial;
        _material = mr.material;
        ThetaDegrees = thetaDegrees;
        SetStartAndEndPoints();
        SetAllMaterialProperties();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        Gizmos.DrawLine(gameObject.transform.TransformPoint(Vector3.zero), gameObject.transform.TransformPoint(new Vector3(lineLength,0,0)));
    }

    #endregion
}
