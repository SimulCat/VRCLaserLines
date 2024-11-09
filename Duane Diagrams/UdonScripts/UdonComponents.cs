using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)] // Keeps performance up
public class UdonComponents : UdonSharpBehaviour
{
    [Header("Lattice Diagram")]
    [SerializeField]
    UdonBehaviour latticeGrid;

    [Header("Vector Pointers")]
    [SerializeField] UdonBehaviour Incident;
    [SerializeField] UdonBehaviour Exit;
    [SerializeField] UdonBehaviour DeltaVec;
    [SerializeField]
    UdonBehaviour momentumX;
    [SerializeField]
    UdonBehaviour momentumY;
    [Header("Ewald Circle Prefab"), SerializeField]
    UdonBehaviour ewaldCircle;
    [Header("Control Settings")]

    [Range(0f, 1f),FieldChangeCallback(nameof(OverlayShift))]
    public float overlayShift = 0;
    public float OverlayShift 
    { 
        get => overlayShift;
        set
        {
            if (overlayShift != value)
            {
                overlayShift = value;
                UpdateOverlay();
            }
        }
    }

    [Header("UI Controls")]
    [SerializeField]
    SyncedSlider IncidentControl;
    bool isInitialized = false;
    [SerializeField, FieldChangeCallback(nameof(IncidentTheta))]
    public float incidentTheta;
    private float IncidentTheta
    {
        get => incidentTheta;
        set
        {
            if (isInitialized)
            {
                incidentTheta = value;
                if (Incident != null)
                {
                    Incident.SetProgramVariable("thetaDegrees",incidentTheta);
                    CalcDelta();
                }
                RequestSerialization();
            }
        }
    }

    [SerializeField]
    SyncedSlider ExitControl;
    [SerializeField, FieldChangeCallback(nameof(ExitTheta))]
    public float exitTheta;
    private float ExitTheta
    {
        get => exitTheta;
        set
        {
            if (isInitialized)
            {
                exitTheta = value;
                if (Exit != null)
                {
                    Exit.SetProgramVariable("thetaDegrees",exitTheta);
                    CalcDelta();
                }
            }
        }
    }
    [SerializeField]
    SyncedSlider RotationControl;

    [SerializeField, FieldChangeCallback(nameof(LatticeRotation))]
    public float latticeRotation;

    private Transform latticeTransform;

    public float LatticeRotation { 
        get => latticeRotation; 
        set 
        {
            if (latticeRotation != value)
            {
                latticeRotation = value;
                if (latticeTransform!= null)
                {
                    latticeTransform.localRotation = Quaternion.Euler(0,0,latticeRotation);
                }
                CalcDelta();
            }
        } 
    }

    [SerializeField] private bool showEwald = true;
    [SerializeField, UdonSynced,FieldChangeCallback(nameof(ShowDelta))]
    bool showDelta;
    bool ShowDelta 
    { 
        get => showDelta;
        set
        {
            showDelta = value;
            RequestSerialization();
        }  
    }
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(ShowMY))]
    bool showMY;
    bool ShowMY
    {
        get => showMY;
        set
        {
            showMY = value;
            RequestSerialization();
            if (momentumY != null)
                momentumY.gameObject.SetActive(value);
        }
    }
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(ShowMX))]
    bool showMX;
    bool ShowMX
    {
        get => showMX;
        set
        {
            showMX = value;
            if (momentumX != null)
            {
                momentumX.gameObject.SetActive(value);
            }
            RequestSerialization();
        }
    }

    [SerializeField]
    Vector2 deltaVec = Vector2.up;
    [SerializeField] 
    Vector2 exitVec = Vector2.right;
    [SerializeField]
    Vector2 incidentVec = Vector2.left;
    [SerializeField]
    private float lineLength = 0.77f;

    private void UpdateOverlay()
    {
        Vector2 DeltaOrigin = Vector2.Lerp(Vector2.zero, exitVec, overlayShift);
        Vector2 IncidentOrigin = Vector2.Lerp(Vector2.zero, incidentVec, overlayShift);
        if (DeltaVec != null)
            DeltaVec.transform.localPosition= DeltaOrigin;
        if (Incident != null)
            Incident.transform.localPosition= IncidentOrigin;
    }
    private void CalcDelta()
    {

        float thetaI = incidentTheta * Mathf.Deg2Rad;
        float thetaE = exitTheta * Mathf.Deg2Rad;
        incidentVec = new Vector2(Mathf.Cos(thetaI), Mathf.Sin(thetaI)) * lineLength;
        exitVec = new Vector2(Mathf.Cos(thetaE), Mathf.Sin(thetaE)) * lineLength;
        deltaVec = incidentVec - exitVec;
        float deltaLen = deltaVec.magnitude;
        float deltaTheta = Mathf.Atan2(deltaVec.y, deltaVec.x) * Mathf.Rad2Deg;
        float thetaLattice = latticeRotation * Mathf.Deg2Rad;
        float cosTheta = Mathf.Cos(-thetaLattice);
        float sinTheta = Mathf.Sin(-thetaLattice);
        Vector2 deltaLattice = new Vector2(cosTheta*deltaVec.x - sinTheta*deltaVec.y,sinTheta*deltaVec.x+ cosTheta*deltaVec.y);
        if (DeltaVec != null) 
        {
            DeltaVec.SetProgramVariable("lineLength",deltaLen);
            DeltaVec.SetProgramVariable("thetaDegrees",deltaTheta);
            DeltaVec.SetProgramVariable("alpha",deltaLen > 0 ? 1f : 0f);
        }
        if (momentumX != null)
        {
            momentumX.SetProgramVariable("thetaDegrees",(deltaLattice.x >= 0 ? 0f : 180f) + latticeRotation);
            momentumX.SetProgramVariable("lineLength",Mathf.Abs(deltaLattice.x));
            momentumX.SetProgramVariable("alpha",deltaLattice.x != 0 ? 1f : 0f);
        }
        if (momentumY != null)
        {
            momentumY.SetProgramVariable("thetaDegrees",(deltaLattice.y >= 0 ? 90f : -90f) + latticeRotation);
            momentumY.SetProgramVariable("lineLength",Mathf.Abs(deltaLattice.y));
            momentumY.SetProgramVariable("alpha",deltaLattice.y != 0 ? 1f : 0f);
        }
        if (showEwald && ewaldCircle!=null)
        {
            ewaldCircle.SetProgramVariable("radius", lineLength);
        }

        UpdateOverlay();
    }

    void Start()
    {
        if (latticeGrid!= null)
        {
            latticeTransform = latticeGrid.transform;
        }
        if (Incident != null)
        {
            Incident.SetProgramVariable("thetaDegrees",incidentTheta);
            Incident.SetProgramVariable("lineLength",lineLength);
        }
        if (Exit != null)
        {
            Exit.SetProgramVariable("thetaDegrees",exitTheta);
            Exit.SetProgramVariable("lineLength", lineLength);
        }
        IncidentTheta = incidentTheta;
        if (IncidentControl != null)
            IncidentControl.SetValues(incidentTheta, -45, 45);
        ExitTheta = exitTheta;
        if (ExitControl != null)
            ExitControl.SetValues(exitTheta, -135, 135);
        LatticeRotation = latticeRotation;
        if (RotationControl != null)
            RotationControl.SetValues(0,-45,45);

        CalcDelta();
        isInitialized = true;
    }
}
