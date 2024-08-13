
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
    private Vector3[] m_lineVertices;
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
        get { return m_lineVertices; }
    }

    #endregion
    private bool configureMesh()
    {
        if (_mesh == null || _material == null)
            return false;
        //SetStartAndEndPoints(m_startPos, m_endPos);
        Vector2[] v2x8 = new Vector2[8];
        v2x8[0] = new Vector2(1.0f, 1.0f);
        v2x8[1] = new Vector2(1.0f, 0.0f);
        v2x8[2] = new Vector2(0.5f, 1.0f);
        v2x8[3] = new Vector2(0.5f, 0.0f);
        v2x8[4] = new Vector2(0.5f, 0.0f);
        v2x8[5] = new Vector2(0.5f, 1.0f);
        v2x8[6] = new Vector2(0.0f, 0.0f);
        v2x8[7] = new Vector2(0.0f, 1.0f);

        _mesh.uv = v2x8;
        v2x8[0] = new Vector2(1.0f, 1.0f);
        v2x8[1] = new Vector2(1.0f, -1.0f);
        v2x8[2] = new Vector2(0.0f, 1.0f);
        v2x8[3] = new Vector2(0.0f, -1.0f);
        v2x8[4] = new Vector2(0.0f, 1.0f);
        v2x8[5] = new Vector2(0.0f, -1.0f);
        v2x8[6] = new Vector2(1.0f, 1.0f);
        v2x8[7] = new Vector2(1.0f, -1.0f);

        _mesh.uv2 = v2x8;
        int[] indices = new int[18];
        // 2, 1, 0,
        indices[0] = 2; indices[1] = 1; indices[2] = 0;
        // 3, 1, 2,
        indices[3] = 3; indices[4] = 1; indices[2] = 2;
        // 4, 3, 2,
        indices[6] = 4; indices[7] = 3; indices[8] = 2;
        // 5, 4, 2,
        indices[9] = 5; indices[10] = 4; indices[11] = 2;
        // 4, 5, 6,
        indices[12] = 4; indices[13] = 5; indices[14] = 6;
        // 6, 5, 7
        indices[15] = 6; indices[16] = 5; indices[17] = 7;
        _mesh.SetIndices(indices, MeshTopology.Triangles, 0);
       // SetAllMaterialProperties();
        return true;
    }

    #region event functions

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        if (null == m_lineVertices)
        {
            return;
        }
        for (int i = 0; i < m_lineVertices.Length - 1; ++i)
        {
            Gizmos.DrawLine(gameObject.transform.TransformPoint(m_lineVertices[i]), gameObject.transform.TransformPoint(m_lineVertices[i + 1]));
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
        // First set up vertices
        // Then do UVs
        // Set Properties
    }
    #endregion
}
