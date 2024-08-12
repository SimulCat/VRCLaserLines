
using UdonSharp;
using UnityEngine;
using VolumetricLines;
using VRC.SDKBase;
using VRC.Udon;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]

public class UdonVolumetricLine : UdonSharpBehaviour
{
    // Used to compute the average value of all the Vector3's components:
    private readonly Vector3 Average = new Vector3(1f / 3f, 1f / 3f, 1f / 3f);

    #region private variables
    /// <summary>
    /// The start position relative to the GameObject's origin
    /// </summary>
    [SerializeField]
    private Vector3 m_startPos;

    /// <summary>
    /// The end position relative to the GameObject's origin
    /// </summary>
    [SerializeField]
    private Vector3 m_endPos = new Vector3(0f, 0f, 100f);

    /// <summary>
    /// Line Color
    /// </summary>
    [SerializeField]
    private Color m_lineColor;

    /// <summary>
    /// The width of the line
    /// </summary>
    [SerializeField,FieldChangeCallback(nameof(LineWidth))]
    private float m_lineWidth;

    /// <summary>
    /// Light saber factor
    /// </summary>
    [SerializeField]
    [Range(0.0f, 1.0f), FieldChangeCallback(nameof(LightSaberFactor))]
    private float m_lightSaberFactor;

    [SerializeField]
    private Material templateMaterial;

    /// <summary>
    /// This GameObject's specific material
    /// </summary>
    [SerializeField]
    private Material material;

    /// <summary>
    /// This GameObject's mesh filter
    /// </summary>
    private MeshFilter mf;
    private Mesh mesh;
    [SerializeField]
    private bool iHaveComponents = false;
    #endregion

    #region properties

    /// <summary>
    /// Get or set the line color of this volumetric line's material
    /// </summary>
    public Color LineColor
    {
        get { return m_lineColor; }
        set
        {
            if (material != null)
            {
                m_lineColor = value;
                material.color = m_lineColor;
            }
        }
    }

    /// <summary>
    /// Get or set the line width of this volumetric line's material
    /// </summary>
    public float LineWidth
    {
        get { return m_lineWidth; }
        set
        {
            if (material != null)
            {
                m_lineWidth = value;
                material.SetFloat("_LineWidth", m_lineWidth);
            }
            UpdateBounds();
        }
    }

    /// <summary>
    /// Get or set the light saber factor of this volumetric line's material
    /// </summary>
    public float LightSaberFactor
    {
        get { return m_lightSaberFactor; }
        set
        {
            if (material != null)
            {
                m_lightSaberFactor = value;
                material.SetFloat("_LightSaberFactor", m_lightSaberFactor);
            }
        }
    }

    /// <summary>
    /// Get or set the start position of this volumetric line's mesh
    /// </summary>
    public Vector3 StartPos
    {
        get { return m_startPos; }
        set
        {
            m_startPos = value;
            SetStartAndEndPoints(m_startPos, m_endPos);
        }
    }

    /// <summary>
    /// Get or set the end position of this volumetric line's mesh
    /// </summary>
    public Vector3 EndPos
    {
        get { return m_endPos; }
        set
        {
            m_endPos = value;
            SetStartAndEndPoints(m_startPos, m_endPos);
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
        if (material != null)
        {
            material.SetFloat("_LineScale", CalculateLineScale());
        }
    }

    /// <summary>
    /// Sets all material properties (color, width, light saber factor, start-, endpos)
    /// </summary>
    private void SetAllMaterialProperties()
    {
        SetStartAndEndPoints(m_startPos, m_endPos);

        if (material != null)
        {
            material.color = m_lineColor;
            material.SetFloat("_LineWidth", m_lineWidth);
            material.SetFloat("_LightSaberFactor", m_lightSaberFactor);
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
            Mathf.Min(m_startPos.x, m_endPos.x) - scaledLineWidth,
            Mathf.Min(m_startPos.y, m_endPos.y) - scaledLineWidth,
            Mathf.Min(m_startPos.z, m_endPos.z) - scaledLineWidth
        );
        var max = new Vector3(
            Mathf.Max(m_startPos.x, m_endPos.x) + scaledLineWidth,
            Mathf.Max(m_startPos.y, m_endPos.y) + scaledLineWidth,
            Mathf.Max(m_startPos.z, m_endPos.z) + scaledLineWidth
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
        if (null != mf)
        {
            var mesh = mf.sharedMesh;
            if (null != mesh)
            {
                mesh.bounds = CalculateBounds();
            }
        }
    }

    /// <summary>
    /// Sets the start and end points - updates the data of the Mesh.
    /// </summary>
    public void SetStartAndEndPoints(Vector3 startPoint, Vector3 endPoint)
    {
        m_startPos = startPoint;
        m_endPos = endPoint;

        Vector3[] vertexPositions = {
                m_startPos,
                m_startPos,
                m_startPos,
                m_startPos,
                m_endPos,
                m_endPos,
                m_endPos,
                m_endPos,
            };

        Vector3[] other = {
                m_endPos,
                m_endPos,
                m_endPos,
                m_endPos,
                m_startPos,
                m_startPos,
                m_startPos,
                m_startPos,
            };

        mesh.vertices = vertexPositions;
        mesh.normals = other;
        UpdateBounds();
    }
    #endregion

    #region event functions

    private bool initUVs(Mesh mesh)
    {
        Vector2[] uvs = new Vector2[8];
        uvs[0] = new Vector2(1.0f, 1.0f);
        uvs[1] = new Vector2(1.0f, 0.0f);
        uvs[2] = new Vector2(0.5f, 1.0f);
        uvs[3] = new Vector2(0.5f, 0.0f);
        uvs[4] = new Vector2(0.5f, 0.0f);
        uvs[5] = new Vector2(0.5f, 1.0f);
        uvs[6] = new Vector2(0.0f, 0.0f);
        uvs[7] = new Vector2(0.0f, 1.0f);
        mesh.uv = uvs;

        Vector2[] uv2 = new Vector2[8];
        uv2[0] = new Vector2(1.0f, 1.0f);
        uv2[1] = new Vector2(1.0f, -1.0f);
        uv2[2] = new Vector2(0.0f, 1.0f);
        uv2[3] = new Vector2(0.0f, -1.0f);
        uv2[4] = new Vector2(0.0f, 1.0f);
        uv2[5] = new Vector2(0.0f, -1.0f);
        uv2[6] = new Vector2(1.0f, 1.0f);
        uv2[7] = new Vector2(1.0f, -1.0f);

        mesh.uv2 = uv2;
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
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        return true;
    }


    void Start()
    {
        mf = GetComponent<MeshFilter>();
        mesh = new Mesh();
        mf.mesh = mesh;
        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.material = templateMaterial;
        material = mr.material;
        SetStartAndEndPoints(m_startPos, m_endPos);
        initUVs(mesh);
        SetAllMaterialProperties();
    }

    /* need to port this to Udon?
    void OnDestroy()
    {
        if (null != mf)
        {
            if (Application.isPlaying)
            {
                Mesh.Destroy(mf.sharedMesh);
            }
            else // avoid "may not be called from edit mode" error
            {
                Mesh.DestroyImmediate(mf.sharedMesh);
            }
            mf.sharedMesh = null;
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
            Gizmos.DrawLine(gameObject.transform.TransformPoint(m_startPos), gameObject.transform.TransformPoint(m_endPos));
        }
 
#endif

#endregion
}
