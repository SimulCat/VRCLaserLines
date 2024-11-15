﻿using UdonSharp;
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
    [Tooltip("Dimension Scale 1:x"), SerializeField, FieldChangeCallback(nameof(VecScale))] public float vecScale = 0.001f;

    [SerializeField] private float arrowLambda = 18;
    //[SerializeField] private float layerGap = 0.003f;
    [SerializeField, FieldChangeCallback(nameof(DemoMode))] public int demoMode;
    [SerializeField] LaserVectorLine[] kVectors;
    [SerializeField] LaserVectorLine[] kComponents;
    [SerializeField] UdonLabel[] vecLabels;
    [SerializeField] LaserVectorLine[] kLines;

    //[SerializeField] 
    Vector2[] kStartPoints;
    //[SerializeField]
    Vector2[] kEndPoints;

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
    private int DemoMode
    {
        get => demoMode; 
        set
        {
            demoMode = value;
            needsUpdate += (_demoMode != demoMode) ? 2 : 0;
            _demoMode = demoMode;
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

    private void hideLines()
    {

    }
    private void kVectorDisplay(int demoMode)
    {
        arrowLength = (arrowLambda) / lambda;
        if (demoMode <= 0)
        {
            hideLabels();
            hideVectors();
            return;
        }
        if (kVectors == null || kVectors.Length == 0)
            return;
        //Vector3 layerOffset = new Vector3(0, 0, layerGap);
        kEndPoints = new Vector2[kVectors.Length];
        kStartPoints = new Vector2[kVectors.Length];
        beamAngles = new string[kVectors.Length];
        labelPoints = new Vector2[kVectors.Length];
        float sinTheta;
        float WidthX2 = slitWidth * 2;

        for (int i = 0; i < kVectors.Length; i++)
        {
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
                switch (demoMode)
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
                            labelPoint.x = displayRect.x;
                            startPoint.x = 0;
                            kStartPoints[i].x = 0;
                        }
                        labelPoint.y = endPoint.y;
                        kEndPoints[i] = endPoint;
                        kStartPoints[i].y = startPoint.y;
                        break;
                    case 3:
                        lineLength = arrowLength / 5f;
                        float deltay = sinTheta * lineLength;
                        startPoint.y = sinTheta * (displayRect.x - lineLength);
                        Vector2 startDelta = new Vector2(cosTheta, sinTheta);
                        startDelta *= lineLength;
                        if (startPoint.y <= halfHeight - deltay)
                            startPoint.x = displayRect.x - lineLength;
                        else
                        {
                            startPoint.y = halfHeight - deltay;
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
                vecLine.ShowTip = demoMode >= 2;
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
                    int posIdx = i;
                    string labelText;
                    //string mul = posIdx > 1 ? string.Format("{0}*",posIdx) : ""; 
                    switch (demoMode)
                    {
                        case 1:
                            labelText = string.Format("θ<sub>{0}</sub>={1}", posIdx, beamAngles[posIdx ]);
                            break;
                        case 2:
                            labelText = string.Format("Δk<sub>{0}</sub>={0}/d", posIdx);
                            break;
                        case 3:
                            string mul = posIdx == 1 ? "h" : string.Format("{0}h", posIdx);
                            labelText = string.Format("Δp<sub>{0}</sub>={1}/d", posIdx,mul);
                            break;
                        default:
                            labelText = "";
                            break;
                    }
                    if (demoMode > 0 && labelPoints[posIdx].x > 0)
                    {
                        lbl.LocalPostion = (Vector3)labelPoints[posIdx]; //+(layerOffset*0.5f);
                        lbl.Visible = true;
                        lbl.Text = labelText;
                    }
                    else
                        lbl.Visible = false;
                }
            }
        }
    }

    private void kLineDisplay(int demoMode)
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
                kptr.Alpha = (i >= maxPoint || kStartPoints[i].x < 0) ? 0 : 1;
                switch (demoMode)
                {
                case 1:
                    kptr.Alpha = 0f;
                    break;
                case 2:
                    kptr.ShowTip = false;
                    kptr.LineLength = displayRect.x;
                        kptr.transform.localPosition = new Vector3(0, labelPoints[i].y, 0); // + offset;
                    break;
                case 3:
                case 4:
                    if (i == 0)
                        kptr.Alpha = 0;
                    else
                    {
                        kptr.ShowTip = true;
                        kptr.LineLength = kEndPoints[i].x - kStartPoints[i].x;
                            kptr.transform.localPosition = (Vector3)kStartPoints[i]; // + offset;
                    }
                    break;
                default:
                    {
                        kptr.Alpha = 0f;
                    }
                    break;
                }
            }
        }
    }
    private void componentDisplay(int demoMode)
    {
        Vector3 linePos = Vector3.zero; // new Vector3(0,0, layerGap * 1.5f);
        if ( kComponents == null)
            return;            
        if (demoMode < 2)
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
        kVectorDisplay(demoMode);
        componentDisplay(demoMode);
        kLineDisplay(demoMode);
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
