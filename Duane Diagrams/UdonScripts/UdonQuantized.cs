using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)] // Keeps performance up

public class UdonQuantized : UdonSharpBehaviour
{
    [Header("Lattice Diagram")]
    [SerializeField]
    UdonGrid2D theLattice;

    [SerializeField] SyncedSlider rotationSlider;

    [SerializeField, FieldChangeCallback(nameof(LatticeRotation))]
    private float latticeRotation;
    [Header("Reciprocal Diagram")]
    [SerializeField] UdonReciprocal2D theReciprocal;

    [SerializeField]
    UdonBehaviour Incident;
    [SerializeField]
    UdonBehaviour Exit;

    [Header("Scale")]
    [SerializeField, Range(0.01f, 0.5f)] private float angstromsToGridScale = 0.04f;
    //[SerializeField, Range(0.01f, 0.5f)] private float momentumToGridScale = 0.075f;
    [SerializeField] private float lineLength = 0.77f;

    [Header("UI Controls")]

    [SerializeField] SyncedSlider AngstromSliderA;

    [SerializeField,FieldChangeCallback(nameof(PitchAngstromsA))] 
    float pitchAngstromsA = 4.5f;
    float PitchAngstromsA
    {
        get => pitchAngstromsA;
        set
        {
            if (pitchAngstromsA != value)
            {
                RefreshGrids();
            }
            pitchAngstromsA = value;
            RequestSerialization();
        }
    }
    [SerializeField] SyncedSlider AngstromSliderB;

    [SerializeField, FieldChangeCallback(nameof(PitchAngstromsB))]
    float pitchAngstromsB = 3f;
    float PitchAngstromsB
    {
        get => pitchAngstromsB; 
        set
        {
            if (pitchAngstromsB != value)
            {
                RefreshGrids();
            }
            pitchAngstromsB = value;
        }
    }


    [SerializeField] SyncedSlider IncidentControl;
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
            }
        }
    }

    [SerializeField] SyncedSlider ExitControl;
    [SerializeField,FieldChangeCallback(nameof(ExitTheta))]
    public float exitTheta;
    public float ExitTheta
    {
        get => exitTheta;
        set
        {
            exitTheta = value;
            if (Exit != null)
            {
                Exit.SetProgramVariable("thetaDegrees",exitTheta);
                CalcDelta();
            }
        }
    }
    [SerializeField] SyncedSlider beamImpulseSlider;


    [SerializeField,FieldChangeCallback(nameof(BeamImpulse))]
    private float beamImpulse = 16;
    public float BeamImpulse
    {
        get => beamImpulse; 
        set
        {
            beamImpulse = value;
            CalcDelta();
            if (theReciprocal != null)
            {
                theReciprocal.BeamImpulse = value;
            }
        }
    }

    [SerializeField] private float maxBeamImpulse = 20;
    [SerializeField] private float minBeamImpulse = 5;
    [Header("Actually Constants")]

//    [SerializeField] float AngstromToP24 = 6.62607004f; // Converts 1/d (Angstroms) to units of momentum e-24.
//    [SerializeField] float photonKeVtoP24 = 0.534429723f;     // Converts KeV to units of momentum e-24.

    [Header("Calculated Values (Debug)")]

    private Transform latticeTransform;
    public float LatticeRotation
    {
        get => latticeRotation;
        set
        {
            if (latticeRotation != value)
            {
                latticeRotation = value;
                if (latticeTransform != null)
                    latticeTransform.localRotation = Quaternion.Euler(0, 0, latticeRotation);
                if (theReciprocal != null)
                    theReciprocal.LatticeRotation = value;
                CalcDelta();
            }
        }
    }

    private bool isInitialized = false;

    private void CalcDelta()
    {
        Incident.SetProgramVariable("lineLength",lineLength);
        Exit.SetProgramVariable("lineLength",lineLength);

        if (theReciprocal != null)
            theReciprocal.BeamAnglesDeg = new Vector2(incidentTheta, exitTheta);
    }
    private void RefreshGrids()
    {
        Vector2 gridSpacing = new Vector2(pitchAngstromsA, pitchAngstromsB);
        if (theReciprocal != null)
        {
            theReciprocal.LatticeAngstroms = gridSpacing;
        }
        gridSpacing *= angstromsToGridScale;
        if (theLattice != null)
        {
            theLattice.GridSpacing = gridSpacing;
        }
    }

    private void Update()
    {
        if (isInitialized)
            return;
        CalcDelta();
        isInitialized = true;
    }
    void Start()
    {
        if (theReciprocal != null)
        {
            theReciprocal.MinBeamImpulse = minBeamImpulse;
            theReciprocal.MaxBeamImpulse = maxBeamImpulse;
            theReciprocal.BeamImpulse = beamImpulse;
            theReciprocal.BeamAnglesDeg = new Vector2(incidentTheta, exitTheta);
            //momentumToGridScale = theReciprocal.momentumToGridScale;
        }
        if (theLattice != null)
            latticeTransform = theLattice.transform;
        if (Incident != null)
        {
            Incident.SetProgramVariable("thetaDegrees",incidentTheta);
            Incident.SetProgramVariable("lineLength",lineLength);
        }
        if (Exit != null)
        {
            Exit.SetProgramVariable("thetaDegrees",exitTheta);
            Exit.SetProgramVariable("lineLength",lineLength);
        }

        LatticeRotation = latticeRotation;
        if (rotationSlider != null)
            rotationSlider.SetValues(latticeRotation, -90f, 90f);
        PitchAngstromsA = pitchAngstromsA;
        if (AngstromSliderA != null)
            AngstromSliderA.SetValues(pitchAngstromsA, 2f, 4.5f);
        PitchAngstromsB = pitchAngstromsB;
        if (AngstromSliderB != null)
            AngstromSliderB.SetValues(pitchAngstromsB, 2f, 4.5f);
        IncidentTheta = incidentTheta;
        if (IncidentControl != null)
            IncidentControl.SetValues(incidentTheta, -67.5f, 67.5f);
        ExitTheta = exitTheta;
        if (ExitControl != null)
            ExitControl.SetValues(exitTheta, -67.5f, 67.5f);
        BeamImpulse = beamImpulse;
        if (beamImpulseSlider != null)
            beamImpulseSlider.SetValues(beamImpulse, minBeamImpulse, maxBeamImpulse);
        isInitialized = false;
    }

}
