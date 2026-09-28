
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ParticleFrame2D : UdonSharpBehaviour
{
    [SerializeField]
    private UdonLineArray laserLines; // Array of UdonLineArray components to draw the frame
    [SerializeField]
    private Vector3 simSize = new Vector3(3.84f, 1.96f, 0.5f); // Size of the simulation area in meters
    [SerializeField]
    private float slitWidth = 0.1f; // Width of the slit in meters
    [SerializeField]
    private float slitPitch = 0.2f; // Pitch of the slit in meters
    [SerializeField]
    private float slitOffset = 0.2f; // Distance of the slit from x=0 in meters
    [SerializeField] int slitCount = 0; // Number of slits
    private bool sizeChanged = true;
    [SerializeField]
    private bool slitsChanged = false; // Flag to indicate if slits have changed
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(WorldScale))]
    private float worldScale = 10.0f; // Scale of the world, used for scaling the simulation area
    private float WorldScale
    {
        get => worldScale;
        set
        {
            if (worldScale != value)
            {
                worldScale = value;
                initFrame();
            }
        }
    }
    public Vector3 SimSize
    {
        get => simSize;
        set
        {
            if (simSize != value)
            {
                simSize = value;
                sizeChanged = true;
                initFrame();
            }
        }
    }
    [SerializeField]
    private Vector3[] lineVertices;
    private void initFrame()
    {
        {
            if (laserLines != null)
                laserLines.LineScale = worldScale / 10f;
            if (!sizeChanged && !slitsChanged)
                return; // No changes to apply, exit early

            int lineCount = 4;
            int gratingLines = (slitOffset > 0 && slitCount > 0) ? slitCount + 1 : 0;
            lineVertices = new Vector3[(lineCount + gratingLines) * 2];
            Vector2 halfDims = simSize / 2;
            int v = 0;
            lineVertices[v++] = new Vector3(-halfDims.x, -halfDims.y, 0);
            lineVertices[v++] = new Vector3(halfDims.x, -halfDims.y, 0);
            lineVertices[v++] = new Vector3(-halfDims.x, halfDims.y, 0);
            lineVertices[v++] = new Vector3(halfDims.x, halfDims.y, 0);
            lineVertices[v++] = new Vector3(-halfDims.x, -halfDims.y, 0);
            lineVertices[v++] = new Vector3(-halfDims.x, halfDims.y, 0);
            lineVertices[v++] = new Vector3(halfDims.x, -halfDims.y, 0);
            lineVertices[v++] = new Vector3(halfDims.x, halfDims.y, 0);
            if (gratingLines > 0)
            {
                float gratingWidth = (slitCount - 1) * slitPitch + slitWidth;
                // Edd end lines
                float halfGratingWidth = gratingWidth * 0.5f;
                float slitPos = -halfDims.x + slitOffset;
                float sideBarLength = simSize.y * 0.5f - halfGratingWidth;
                lineVertices[v++] = new Vector3(slitPos, -halfDims.y, 0);
                lineVertices[v++] = new Vector3(slitPos, -halfDims.y + sideBarLength, 0);
                lineVertices[v++] = new Vector3(slitPos, halfDims.y, 0);
                lineVertices[v++] = new Vector3(slitPos, halfDims.y - sideBarLength, 0);
                float barStart = -halfDims.y + sideBarLength + slitWidth;
                float barLength = slitPitch - slitWidth;
                for (int i = 1; i < slitCount; i++)
                {
                    lineVertices[v++] = new Vector3(slitPos, barStart, 0);
                    lineVertices[v++] = new Vector3(slitPos, barStart + barLength, 0);
                    barStart += slitPitch;
                }
            }
            slitsChanged = false;
            sizeChanged = false;
            if (laserLines != null)
            {
                laserLines.LineVertices = lineVertices;
            }
        }
    }

    public float SlitWidth
    {
        get => slitWidth;
        set
        {
            slitsChanged |= (slitWidth != value);
            {
                slitWidth = value;
            }
        }
    }

    public float SlitPitch
    {
        get => slitPitch;
        set
        {
            slitsChanged |= (slitPitch != value);
            {
                slitPitch = value;
            }
        }
    }

    public float SlitOffset
    {
        get => slitOffset;
        set
        {
            slitsChanged |= (slitOffset != value);
            {
                slitOffset = value;
            }
        }
    }

    public int SlitCount
    {
        get => slitCount;
        set
        {
            slitsChanged |= (slitCount != value);
            {
                slitCount = value;
            }
        }
    }

    public void SetGratingParams(Vector2 frameSize, int numSlits, float gapWidth, float gapPitch, float gratingOffset)
    {
        simSize = frameSize;
        slitCount = numSlits;
        slitWidth = gapWidth;
        slitPitch = gapPitch;
        slitOffset = gratingOffset;
        initFrame();
    }

#if UNITY_EDITOR
    public void OnValidate()
    {
        sizeChanged = true;
        initFrame();

    }
#endif
    // Start is called before the first frame update
    void OnEnable()
    {
        initFrame();
    }

    // Update is called once per frame
    void Update()
    {
        if (slitsChanged || sizeChanged)
            initFrame();
    }
}
