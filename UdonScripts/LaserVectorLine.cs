
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Wrapper.Modules;

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
            if (lineLength != value)
            {
                lineLength = value;
                SetStartAndEndPoints();
            }
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

    [SerializeField,FieldChangeCallback(nameof(ThetaDegrees))]
    private float thetaDegrees = 0;
    public float ThetaDegrees
    {
        get => thetaDegrees;
        set
        {
            float thetaRad = thetaDegrees * Mathf.Deg2Rad;
            //Debug.Log(string.Format("{0}: thetaDegrees {1:F2}", gameObject.name,value));
            thetaDegrees = value;
            SetStartAndEndPoints();
        }
    }

    [SerializeField,FieldChangeCallback(nameof(LineColour))]
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
    [SerializeField]
    private Vector3 _startPos = Vector3.zero;
    [SerializeField]
    private Vector3 _endPos = Vector3.right; 
    #endregion
    #region mesh calculations
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
    /// 
    public void SetStartAndEndPoints()
    {
        _startPos = Vector3.zero;
        float theta = thetaDegrees*Mathf.Deg2Rad;
        _endPos = new Vector3(Mathf.Cos(theta), Mathf.Sin(theta),0);
        _endPos *= lineLength;

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

    private bool initUVs(Mesh _mesh)
    {
        if (_mesh == null)
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

    /// <summary>
    /// Calculates the (approximated) _LineScale factor based on the object's scale.
    /// </summary>
    private float CalculateLineScale()
    {
        return Vector3.Dot(transform.lossyScale, Average);
    }

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
        SetStartAndEndPoints();
        initUVs(_mesh);
        SetAllMaterialProperties();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        Gizmos.DrawLine(gameObject.transform.TransformPoint(Vector3.zero), gameObject.transform.TransformPoint(new Vector3(lineLength,0,0)));
    }

    #endregion
}
