﻿using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class VectorDiagram : UdonSharpBehaviour
{
    [SerializeField, FieldChangeCallback(nameof(DisplayRect))] public Vector2 displayRect = new Vector2(1.95f,0.95f);
    private float halfHeight = 0.46f;

    [Tooltip("Slit Count"),SerializeField, Range(1,16),FieldChangeCallback(nameof(SlitCount))] public int slitCount = 2;
    [Tooltip("Slit Width (mm)"),SerializeField, FieldChangeCallback(nameof(SlitWidth))] float slitWidth;
    [Tooltip("Slit Pitch (mm)"), SerializeField,FieldChangeCallback(nameof(SlitPitch))] public float slitPitch = 436.5234f;
    [Tooltip("Lambda (mm)"), SerializeField, FieldChangeCallback(nameof(Lambda))] public float lambda = 48.61111f;
    [SerializeField] private float arrowLambda = 18;
    [SerializeField] private float layerGap = 0.003f;
    [SerializeField, FieldChangeCallback(nameof(DemoMode))] public int demoMode;
    [SerializeField] UdonBehaviour[] kVectors;
    [SerializeField] UdonBehaviour[] kComponents;
    [SerializeField] UdonLabel[] vecLabels;
    [SerializeField] UdonBehaviour[] kLines;

    Vector2[] kEndPoints;
    Vector2[] kStartPoints;
    Vector2[] labelPoints;
    string[] beamAngles;
    private bool needsUpdate = false;
    private float arrowLength = 0.1f;
    
    public Vector2 DisplayRect
    {
        get => displayRect;
        set
        {
            displayRect = value;
            halfHeight = displayRect.y/2f;
            needsUpdate = true;
        }
    }
    private int DemoMode
    {
        get => demoMode; 
        set
        {
            demoMode = value;
            needsUpdate = true;
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
        Vector3 layerOffset = new Vector3(0,0,layerGap);
        kEndPoints = new Vector2[kVectors.Length];
        kStartPoints = new Vector2[kVectors.Length];
        beamAngles = new string[kVectors.Length];
        labelPoints = new Vector2[kVectors.Length];
        float sinTheta;
        float WidthX2 = slitWidth * 2;
        for (int i = 0; i < kVectors.Length; i++)
        {
            float thetaRadians;
            if (slitCount > 1)
                sinTheta = i * lambda / slitPitch;
            else
                sinTheta = i == 0 ? 0 : lambda * (2 * i + 1) / WidthX2;
            float lineLength = arrowLength;
            Vector2 endPoint = Vector2.left;
            Vector2 startPoint = Vector3.zero;
            Vector2 labelPoint = Vector2.left;
            bool sinIsValid = Mathf.Abs(sinTheta) < 1f;
            thetaRadians = sinIsValid ? Mathf.Asin(sinTheta) : 0;
            float cosTheta = Mathf.Cos(thetaRadians);
            beamAngles[i] = sinIsValid ? string.Format("{0:0.#}°", thetaRadians*Mathf.Rad2Deg) : "";
            if (sinIsValid)
            {
                switch (demoMode)
                {
                    case 2:
                        endPoint.y = sinTheta * arrowLength;
                        if (endPoint.y <= halfHeight)
                        {
                            endPoint.x = Mathf.Cos(thetaRadians) * arrowLength;
                            labelPoint.x = displayRect.x;
                        }
                        labelPoint.y = endPoint.y;
                        break;
                    case 3:
                        lineLength = arrowLength / 5f;
                        float deltay = sinTheta * lineLength;
                        startPoint.y = sinTheta * (displayRect.x-lineLength);
                        Vector2 startDelta = new Vector2(cosTheta, sinTheta);
                        startDelta *= lineLength;
                        if (startPoint.y <= halfHeight - deltay)
                            startPoint.x = displayRect.x - lineLength;
                        else
                        {
                            startPoint.y = halfHeight - deltay;
                            startPoint.x = startPoint.y/Mathf.Tan(thetaRadians); // halfHeightx = x * tan
                        }
                        endPoint = startPoint + startDelta;
                        labelPoint = endPoint;
                        break;

                default:
                        endPoint.y = sinTheta * displayRect.x;
                        if (endPoint.y <= halfHeight)
                        {
                            endPoint.x = displayRect.x;
                            lineLength = endPoint.magnitude;
                        }
                        else
                        {
                            endPoint.y = halfHeight;
                            lineLength = halfHeight / sinTheta;
                            endPoint.x = lineLength * Mathf.Cos(thetaRadians);
                        }
                        labelPoint = endPoint;
                        break;
                }
            }
            kEndPoints[i] = endPoint;
            kStartPoints[i] = startPoint;
            labelPoints[i] = labelPoint;
            if (kVectors[i] != null)
            {
                UdonBehaviour tmp = kVectors[i];
                tmp.SetProgramVariable<bool>("showTip", demoMode >= 2);
                if (endPoint.x > 0)
                {
                    tmp.transform.localPosition = (Vector3)startPoint + layerOffset;
                    tmp.SetProgramVariable("lineLength", lineLength);
                    tmp.SetProgramVariable("thetaDegrees", thetaRadians * Mathf.Rad2Deg);
                    tmp.SetProgramVariable("alpha", 1f);
                }
                else
                    tmp.SetProgramVariable("alpha", 0f);
            }
            else
                Debug.Log(string.Format("kVectors[{0}]: null", i));
        }
        if (vecLabels != null && vecLabels.Length > 0)
        {
            for (int i = 0; i < vecLabels.Length; i++)
            {
                if (vecLabels[i] != null)
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
                        vecLabels[i].LocalPostion = (Vector3)labelPoints[posIdx]+(layerOffset*0.5f);
                        vecLabels[i].Visible = true;
                    }
                    else
                        vecLabels[i].Visible = false;
                    vecLabels[i].Text = labelText;
                }
            }
        }

    }

    private void kLineDisplay(int demoMode)
    {
        if (kLines == null)
            return;
        Vector3 offset = new Vector3(0,0,layerGap*.75f);
        int maxIndex = (kEndPoints == null) || (labelPoints == null) ? 0 : kEndPoints.Length;
        for (int i = 0; i < kLines.Length; i++)
        {
            UdonBehaviour kptr = kLines[i];
            if (kptr != null)
            {
                if (demoMode < 2 || i >= maxIndex)
                    kptr.SetProgramVariable("alpha",0f);
                else
                {
                    if (labelPoints[i].x < 0)
                        kptr.SetProgramVariable("alpha",0f);
                    else
                    {
                        if (demoMode == 2)
                        {
                            kptr.SetProgramVariable("alpha", 1f);
                            kptr.SetProgramVariable<bool>("showTip",false);
                            kptr.SetProgramVariable("lineLength",displayRect.x);
                            kptr.transform.localPosition = new Vector3(0, labelPoints[i].y, 0) + offset;
                        }
                        else
                        {
                            if (i == 0)
                                kptr.SetProgramVariable("alpha", 0f);
                            else
                            {
                                kptr.SetProgramVariable("alpha", 1f);
                                kptr.SetProgramVariable<bool>("showTip", true);
                                kptr.SetProgramVariable("lineLength", kEndPoints[i].x - kStartPoints[i].x);
                                kptr.transform.localPosition = (Vector3)kStartPoints[i] + offset;
                            }
                        }
                    }
                }
            }
        }
    }
    private void componentDisplay(int demoMode)
    {
        Vector3 linePos = new Vector3(0,0, layerGap * 1.5f);
        if ( kComponents == null)
            return;            
        if (demoMode < 2)
        {
            for (int i = 0; i < kComponents.Length; i++)
            {
                if (kComponents[i] != null)
                    kComponents[i].SetProgramVariable("alpha",0f);
            }
            return;
        }
        if (kComponents == null)
            return;
        int maxIndex = kEndPoints.Length - 1;
        if (kComponents.Length < maxIndex)
            maxIndex = kComponents.Length;
        for (int j = 0; j < maxIndex; j++)
        {
            if (kComponents[j] != null)
            {
                float len = kEndPoints[j + 1].y - kStartPoints[j +1].y;
                linePos.x = kEndPoints[j + 1].x;
                linePos.y = kEndPoints[j + 1].y-len;
                kComponents[j].SetProgramVariable("lineLength",len);
                if (linePos.x >= 0)
                {
                    kComponents[j].transform.localPosition = linePos;
                    kComponents[j].SetProgramVariable("alpha",1f);
                }
                else
                {
                    kComponents[j].SetProgramVariable("alpha",0f);
                }
            }
        }
    }
    private void recalc()
    {
        kVectorDisplay(demoMode);
        componentDisplay(demoMode);
        kLineDisplay(demoMode);
        needsUpdate = false;
    }

    public int SlitCount
    {
        get => slitCount;
        set
        {
            value = Mathf.Max(1,value);
            slitCount = value;
            needsUpdate = true;
        }
    }

    public float SlitWidth
    {
        get => slitWidth;
        set
        {
            slitWidth = Mathf.Max(1.0f,value);
            needsUpdate = true;
        }
    }
    public float SlitPitch
    {
        get => slitPitch; 
        set
        {
            slitPitch = value;
            needsUpdate = true;
            //Debug.Log("Vect Gap: " +  slitPitch);
        } 
    }
    public float Lambda
    {
        get => lambda;
        set
        {
            lambda = value;
            needsUpdate = true;
        }
    }

    private void Update()
    {
        if (needsUpdate)
        {
            needsUpdate = false;
            recalc();
        }
    }

    void Start()
    {
        DisplayRect = displayRect;
        needsUpdate = true;
    }
}
