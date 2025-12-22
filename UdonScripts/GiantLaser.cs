
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GiantLaser : UdonSharpBehaviour
{
    [Tooltip("Laser Start Point transform.")]
    public Transform StartPoint;

    [Tooltip("Physics Raycast Masking.")]
    public LayerMask LaserMask = 1;

    [Tooltip("Show Beam")]
    [SerializeField] bool laserOn = true;
    public bool LaserOn
    {
        get => laserOn;
        set
        {
            if (laserOn != value)
            {
                laserOn = value;
                beamChanged = true;
                colorChanged = true;
            }
        }
    }

    public Renderer[] neonSpirals;

    [SerializeField, ColorUsage(true, true)]
    Color laserColor = Color.yellow;
    private bool colorChanged = false;

    public Color LaserColor
    {
        get => laserColor;
        set
        {
            if (laserColor != value)
            {
                colorChanged = true;
                laserColor = value;
            }
            beamChanged |= colorChanged;
        }

    }

    [Tooltip("Width of Laser.")]
    public float beamThickness = 1.0f;

    [Tooltip("Maximum distance of Laser.")]
    public float LaserDist = 20.0f;

    [SerializeField] LaserVectorLine beamRenderer;
    //private float ViewAngle;
    private bool beamChanged = true;

    private void Awake()
    {
        if (StartPoint == null)
        {
            if (beamRenderer != null)
                StartPoint = beamRenderer.transform;
            else
                StartPoint = gameObject.transform;
        }
    }
    void Start()
    {
        LaserColor = laserColor;
        colorChanged = true;
        beamChanged = true;
    }//end start
     /////////////////////////////////////
    void LateUpdate()
    {
        if (beamChanged)
        {
            beamChanged = false;
            if (colorChanged)
            {
                if (neonSpirals != null)
                {
                    foreach (Renderer rSpiral in neonSpirals)
                    {
                        if (rSpiral != null)
                            rSpiral.material.color = LaserColor * 1.5f;
                    }
                }
                //Flare Control
                Color noAlpha = LaserColor;
                noAlpha.a = 1;

                if (beamRenderer != null)
                {
                    if (laserOn)
                    {
                        // beamRenderer.Thickness = beamThickness;
                        Color lineColor = LaserColor * 1.5f;
                        lineColor.a = 0.1f;
                        beamRenderer.LineColour = lineColor;
                        //Vector3 NewRay = Vector3(ray.GetPoint);
                    }
                    else
                    {
                        beamRenderer.LineColour = Color.clear;
                    }
                }
                colorChanged = false;
            }
        }//end Laser On   
    }//end Update
} 

