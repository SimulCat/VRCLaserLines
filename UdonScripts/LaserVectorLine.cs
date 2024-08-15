
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using static UnityEngine.Rendering.DebugUI;
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]

public class LaserVectorLine : UdonSharpBehaviour
{
    #region udon callback
    /// <summary>
    /// Light saber factor
    /// </summary>
    [SerializeField]
    [Range(0.0f, 1.0f), FieldChangeCallback(nameof(LightSaberFactor))]
    private float _lightSaberEffect;
    [SerializeField]
    private bool _hasSaberEffect = false;

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
    [SerializeField]
    private float lineLength = 0.5f;
    public float LineLength
    {
        get => lineLength;
        set
        {
            if (lineLength != value)
            {
                lineLength = value;
                //UpdateShaft();
                //UpdateTipLocations();
            }
        }
    }
    [SerializeField, Range(0f, 0.1f)]
    private float lineWidth = 0.03f;

    public float LineWidth
    {
        get => lineWidth;
        set
        {
            lineWidth = value;
            //UpdateShaft();
        }
    }

    [SerializeField]
    private float thetaDegrees = 0;
    public float ThetaDegrees
    {
        get => thetaDegrees;
        set
        {
            thetaDegrees = value;
            transform.localRotation = Quaternion.Euler(0, 0, thetaDegrees);
        }
    }
    [SerializeField]
    public Color lineColour = Color.cyan;
    private Color currentColour = Color.white;

    public Color LineColour
    {
        get => lineColour;
        set
        {
            if (lineColour != value)
            {
                lineColour = value;
                //RefreshColours();
            }
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
    /// Gets or sets the tmplate material.
    /// Setting this will only have an impact once. 
    /// Subsequent changes will be ignored.
    /// </summary>
    public Material TemplateMaterial
    {
        get { return templateMaterial; }
        set { templateMaterial = value; }
    }

    #endregion
    #region events

    #if UNITY_EDITOR
        private void OnValidate()
        {
                
        }
    #endif

    private void Start()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _mesh = GetComponent<Mesh>();
        
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        float thetaRadians = thetaDegrees * Mathf.Deg2Rad;

        Vector3 endPoint = new Vector3(Mathf.Cos(thetaRadians), Mathf.Sin(thetaRadians), 0)*lineLength;
        Vector3 startPoint = Vector3.zero;
        Gizmos.DrawLine(gameObject.transform.TransformPoint(startPoint), gameObject.transform.TransformPoint(endPoint));
    }

    #endregion
}
