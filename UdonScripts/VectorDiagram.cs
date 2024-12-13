using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class VectorDiagram : UdonSharpBehaviour
{
    [SerializeField] private Vector2 displayRect = new Vector2(1.95f,0.95f);
    private float halfHeight = 0.46f;

    [Tooltip("Slit Count"),SerializeField, Range(1,16),FieldChangeCallback(nameof(SlitCount))] public int slitCount = 2;
    [Tooltip("Slit Width (mm)"),SerializeField, FieldChangeCallback(nameof(SlitWidth))] float slitWidth;
    [Tooltip("Slit Pitch (mm)"), SerializeField,FieldChangeCallback(nameof(SlitPitch))] public float slitPitch = 436.5234f;
    [Tooltip("Lambda (mm)"), SerializeField, FieldChangeCallback(nameof(Lambda))] public float lambda = 48.61111f;
    [Tooltip("Scale simulation mm to diagram metres"), SerializeField, FieldChangeCallback(nameof(VecScale))] public float vecScale = 0.001f;

    [SerializeField] private float arrowLambda = 18;
    //[SerializeField] private float layerGap = 0.003f;
    [SerializeField, FieldChangeCallback(nameof(Mode))] private int mode;
    [SerializeField] LaserVectorLine[] kVectors;
    [SerializeField] LaserVectorLine[] kComponents;
    [SerializeField] UdonLabel[] vecLabels;
    [SerializeField] LaserVectorLine[] kLines;

   // [SerializeField] 
    Vector2[] kStartPoints;
 //   [SerializeField]
    Vector2[] kEndPoints;
    [SerializeField]
    Vector2[] kLinePoints;
    Vector2[] labelPoints;
    string[] beamAngles;
    private int needsUpdate = -1;
    private float arrowLength = 0.1f;
    

    public Vector2 DisplayRect
    {
        get => displayRect;
        set
        {
            needsUpdate += (value != displayRect) ? 1 : 0;
            displayRect = value;
            halfHeight = displayRect.y / 2f;
        }
    }

    private int _demoMode = -1;
    private int Mode
    {
        get => mode; 
        set
        {
            mode = value;
            needsUpdate += (_demoMode != mode) ? 2 : 0;
            _demoMode = mode;
        }
    }

    private void hideLabels()
    {
        if (vecLabels == null)
            return;
        for (int i = 0; i < vecLabels.Length; i++)
        {
            if (vecLabels[i] != null)
                vecLabels[i].Visible = false;
        }
    }

    private void hideVectors()
    {
        if (kVectors == null)
            return;
        for (int i = 0; i < kVectors.Length; i++)
        {
            if (kVectors[i] != null)
                kVectors[i].SetProgramVariable("alpha", 0f);
        }
    }

    private void kVectorDisplay(int mode)
    {
        arrowLength = arrowLambda / lambda;
        float kPitch = slitPitch > 0 ? arrowLambda/slitPitch : 0;
        float ssPitch = slitWidth > 0 ? arrowLambda/(2*slitWidth) : 0;
        if (mode <= 0)
        {
            hideLabels();
            hideVectors();
            return;
        }
        if (kVectors == null || kVectors.Length == 0)
            return;
        //Vector3 layerOffset = new Vector3(0, 0, layerGap);
        kEndPoints = new Vector2[kVectors.Length];
        kLinePoints = new Vector2[kLines.Length];
        kStartPoints = new Vector2[kVectors.Length];
        beamAngles = new string[kVectors.Length];
        labelPoints = new Vector2[kVectors.Length];
        float sinTheta;
        float WidthX2 = slitWidth * 2;

        for (int i = 0; i < kVectors.Length; i++)
        {
            float kDelta = slitCount > 1 ?  kPitch * i : i >= 1 ? ssPitch * (2*i + 1) : 0 ;
            kLinePoints[i] = new Vector2((kDelta <= halfHeight) ? 0 : -1, kDelta);

            kStartPoints[i] = Vector2.left;
            float thetaRadians;
            if (slitCount > 1)
                sinTheta = i * lambda / slitPitch;
            else
                sinTheta = i == 0 ? 0 : lambda * (2 * i + 1) / WidthX2;
            float lineLength = arrowLength;
            Vector2 endPoint = Vector2.left;
            Vector2 startPoint = Vector2.zero;
            Vector2 labelPoint = Vector2.left;
            bool lessThan90Deg = Mathf.Abs(sinTheta) < 1f;
            thetaRadians = lessThan90Deg ? Mathf.Asin(sinTheta) : 0;
            float cosTheta = Mathf.Cos(thetaRadians);
            beamAngles[i] = lessThan90Deg ? string.Format("{0:0.#}°", thetaRadians * Mathf.Rad2Deg) : "";
            if (lessThan90Deg)
            {
                switch (mode)
                {
                    case 1:
                        {
                            endPoint.y = sinTheta * displayRect.x;
                            if (endPoint.y <= halfHeight)
                                endPoint.x = displayRect.x;
                            else
                            {
                                endPoint.y = halfHeight;
                                endPoint.x = (halfHeight / sinTheta) * Mathf.Cos(thetaRadians);
                            }
                            lineLength = (endPoint - startPoint).magnitude;
                            labelPoint = endPoint;
                            kStartPoints[i] = startPoint;
                            kEndPoints[i] = endPoint;
                        }
                        break;
                    case 2:
                        endPoint.y = sinTheta * arrowLength;
                        if (endPoint.y <= halfHeight)
                        {
                            endPoint.x = Mathf.Cos(thetaRadians) * arrowLength;
                            startPoint.x = 0;
                        }
                        if (startPoint.y < halfHeight) kStartPoints[i].x = 0;
                        labelPoint.y = endPoint.y;
                        labelPoint.x = (labelPoint.y <= halfHeight) ? displayRect.x : -1;
                        kStartPoints[i].y = startPoint.y;
                        kEndPoints[i] = endPoint;
                        break;
                    case 3:
                        lineLength = arrowLength / 5f;
                        float deltay = sinTheta * lineLength;
                        //startPoint.y = sinTheta * (displayRect.x - lineLength);
                        startPoint.y = sinTheta * arrowLength;
                        Vector2 startDelta = new Vector2(cosTheta, sinTheta);
                        startDelta *= lineLength;
                        if (startPoint.y <= halfHeight)
                            startPoint.x = arrowLength*cosTheta;
                        else
                        {
                            startPoint.y = halfHeight;
                            startPoint.x = startPoint.y / Mathf.Tan(thetaRadians); // halfHeightx = x * tan
                        }
                        endPoint = startPoint + startDelta;
                        labelPoint = endPoint;
                        kEndPoints[i] = endPoint;
                        kStartPoints[i] = startPoint;
                        break;

                    default:
                        endPoint.y = sinTheta * displayRect.x;
                        if (endPoint.y <= halfHeight)
                        {
                            endPoint.x = displayRect.x;
                            lineLength = endPoint.magnitude;
                            kStartPoints[i] = startPoint;
                        }
                        else
                        {
                            endPoint.y = halfHeight;
                            lineLength = halfHeight / sinTheta;
                            endPoint.x = lineLength * Mathf.Cos(thetaRadians);
                            kStartPoints[i] = Vector2.left;
                        }
                        labelPoint = endPoint;
                        kEndPoints[i] = endPoint;
                        break;
                }
            }
            else
            {
                // Sine not valid
                kStartPoints[i] = Vector2.left;
            }
            labelPoints[i] = labelPoint;
            if (kVectors[i] != null)
            {
                LaserVectorLine vecLine = kVectors[i];
                vecLine.ShowTip = mode >= 2;
                if (endPoint.x > 0)
                {
                    vecLine.transform.localPosition = (Vector3)startPoint;// + layerOffset;
                    vecLine.LineLength = lineLength;
                    vecLine.ThetaDegrees = thetaRadians * Mathf.Rad2Deg;
                    vecLine.Alpha = 1f;
                }
                else
                    vecLine.Alpha = 0f;
            }
            //else
              //  Debug.Log(string.Format("kVectors[{0}]: null", i));
        }
        if (vecLabels != null && vecLabels.Length > 0)
        {
            for (int i = 0; i < vecLabels.Length; i++)
            {
                UdonLabel lbl = vecLabels[i];
                if (lbl != null)
                {
                    string labelText;
                    //string mul = posIdx > 1 ? string.Format("{0}*",posIdx) : ""; 
                    switch (mode)
                    {
                        case 1:
                            labelText = string.Format("θ<sub>{0}</sub>={1}", i, beamAngles[i]);
                            break;
                        case 2:
                            if (slitCount <= 1)
                            {
                                labelText = string.Format("Δk<sub>{0}</sub>={1}/2w", i, i == 0 ? 0 : (2 * i + 1));
                            }
                            else
                            {
                                labelText = string.Format("Δk<sub>{0}</sub>={0}/d", i);
                            }
                            break;
                        case 3:
                            if (slitCount <= 1)
                            {
                                labelText = string.Format("Δp<sub>{0}</sub>={1}h/2w", i, i == 0 ? 0 : (2 * i + 1));
                            }
                            else
                            {
                                string mul = i == 1 ? "h" : string.Format("{0}h", i);
                                labelText = string.Format("Δp<sub>{0}</sub>={1}/d", i, mul);
                            }
                            break;
                        default:
                            labelText = "";
                            break;
                    }
                    if (mode > 0 && labelPoints[i].x > 0)
                    {
                        lbl.LocalPostion = (Vector3)labelPoints[i]; //+(layerOffset*0.5f);
                        lbl.Visible = true;
                        lbl.Text = labelText;
                    }
                    else
                        lbl.Visible = false;
                }
            }
        }
    }

    private void kLineDisplay(int mode)
    {
        if (kLines == null)
            return;
        //Vector3 offset = new Vector3(0,0,layerGap*.75f);
        int maxPoint = (kEndPoints == null) ? 0 : kEndPoints.Length;
        for (int i = 0; i < kLines.Length; i++)
        {
            LaserVectorLine kptr = kLines[i];
            if (kptr != null)
            {
                switch (mode)
                {
                case 1:
                        kptr.Alpha = 0f;
                    break;
                case 2: // Wave K-vector lines horizontal
                        if ((i >= maxPoint) || (i == 0) || (kLinePoints[i].x < 0))
                            kptr.Alpha = 0;
                        else
                        {
                            kptr.Alpha = 1;
                            kptr.ShowTip = false;
                            kptr.LineLength = displayRect.x;
                            kptr.transform.localPosition = kLinePoints[i]; // + offset;
                        }
                    break;
                case 3: // Photon K-vector lines
                case 4:
                        if (i >= maxPoint || (i == 0) || (kStartPoints[i].x < 0))
                            kptr.Alpha = 0;
                        else
                        {
                            kptr.Alpha = 1;
                            kptr.ShowTip = true;
                            kptr.LineLength = kEndPoints[i].x - kStartPoints[i].x;
                            kptr.transform.localPosition = (Vector3)kStartPoints[i]; // + offset;
                        }
                    break;
                default:
                        kptr.Alpha = 0f;
                    break;
                }
            }
        }
    }
    private void componentDisplay(int mode)
    {
        Vector3 linePos = Vector3.zero; // new Vector3(0,0, layerGap * 1.5f);
        if ( kComponents == null)
            return;            
        if (mode < 2)
        {
            for (int i = 0; i < kComponents.Length; i++)
            {
                if (kComponents[i] != null)
                    kComponents[i].Alpha = 0f;
            }
            return;
        }
        int maxIndex = kEndPoints.Length - 1;
        if (kComponents.Length < maxIndex)
            maxIndex = kComponents.Length;
        for (int j = 0; j < maxIndex; j++)
        {
            LaserVectorLine line = kComponents[j];
            if (line != null)
            {
                float len = kEndPoints[j + 1].y - kStartPoints[j +1].y;
                linePos.x = kEndPoints[j + 1].x;
                linePos.y = kEndPoints[j + 1].y-len;
                line.LineLength = len;
                if (linePos.x >= 0)
                    line.transform.localPosition = linePos;
                line.Alpha = linePos.x >= 0 && len > 0 ? 1 :0f;
            }
        }
    }
    private void recalc()
    {
        kVectorDisplay(mode);
        componentDisplay(mode);
        kLineDisplay(mode);
    }

    public int SlitCount
    {
        get => slitCount;
        set
        {
            value = Mathf.Max(1,value);
            needsUpdate |= value != slitCount ? 4 : 0;
            slitCount = value;
        }
    }

    public float SlitWidth
    {
        get => slitWidth;
        set
        {
            value = Mathf.Max(1.0f,value);
            needsUpdate |= slitWidth != value ? 8 : 0;
            slitWidth = value;
        }
    }
    public float SlitPitch
    {
        get => slitPitch; 
        set
        {
            needsUpdate |= slitPitch != value ? 16 : 0;
            slitPitch = value;
        } 
    }
    public float Lambda
    {
        get => lambda;
        set
        {
            needsUpdate |= lambda != value ? 32 : 0;
            lambda = value;
        }
    }

    public float VecScale
    {
        get=>vecScale;
        set
        {
            needsUpdate |= (vecScale != value) ? 64 : 0;
            vecScale = value;
        }
    }
    private void Update()
    {
        if (needsUpdate > 0)
        {
            //Debug.Log(string.Format("Update {0:X}", needsUpdate)); 
            needsUpdate = 0;
            recalc();
        }
    }

    void Start()
    {
        DisplayRect = displayRect;
        needsUpdate = 128;
    }
}
