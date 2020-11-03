using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Sprites;
using System;
using System.Collections;
using UnityEngine.Serialization;

//[AddComponentMenu("UI/Circle Image")]
public class CircleImage : MaskableGraphic, ISerializationCallbackReceiver, ILayoutElement, ICanvasRaycastFilter
{

    [FormerlySerializedAs("m_Frame")]
    [SerializeField]
    private Sprite m_Sprite;
    public Sprite sprite { get { return m_Sprite; } set { if (SetPropertyUtilityExt.SetClass(ref m_Sprite, value)) SetAllDirty(); } }

    [NonSerialized]
    private Sprite m_OverrideSprite;
    public Sprite overrideSprite { get { return m_OverrideSprite == null ? sprite : m_OverrideSprite; } set { if (SetPropertyUtilityExt.SetClass(ref m_OverrideSprite, value)) SetAllDirty(); } }

    [Header("圆形或扇形填充比例")]
    [Range(0, 1)]
    public float fillPercent = 1f;
    [Header("是否填充圆形")]
    public bool fill = true;
    [Header("圆环宽度")]
    public float thickness = 5;
    [Header("圆形精度")]
    [Range(3, 100)]
    public int segements = 20;

    private List<Vector3> innerVertices;
    private List<Vector3> outterVertices;

    // Use this for initialization
    void Awake()
    {
        innerVertices = new List<Vector3>();
        outterVertices = new List<Vector3>();
    }

    // Update is called once per frame
    void Update()
    {
        this.thickness = (float)Mathf.Clamp(this.thickness, 0, rectTransform.rect.width / 2);
    }



    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        innerVertices.Clear();
        outterVertices.Clear();

        float degreeDelta = (float)(2 * Mathf.PI / segements);
        int curSegements = (int)(segements * fillPercent);

        float tw = rectTransform.rect.width;
        float th = rectTransform.rect.height;
        float outerRadius = rectTransform.pivot.x * tw;
        float innerRadius = rectTransform.pivot.x * tw - thickness;

        Vector4 uv = overrideSprite != null ? DataUtility.GetOuterUV(overrideSprite) : Vector4.zero;

        float uvCenterX = (uv.x + uv.z) * 0.5f;
        float uvCenterY = (uv.y + uv.w) * 0.5f;
        float uvScaleX = (uv.z - uv.x) / tw;
        float uvScaleY = (uv.w - uv.y) / th;

        float curDegree = 0;
        UIVertex uiVertex;
        int verticeCount;
        int triangleCount;
        Vector2 curVertice;

        if (fill) //圆形
        {
            curVertice = Vector2.zero;
            verticeCount = curSegements + 1;
            uiVertex = new UIVertex();
            uiVertex.color = color;
            uiVertex.position = curVertice;
            uiVertex.uv0 = new Vector2(curVertice.x * uvScaleX + uvCenterX, curVertice.y * uvScaleY + uvCenterY);
            vh.AddVert(uiVertex);

            for (int i = 1; i < verticeCount; i++)
            {
                float cosA = Mathf.Cos(curDegree);
                float sinA = Mathf.Sin(curDegree);
                curVertice = new Vector2(cosA * outerRadius, sinA * outerRadius);
                curDegree += degreeDelta;

                uiVertex = new UIVertex();
                uiVertex.color = color;
                uiVertex.position = curVertice;
                uiVertex.uv0 = new Vector2(curVertice.x * uvScaleX + uvCenterX, curVertice.y * uvScaleY + uvCenterY);
                vh.AddVert(uiVertex);

                outterVertices.Add(curVertice);
            }

            triangleCount = curSegements * 3;
            for (int i = 0, vIdx = 1; i < triangleCount - 3; i += 3, vIdx++)
            {
                vh.AddTriangle(vIdx, 0, vIdx + 1);
            }
            if (fillPercent == 1)
            {
                //首尾顶点相连
                vh.AddTriangle(verticeCount - 1, 0, 1);
            }
        }
        else//圆环
        {
            verticeCount = curSegements * 2;
            for (int i = 0; i < verticeCount; i += 2)
            {
                float cosA = Mathf.Cos(curDegree);
                float sinA = Mathf.Sin(curDegree);
                curDegree += degreeDelta;

                curVertice = new Vector3(cosA * innerRadius, sinA * innerRadius);
                uiVertex = new UIVertex();
                uiVertex.color = color;
                uiVertex.position = curVertice;
                uiVertex.uv0 = new Vector2(curVertice.x * uvScaleX + uvCenterX, curVertice.y * uvScaleY + uvCenterY);
                vh.AddVert(uiVertex);
                innerVertices.Add(curVertice);

                curVertice = new Vector3(cosA * outerRadius, sinA * outerRadius);
                uiVertex = new UIVertex();
                uiVertex.color = color;
                uiVertex.position = curVertice;
                uiVertex.uv0 = new Vector2(curVertice.x * uvScaleX + uvCenterX, curVertice.y * uvScaleY + uvCenterY);
                vh.AddVert(uiVertex);
                outterVertices.Add(curVertice);
            }

            triangleCount = curSegements * 3 * 2;
            for (int i = 0, vIdx = 0; i < triangleCount - 6; i += 6, vIdx += 2)
            {
                vh.AddTriangle(vIdx + 1, vIdx, vIdx + 3);
                vh.AddTriangle(vIdx, vIdx + 2, vIdx + 3);
            }
            if (fillPercent == 1)
            {
                //首尾顶点相连
                vh.AddTriangle(verticeCount - 1, verticeCount - 2, 1);
                vh.AddTriangle(verticeCount - 2, 0, 1);
            }
        }

    }

    public virtual bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        Sprite sprite = overrideSprite;
        if (sprite == null)
            return true;

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out local);
        return Contains(local, outterVertices, innerVertices);
    }

    private bool Contains(Vector2 p, List<Vector3> outterVertices, List<Vector3> innerVertices)
    {
        var crossNumber = 0;
        RayCrossing(p, innerVertices, ref crossNumber);//检测内环
        RayCrossing(p, outterVertices, ref crossNumber);//检测外环
        return (crossNumber & 1) == 1;
    }

    /// <summary>
    /// 使用RayCrossing算法判断点击点是否在封闭多边形里
    /// </summary>
    /// <param name="p"></param>
    /// <param name="vertices"></param>
    /// <param name="crossNumber"></param>
    private void RayCrossing(Vector2 p, List<Vector3> vertices, ref int crossNumber)
    {
        for (int i = 0, count = vertices.Count; i < count; i++)
        {
            var v1 = vertices[i];
            var v2 = vertices[(i + 1) % count];

            //点击点水平线必须与两顶点线段相交
            if (((v1.y <= p.y) && (v2.y > p.y))
                || ((v1.y > p.y) && (v2.y <= p.y)))
            {
                //只考虑点击点右侧方向，点击点水平线与线段相交，且交点x > 点击点x，则crossNumber+1
                if (p.x < v1.x + (p.y - v1.y) / (v2.y - v1.y) * (v2.x - v1.x))
                {
                    crossNumber += 1;
                }
            }
        }
    }



    /// <summary>
    /// Image's texture comes from the UnityEngine.Image.
    /// </summary>
    public override Texture mainTexture
    {
        get
        {
            return overrideSprite == null ? s_WhiteTexture : overrideSprite.texture;
        }
    }

    public float pixelsPerUnit
    {
        get
        {
            float spritePixelsPerUnit = 100;
            if (sprite)
                spritePixelsPerUnit = sprite.pixelsPerUnit;

            float referencePixelsPerUnit = 100;
            if (canvas)
                referencePixelsPerUnit = canvas.referencePixelsPerUnit;

            return spritePixelsPerUnit / referencePixelsPerUnit;
        }
    }

    // /// <summary>
    // /// 子类需要重写该方法来自定义Image形状
    // /// </summary>
    // /// <param name="vh"></param>
    // protected override void OnPopulateMesh(VertexHelper vh)
    // {
    //     base.OnPopulateMesh(vh);
    // }

    #region ISerializationCallbackReceiver
    public void OnAfterDeserialize()
    {
    }

    //
    // 摘要: 
    //     Implement this method to receive a callback after unity serialized your object.
    public void OnBeforeSerialize()
    {
    }
    #endregion

    #region ILayoutElement
    public virtual void CalculateLayoutInputHorizontal() { }
    public virtual void CalculateLayoutInputVertical() { }

    public virtual float minWidth { get { return 0; } }

    public virtual float preferredWidth
    {
        get
        {
            if (overrideSprite == null)
                return 0;
            return overrideSprite.rect.size.x / pixelsPerUnit;
        }
    }

    public virtual float flexibleWidth { get { return -1; } }

    public virtual float minHeight { get { return 0; } }

    public virtual float preferredHeight
    {
        get
        {
            if (overrideSprite == null)
                return 0;
            return overrideSprite.rect.size.y / pixelsPerUnit;
        }
    }

    public virtual float flexibleHeight { get { return -1; } }

    public virtual int layoutPriority { get { return 0; } }
    #endregion

    #region ICanvasRaycastFilter
    // public virtual bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    // {
    //     return true;
    // }
    #endregion



}

internal static class SetPropertyUtilityExt
{
    public static bool SetColor(ref Color currentValue, Color newValue)
    {
        if (currentValue.r == newValue.r && currentValue.g == newValue.g && currentValue.b == newValue.b && currentValue.a == newValue.a)
            return false;

        currentValue = newValue;
        return true;
    }

    public static bool SetStruct<T>(ref T currentValue, T newValue) where T : struct
    {
        if (currentValue.Equals(newValue))
            return false;

        currentValue = newValue;
        return true;
    }

    public static bool SetClass<T>(ref T currentValue, T newValue) where T : class
    {
        if ((currentValue == null && newValue == null) || (currentValue != null && currentValue.Equals(newValue)))
            return false;

        currentValue = newValue;
        return true;
    }
}