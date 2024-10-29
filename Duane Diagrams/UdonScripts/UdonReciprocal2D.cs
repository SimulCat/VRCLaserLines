using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)] // Keeps performance up

public class UdonReciprocal2D : UdonSharpBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Line Prototype")]
    [SerializeField]
    GameObject linePrefab;
    [Header("Scales")]
    public float momentumToGridScale = 0.075f;
    [Header("Style")]
    [SerializeField, Range(0f, 0.1f)] private float pointSize = 0.02f;
    [SerializeField, Range(0f, 0.1f)] private float lineThickness = 0.02f;

    [SerializeField] private float impulseWidth = 0.5f;
    [Header("Display Control")]
    [Range(0f, 1f), UdonSynced, FieldChangeCallback(nameof(OverlayShift))]
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
    private bool iamOwner = false;
    private VRCPlayerApi player;

    [SerializeField]
    private bool showPoints = true;
   // [SerializeField]
   // private bool showGridLines = true;
    [SerializeField]
    private bool showEwald = true;
    [SerializeField]
    private bool showReactions = true;
    
    [SerializeField,UdonSynced,FieldChangeCallback(nameof(ShowComponents))]
    public bool showComponents = true;
    [SerializeField] Toggle togComponents;
    private bool ShowComponents
    {
        get => showComponents;
        set
        {
            showComponents = value;
            if (momentumX != null)
                momentumX.gameObject.SetActive(showComponents);
            if (momentumY != null)
                momentumY.gameObject.SetActive(showComponents);
            if (togComponents != null && togComponents.isOn != value) 
                togComponents.SetIsOnWithoutNotify(true);
            RequestSerialization();
        }
    }

    public void onComponentTog()
    {
        if (togComponents != null)
        {
            bool updated = togComponents.isOn;
            if (updated == showComponents)
                return;
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            ShowComponents = updated;
        }
    }

    [Header("Lattice Dimensions and rotation")]
    [SerializeField]private Vector2 latticeAngstroms;
    public Vector2 LatticeAngstroms 
    {  
        get =>  latticeAngstroms; 

        set 
        {
            if (value != latticeAngstroms)
                latticeChanged = true;
            latticeAngstroms = value;
        }
    }
    [SerializeField] private float latticeRotation = 0;
    public float LatticeRotation
    {
        get => latticeRotation;
        set 
        {
            if ( value != latticeRotation)
                latticeChanged = true;
            if (gridTransform != null)
                gridTransform.localRotation = Quaternion.Euler(0, 0, latticeRotation);

            latticeRotation = value;
        }
    }

    [Header("Beam Energy Settings")]

    [SerializeField] private float beamImpulse = 42;
    public float BeamImpulse
    {
        get => beamImpulse;
        set
        {
            beamImpulse = value;
            beamChanged = true;
        }
    }
    [SerializeField] private float maxBeamImpulse = 48;
    public float MaxBeamImpulse 
    {
        get => maxBeamImpulse;
        set 
        {
            maxBeamImpulse = value;
            latticeChanged = true;
            beamChanged = true;
        }
    }
    [SerializeField] private float minBeamImpulse = 5;
    public float MinBeamImpulse
    {
        get => minBeamImpulse;
        set
        {
            if (minBeamImpulse != value)
            {
                minBeamImpulse = value;
                latticeChanged = true;
                beamChanged = true;
            }
        }
    }
    [SerializeField] private Vector2 beamAnglesDeg = new Vector2(0, 0);
    Vector2 beamAngles;
    public Vector2 BeamAnglesDeg
    {
        get => beamAnglesDeg;
        set
        {
            beamChanged = true;
            beamAnglesDeg = value;
            beamAngles = beamAnglesDeg * Mathf.Deg2Rad;
        }
    }


    [ColorUsage(true, true)]
    [SerializeField] private Color lineColor = Color.black;
    [ColorUsage(true, true)]
    [SerializeField] private Color beamColour = Color.magenta;
    //[Header("Constants")]
    float AngstromToP24 = 6.62607015f; // Converts 1/d (Angstroms) to units of momentum e-24.
    //float photonKeVtoP24 = 0.534429723f;     // Converts KeV to units of momentum e-24.
    [Header("Pointer Prefabs")]
    [SerializeField] UdonBehaviour incidentPointer;
    [SerializeField] UdonBehaviour exitPointer;
    Transform exitLocation;
    [SerializeField] TextMeshProUGUI momentumLabelA;
    [SerializeField] TextMeshProUGUI momentumLabelB;
    [SerializeField] UdonBehaviour deltaPointer;
    [SerializeField] UdonBehaviour momentumX;
    [SerializeField] UdonBehaviour momentumY;
    [Header("Ewald Circle Prefab"),SerializeField]
    UdonCircle ewaldCircle;
    [Header("Calculated Values")]
    [SerializeField]
    public Vector2Int numPoints = Vector2Int.one;
    [SerializeField]
    public Vector2Int numColsRows = Vector2Int.one;
    [SerializeField]
    public Vector2 reciprocalCellP = Vector2.one;
    [SerializeField]
    public Vector2 reciprocalSpacing = Vector2.one;

    // Particles for points
    [SerializeField]
    ParticleSystem gridParticles;
    Transform gridTransform;
    [SerializeField]
    bool hasParticles = false;

    [SerializeField] Vector2 incidentVec = Vector2.one;
    [SerializeField] Vector2 exitVec = Vector2.one;
    [SerializeField] Vector2 exitOrigin = Vector2.zero;
    [SerializeField] Vector2 deltaVec = Vector2.one;
    float beamPointerLen;
    bool latticeChanged = false;
    bool rotationChanged = false;
    bool beamChanged = false;
    bool started = false;

    Vector3[] latticeReactions = null; // Encoding x,y reaction; z reaction magnitude
    [SerializeField] Vector2 ewaldCentre = Vector2.one;
    private void UpdateReactionLines()
    {
        if (!showReactions)
        {
            if (reactionLines != null)
            {
                for (int i = 0; i < reactionLines.Length; i++)
                    if (reactionLines[i] != null)
                        reactionLines[i].enabled = false;
            }
            return;
        }
        if (latticeReactions == null) 
            generateReactionList();
        Vector3 currentReactionStruct;
        Vector2 reactNormalized;
        float reactmag;
        float cosineBeamToReaction;
        float reactMax = maxBeamImpulse * 2f;
        //float halfreactMag, theta, opposite, adjacent;
        //float requiredBeamImpulse;
        float incidentRadians = beamAngles.x + Mathf.PI;
        Vector2 incidentNormalized = new Vector2(Mathf.Cos(incidentRadians), Mathf.Sin(incidentRadians));
        ewaldCentre = incidentNormalized * beamImpulse;
        int validReactionCount = 0;
        float lineMin = lineThickness / 2f;
        float lineMax = (lineThickness * 3) - lineMin;
        for (int nReaction = 0; nReaction < latticeReactions.Length; nReaction++)
        {
            if (latticeReactions[nReaction] != null)
            {
                currentReactionStruct = latticeReactions[nReaction];
                reactNormalized = currentReactionStruct;
                reactmag = currentReactionStruct.z;
                //halfreactmag = reactmag*0.5f;
                cosineBeamToReaction = Vector2.Dot(incidentNormalized, reactNormalized);
                if (cosineBeamToReaction > 0)
                {
                    Vector2 reactionVec = reactmag * reactNormalized;
                    float ewaldDistance = (reactionVec - ewaldCentre).magnitude;
                    float delta = Mathf.Abs(beamImpulse - ewaldDistance);
                    if (delta <= impulseWidth)
                    {
                        float lineWidth = lineMin + ((impulseWidth-delta) * lineMax) / impulseWidth;
                        if (validReactionCount < reactionLines.Length)
                        {
                            var lr = reactionLines[validReactionCount];
                            lr.startWidth = lineWidth;
                            lr.endWidth = lineWidth;
                            lr.SetPosition(1,reactionVec*momentumToGridScale);
                            lr.enabled = true;
                            lr.endColor = lerpColour(reactmag / reactMax);
                            reactionLines[validReactionCount] = lr;
                        }
                        validReactionCount++;
                    }
                }
            }
            for (int n = validReactionCount; n < reactionLines.Length; n++ )
            {
                reactionLines[n].enabled = false;
            }
        }
    }

    private void generateReactionList()
    {
        Vector2 currentReaction = new Vector2(numPoints.x * reciprocalCellP.x, numPoints.y * reciprocalCellP.y);
        int totalReactions = numColsRows.x * numColsRows.y;
        if (totalReactions <= 0)
            return;
        if (latticeReactions == null || latticeReactions.Length < totalReactions)
            latticeReactions = new Vector3[totalReactions];
        int nReaction = 0;
        for (int nRow = 0; nRow < numColsRows.y; nRow++)
        {
            currentReaction.x = numPoints.x * reciprocalCellP.x;
            for (int nCol = 0; nCol < numColsRows.x; nCol++)
            {
                Vector2 reactNorm = currentReaction.normalized;
                float reactMag = currentReaction.magnitude;
                latticeReactions[nReaction++] = new Vector3(reactNorm.x,reactNorm.y,reactMag);
                currentReaction.x -= reciprocalCellP.x;
            }
            currentReaction.y -= reciprocalCellP.y;
        }
    }


    public Color lerpColour(float frac)
    {

        return spectrumColour(Mathf.Lerp(780, 390, frac*.7f),1);
    }

    public Color spectrumColour(float wavelength, float gamma = 0.8f)
    {
        Color result = Color.white;
        if (wavelength >= 380 & wavelength <= 440)
        {
            float attenuation = 0.3f + 0.7f * (wavelength - 380.0f) / (440.0f - 380.0f);
            result.r = Mathf.Pow(((-(wavelength - 440) / (440 - 380)) * attenuation), gamma);
            result.g = 0.0f;
            result.b = Mathf.Pow((1.0f * attenuation), gamma);
        }

        else if (wavelength >= 440 & wavelength <= 490)
        {
            result.r = 0.0f;
            result.g = Mathf.Pow((wavelength - 440f) / (490f - 440f), gamma);
            result.b = 1.0f;
        }
        else if (wavelength >= 490 & wavelength <= 510)
        {
            result.r = 0.0f;
            result.g = 1.0f;
            result.b = Mathf.Pow(-(wavelength - 510f) / (510f - 490f), gamma);
        }
        else if (wavelength >= 510 & wavelength <= 580)
        {
            result.r = Mathf.Pow((wavelength - 510f) / (580f - 510f), gamma);
            result.g = 1.0f;
            result.b = 0.0f;
        }
        else if (wavelength >= 580f & wavelength <= 645f)
        {
            result.r = 1.0f;
            result.g = Mathf.Pow(-(wavelength - 645f) / (645f - 580f), gamma);
            result.b = 0.0f;
        }
        else if (wavelength >= 645 & wavelength <= 750)
        {
            float attenuation = 0.3f + 0.7f * (750 - wavelength) / (750 - 645);
            result.r = Mathf.Pow(1.0f * attenuation, gamma);
            result.g = 0.0f;
            result.b = 0.0f;
        }
        else
        {
            result.r = 0.1f;
            result.g = 0.1f;
            result.b = 0.1f;
            result.a = 0.3f;
        }
        return result;
    }

    LineRenderer[] reactionLines = null;

    void CalcDimensions()
    {
        reciprocalSpacing.x = momentumToGridScale * AngstromToP24 / latticeAngstroms.x;
        reciprocalSpacing.y = momentumToGridScale * AngstromToP24 / latticeAngstroms.y;
        reciprocalCellP = new Vector2(AngstromToP24 / latticeAngstroms.x, AngstromToP24 / latticeAngstroms.y);
        if (momentumLabelA != null)
            momentumLabelA.text = string.Format("h/a={0:0.0}yN•s", reciprocalCellP.x);
        if (momentumLabelB != null)
            momentumLabelB.text = string.Format("h/b={0:0.0}yN•s", reciprocalCellP.y);
        Vector2Int prevX = numPoints;
        numPoints.x = (int)((2*maxBeamImpulse )/ reciprocalCellP.x)+1;
        numPoints.y = (int)((2*maxBeamImpulse )/ reciprocalCellP.y)+1;
        if (prevX != numPoints)
        {
            SpawnGrid();
            generateReactionList();
        }
    }

    void SpawnGrid()
    {
        numColsRows.x = (numPoints.x * 2) + 1;
        numColsRows.y = (numPoints.y * 2) + 1;
        
        int nLines = numColsRows.x + 5;
        int oldLen = reactionLines != null ? reactionLines.Length : 0;
        int newStart = oldLen;
        if ((reactionLines == null) || (reactionLines.Length <= nLines))
        {
            LineRenderer[] tmpLines = new LineRenderer[numColsRows.x + 5];
            while (oldLen-- > 0)
            {
                tmpLines[oldLen] = reactionLines[oldLen];
            }
            reactionLines = tmpLines;
        } 
        for (int i = newStart; i < reactionLines.Length; i++)
        {
            GameObject go = Instantiate(linePrefab);
            if (go != null)
            {
                go.transform.parent = transform;
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one;
                go.transform.localRotation = Quaternion.identity;
                LineRenderer lr = go.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    lr.startWidth = lineThickness;
                    lr.endWidth = lineThickness;
                    lr.startColor = lineColor;
                    lr.endColor = lineColor;
                    lr.enabled = false;
                    reactionLines[i] = lr;
                }
            } 
        
        }
    }
    ParticleSystem.Particle[] particles;

    void UpdateGrid()
    {
        if (!showPoints) 
            return;
        Vector3 rowCol = new Vector3(numPoints.x * reciprocalSpacing.x, numPoints.y * reciprocalSpacing.y, 0);
        Vector2 pointP = new Vector2(numPoints.x * reciprocalCellP.x, numPoints.y * reciprocalCellP.y);
        Vector3 lossyScale = transform.lossyScale;
        Vector3 lineEnd;

        int particleCount = 0;
        if (hasParticles && showPoints)
        {
            gridParticles.Stop();
            particles = new ParticleSystem.Particle[numColsRows.x*numColsRows.y];
        }

        for (int nRow = 0; nRow < numColsRows.y; nRow++)
        {
            rowCol.x = numPoints.x * reciprocalSpacing.x;
            pointP.x = numPoints.x * reciprocalCellP.x;
            lineEnd = new Vector3(rowCol.x - ((numColsRows.x - 1) * reciprocalSpacing.x), rowCol.y, 0);
            for (int nCol = 0; nCol < numColsRows.x; nCol++)
            {
                float momentumRatio = pointP.magnitude / maxBeamImpulse;
                if (showPoints && momentumRatio <= 2f)
                {
                    var particle = new ParticleSystem.Particle();
                    particle.position = Vector3.Scale(lossyScale,rowCol);
                    particle.startColor = lerpColour(momentumRatio);
                    particle.startSize = pointSize;
                    particle.startLifetime = 100;
                    particle.remainingLifetime = 100;
                    particles[particleCount++] = particle;
                }
                rowCol.x -= reciprocalSpacing.x;
                pointP.x -= reciprocalCellP.x;
            }
            rowCol.y -= reciprocalSpacing.y;
            pointP.y -= reciprocalCellP.y;
        }
        if (hasParticles && showPoints && particleCount > 0)
        {
            gridParticles.SetParticles(particles, particleCount);
        }
    }

    private void drawExitVec()
    {
        // Overlay on incident vector
        exitLocation.localPosition = exitOrigin;
        exitPointer.SetProgramVariable("thetaDegrees",beamAnglesDeg.y);
        exitPointer.SetProgramVariable<Color>("lineColour",beamColour);
        exitPointer.SetProgramVariable("lineLength",beamPointerLen);
    }
    private bool UpdateOverlay()
    {
        Vector2 newOrigin = Vector2.Lerp(Vector2.zero, -incidentVec, overlayShift);
        if (newOrigin != exitOrigin)
        {
            exitOrigin = newOrigin;
            drawExitVec();
            return true;
        }
        return false;
    }

    Vector2 prevAngles = new Vector2(-500,-500);
    float prevImpulse = -1000;

    bool UpdateVectors()
    {
        bool impulseChanged = (!started) || prevImpulse != beamImpulse;
        bool incidentChanged = prevAngles.x != beamAngles.x || impulseChanged;
        bool exitChanged = prevAngles.y != beamAngles.y || incidentChanged;
        beamPointerLen = momentumToGridScale * beamImpulse;
        float maxBeamLen = momentumToGridScale * maxBeamImpulse;
        beamColour = lerpColour(beamPointerLen / maxBeamLen);
        float rot = latticeRotation * Mathf.Deg2Rad;
        float cosRotation = Mathf.Cos(-rot);
        float sinRotation = Mathf.Sin(-rot);
        // calc incident vector from zero
        incidentVec = new Vector2(Mathf.Cos(beamAngles.x), Mathf.Sin(beamAngles.x)) * beamPointerLen;

        exitVec = new Vector2(Mathf.Cos(beamAngles.y), Mathf.Sin(beamAngles.y)) * beamPointerLen;

        if (incidentPointer != null && incidentChanged)
        {
            incidentPointer.SetProgramVariable("thetaDegrees",beamAnglesDeg.x);
            incidentPointer.SetProgramVariable<Color>("lineColour",beamColour);
            incidentPointer.SetProgramVariable("lineLength",beamPointerLen);
            //Debug.Log("Udon Reciprocal: Beam Pointer Len" + beamPointerLen);
        }
        exitChanged |= UpdateOverlay();
        if (exitPointer != null && exitChanged)
        {
            drawExitVec();
        }
        deltaVec = incidentVec - exitVec;

        Vector2 deltaR = new Vector2(cosRotation*deltaVec.x - sinRotation*deltaVec.y, sinRotation * deltaVec.x + cosRotation*deltaVec.y);   
        if (deltaPointer != null && exitChanged)
        {
            float deltaLen = deltaVec.magnitude;
            float deltaTheta = Mathf.Atan2(deltaVec.y, deltaVec.x) * Mathf.Rad2Deg;
            deltaPointer.SetProgramVariable("thetaDegrees",deltaTheta + 180f);
            deltaPointer.SetProgramVariable("lineLength",deltaLen);
            deltaPointer.SetProgramVariable<Color>("lineColour",lerpColour(deltaLen / maxBeamLen));
        }
        if (momentumX != null)
        {
            momentumX.SetProgramVariable("thetaDegrees",(deltaR.x >= 0 ? 180f : 0f) + latticeRotation);
            float lenX = Mathf.Abs(deltaR.x);
            momentumX.SetProgramVariable("lineLength",lenX);
            momentumX.SetProgramVariable("alpha",deltaR.x != 0 ? 1f : 0f);
            momentumX.SetProgramVariable<Color>("lineColour",lerpColour(lenX / maxBeamLen));
        }
        if (momentumY != null)
        {
            momentumY.SetProgramVariable("thetaDegrees",(deltaVec.y >= 0 ? -90f : 90f) + latticeRotation);
            float lenY = Mathf.Abs(deltaR.y);
            momentumY.SetProgramVariable("lineLength",lenY);
            momentumY.SetProgramVariable("alpha",deltaR.y != 0 ? 1f : 0f);
            momentumY.SetProgramVariable<Color>("lineColour",lerpColour(lenY / maxBeamLen));
        }

        if (showEwald && incidentChanged)
        {
            ewaldCircle.transform.localPosition = -incidentVec;
            ewaldCircle.Radius = beamPointerLen;
        }
        prevAngles = beamAngles;
        prevImpulse = beamImpulse;
        return incidentChanged;
    }
    private void Update()
    {
        if (latticeChanged || !started)
        {
            CalcDimensions();
            UpdateGrid();
        }
        //if (rotationChanged || !started)

        if ( latticeChanged || rotationChanged || beamChanged || !started)
        {
            bool reactChanged = UpdateVectors();
            if (latticeChanged || reactChanged || !started) 
                UpdateReactionLines();
        }
        beamChanged = false;
        latticeChanged = false;
        rotationChanged = false;
        started = true;
    }
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        iamOwner = Networking.IsOwner(this.gameObject);
    }

    void Start()
    {

        player = Networking.LocalPlayer;
        iamOwner = Networking.IsOwner(this.gameObject);
        if (exitPointer != null)
            exitLocation = exitPointer.transform;
        if (gridParticles == null)
            gridParticles = gameObject.GetComponentInChildren<ParticleSystem>();
        ShowComponents = showComponents;
        hasParticles = (gridParticles != null);
        if (hasParticles) gridTransform = gridParticles.transform;
        if (ewaldCircle == null)
            showEwald = false;
        showReactions &= (linePrefab != null);
        latticeChanged = false;
        beamChanged = false;
        started = false;
    }
}
