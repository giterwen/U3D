//----------------------------------------------
//            NGUI: Next-Gen UI kit
// Copyright © 2011-2015 Tasharen Entertainment
//----------------------------------------------

using UnityEngine;
using System.Collections.Generic;
using System;
/// <summary>
/// This script makes it possible for a scroll view to wrap its content, creating endless scroll views.
/// Usage: simply attach this script underneath your scroll view where you would normally place a UIGrid:
/// 
/// + Scroll View
/// |- UIWrappedContent
/// |-- Item 1
/// |-- Item 2
/// |-- Item 3
/// </summary>

[AddComponentMenu("NGUI/Interaction/Wrap Content Ex")]
public class UIWrapContentEx : MonoBehaviour
{
    public delegate void OnInitializeItem(GameObject go, int realIndex, int rowIndex);
    public delegate void OnListOnTop(bool b);
    public delegate void OnListOnBottom(bool b);

	public enum ShowCondition
	{
		Always,
		OnlyIfNeeded,
		WhenDragging,
	}

    bool onTop = false;
    bool onBottom = false;
    //bool onLeft = false;
    //bool onRight = false;

    /// <summary>
    /// Width or height of the child items for positioning purposes.
    /// </summary>
    public int itemXSpace = 20;       //列间距
    public int itemYSpace = 20;       //行间距
    public int rowNumber = 1;
    float beginX;
    float beginY;

    public int itemCount = 0;
    int pageCount = 0;

    bool bDirty = true;
    public bool dirty
    {
        set
        {
            bDirty = value;
        }
    }


    public bool bLoop = true;

    /// <summary>
    /// Whether the content will be automatically culled. Enabling this will improve performance in scroll views that contain a lot of items.
    /// </summary>
    public bool cullContent = true;

    /// <summary>
    /// Callback that will be called every time an item needs to have its content updated.
    /// The 'wrapIndex' is the index within the child list, and 'realIndex' is the index using position logic.
    /// </summary>
    public OnInitializeItem onInitializeItem;

    public UIScrollView.OnDragNotification onScrollFinish;

    public OnListOnTop onListOnTop;
    public OnListOnBottom onListOnBottom;


    Transform mTrans;
    UIPanel mPanel;
    public UIScrollView mScroll;
    Vector4 mScrollBeginPos;
    Vector2 mScrollBeginOffset;

    Vector4 mScrollBeginPos_Orignal;
    Vector2 mScrollBeginOffset_Orignal;

    bool mHorizontal = false;
    bool mFirstTime = true;
    List<Transform> mChildren = new List<Transform>();

    public UIScrollBar scrollBar;
	public ShowCondition showScrollBars = ShowCondition.OnlyIfNeeded;

    float fBarValue;
    public float barValue
    {
        set
        {
            fBarValue = value;
            if (scrollBar != null)
            {
                scrollBar.value = fBarValue;
            }

            //TODO:目前只考虑垂直
            if (mHorizontal)
            {
            }
            else
            {
                CheckBarEvent(value);
            }

        }
        get
        {
            return fBarValue;
        }
    }

    void CheckBarEvent(float value)
    {
        if (!mHorizontal)
        {
            if(mScroll == null||mScroll.panel == null )
            {
                //Debug.LogWarning("111");
            }
            
            if (mScroll != null &&mScroll.panel != null&&!mScroll.shouldMoveVertically)
            {
                onTop = true;
                if (null != onListOnTop) onListOnTop(onTop);
                onBottom = true;
                if (null != onListOnBottom) onListOnBottom(onBottom);
            }
            else
            {
                if (Mathf.Abs(value) < 0.01f)
                {
                    if (!onTop)
                    {
                        onTop = true;
                        if (null != onListOnTop) onListOnTop(onTop);
                    }

                    onBottom = false;
                    if (null != onListOnBottom) onListOnBottom(onBottom);
                }
                else if (Mathf.Abs(value - 1) < 0.01f)
                {
                    if (!onBottom)
                    {
                        onBottom = true;
                        if (null != onListOnBottom) onListOnBottom(onBottom);

                    }


                    onTop = false;
                    if (null != onListOnTop) onListOnTop(onTop);

                }
                else
                {
                    if (onTop)
                    {
                        onTop = false;
                        if (null != onListOnTop) onListOnTop(onTop);
                    }
                    if (onBottom)
                    {
                        onBottom = false;
                        if (null != onListOnBottom) onListOnBottom(onBottom);
                    }
                }
            }
        }
        else
        {
            //TODO:
        }

    }

    bool mIgnoreCallbacks = false;
    /// <summary>
    /// Initialize everything and register a callback with the UIPanel to be notified when the clipping region moves.
    /// </summary>
    protected virtual void Start()
    {
        ResetContent(itemCount);
    }
    protected virtual void Awake()
    {
        SortBasedOnScrollMovement();

        //ResetChildPositions();
        //ResetContent(itemCount);
    }

    public void ResetContent(int iCount, bool bRePos = true)
    {
//         if (this.gameObject.GetComponent<DragPlaySound>() == null)
//         {
//             dragPlaySound = this.gameObject.AddComponent<DragPlaySound>();
//             dragPlaySound.audioClip = UIManager.instance.ScrollViewDragSound;
//             dragPlaySound.volume = UIManager.instance.ScrollViewDragSoundVolume;
// 
//         }
        if (bRePos)
        {
            if (mScroll != null)
            {
                mScroll.transform.localPosition = mScrollBeginPos_Orignal;
                mScroll.restrictWithinPanel = false;
                mScroll.DisableSpring();
            }
        }

        if (bRePos)
        {
            if (mPanel != null)
            {
                mPanel.clipOffset = mScrollBeginOffset_Orignal;
            }

            ResetChildPositions();
        }


        bDirty = true;
        itemCount = iCount;

        mFirstTime = true;
        WrapContent();
        mFirstTime = false;

        if (bRePos)
        {
            barValue = 0;
        }


        mIgnoreCallbacks = true;
        Processbar();

        CheckBarEvent(barValue);

        mIgnoreCallbacks = false;
    }


    public void RefreshContent(int iCount)
    {
        bDirty = true;
        itemCount = iCount;

        mFirstTime = true;
        WrapContent();
        mFirstTime = false;

        mIgnoreCallbacks = true;
        Processbar();
        mIgnoreCallbacks = false;
    }


    void ClampInItem()
    {

    }


    public void Drag(Vector3 offPos, float fStrength)
    {
        //SpringPanel sp = mScroll.GetComponent<SpringPanel>();
        //if (sp != null && sp.enabled )
        //    return;

        //TODO:临时实现，只考虑垂直
        if (mHorizontal)
        {
        }
        else
        {
            float fOver = (mPanel.clipOffset.y + offPos.y) % itemYSpace;
            offPos.y -= fOver;

            Vector3 pos = mScroll.transform.localPosition;

            if (mPanel.clipOffset.y + offPos.y >= 0)
            {
                pos.y = mScrollBeginPos_Orignal.y;
            }
            else if (mPanel.clipOffset.y + offPos.y <= -(itemYSpace * itemCount / rowNumber - mPanel.finalClipRegion.w))
            {
                pos.y = itemYSpace * itemCount / rowNumber - mPanel.finalClipRegion.w + mScrollBeginPos_Orignal.y;
            }
            else
            {
                pos = mScroll.transform.localPosition - offPos;
            }

            SpringPanel.Begin(mScroll.gameObject, pos, fStrength);
        }


    }


    public void SetScrollViewDragFinish(UIScrollView.OnDragNotification onDragFinished_)
    {
        onScrollFinish = onDragFinished_;
        if (mScroll == null) return;

        if (onScrollFinish != null)
            mScroll.onDragFinished = onScrollFinish;
    }
    /// <summary>
    /// Cache the scroll view and return 'false' if the scroll view is not found.
    /// </summary>
    protected bool CacheScrollView()
    {
        mTrans = transform;
        mPanel = NGUITools.FindInParents<UIPanel>(gameObject);
        mScroll = mPanel.GetComponent<UIScrollView>();
        if (mScroll == null) return false;

        if (onScrollFinish != null && mScroll.onDragFinished == null)
            mScroll.onDragFinished = onScrollFinish;

        mScrollBeginPos = mScroll.transform.localPosition;
        mScrollBeginPos_Orignal = mScrollBeginPos;

        mScrollBeginOffset = mPanel.clipOffset;
        mScrollBeginOffset_Orignal = mScrollBeginOffset;

        if (mScroll.movement == UIScrollView.Movement.Horizontal) mHorizontal = true;
        else if (mScroll.movement == UIScrollView.Movement.Vertical) mHorizontal = false;
        else return false;

        if (mScroll != null)
        {
            mScroll.GetComponent<UIPanel>().onClipMove = OnMove;
            if (scrollBar != null && scrollBar.onChange.Count == 0)
            {
                EventDelegate.Add(scrollBar.onChange, OnScrollBar);
            }

            mScroll.onCustomConstraint = OnCustomConstraint;
        }


        return true;
    }

    public void OnScrollBar()
    {
        if (scrollBar == null)
            return;

        if (!mIgnoreCallbacks)
        {
            mIgnoreCallbacks = true;
            float value = scrollBar.value;
            //Debug.LogWarning("OnScrollBar  == " + value);

            mScroll.DisableSpring();
            if (mHorizontal)
            {
                float beginX = mScrollBeginPos.x;
                int lineCount = (itemCount / rowNumber) + ((itemCount % rowNumber) != 0 ? 1 : 0);
                float totalX = lineCount * itemXSpace - mPanel.finalClipRegion.z;
                float ox = Mathf.Lerp(beginX, -totalX, value);

                mScroll.transform.localPosition = new Vector3(ox, mScroll.transform.localPosition.y, mScroll.transform.localPosition.z);
                Vector4 cr = mPanel.baseClipRegion;
                mPanel.clipOffset = new Vector2(beginX -ox + mScrollBeginOffset.x, cr.y + mScrollBeginOffset.y);
            }
            else
            {
                float beginY = mScrollBeginPos.y;
                int lineCount = (itemCount / rowNumber) + ((itemCount % rowNumber) != 0 ? 1 : 0);
                float totalY = lineCount * itemYSpace - mPanel.finalClipRegion.w + beginY;
                float ox = Mathf.Lerp(beginY, totalY, value);

                //Debug.LogWarning("bar value:" + value.ToString() );

                mScroll.transform.localPosition = new Vector3(mScroll.transform.localPosition.x, ox, mScroll.transform.localPosition.z);
                //Debug.LogWarning("bar localPosition y:" + mScroll.transform.localPosition.y.ToString());
                Vector4 cr = mPanel.baseClipRegion;
                mPanel.clipOffset = new Vector2(cr.x + mScrollBeginOffset.x, beginY - ox + mScrollBeginOffset.y);
            }

            mIgnoreCallbacks = false;
        }


    }

    public Vector3 OnCustomConstraint(Bounds bounds)
    {
        Vector3 constraint = Vector3.zero;
        if (mHorizontal)
        {
            //TODO:这里貌似没测试过
            float x = mScroll.transform.localPosition.x;
            int lineCount = (itemCount / rowNumber) + ((itemCount % rowNumber) != 0 ? 1 : 0);
            float lineSize = lineCount * itemXSpace;
            float fTotalX = lineSize - mPanel.finalClipRegion.z;

            if (fTotalX <= 0)
            {
                constraint = mPanel.clipOffset;
            }
            else
            {
                if (x > mScrollBeginPos.x)
                {
                    constraint = mPanel.clipOffset;
                }
                else if (mScrollBeginPos.x - x + mPanel.finalClipRegion.z > lineSize)
                {
                    //constraint = mPanel.CalculateConstrainOffset(bounds.min, bounds.max);
                    constraint = new Vector3(lineSize - (x - mScrollBeginPos.x + mPanel.finalClipRegion.z), 0, 0);
                }
            }
        }
        else
        {
            float y = mScroll.transform.localPosition.y;
            int lineCount = (itemCount / rowNumber) + ((itemCount % rowNumber) != 0 ? 1 : 0);
            float lineSize = lineCount * itemYSpace;
            float fTotalY = lineSize - mPanel.finalClipRegion.w;


            if (fTotalY <= 0)
            {
                constraint = mPanel.clipOffset;
            }
            else
            {
                if (y <= mScrollBeginPos.y)
                {
                    constraint = mPanel.clipOffset;
                }
                else if (y - mScrollBeginPos.y + mPanel.finalClipRegion.w > lineSize)
                {
                     //constraint = mPanel.CalculateConstrainOffset(bounds.min, bounds.max);
                   constraint = new Vector3(0, lineSize - (y - mScrollBeginPos.y + mPanel.finalClipRegion.w), 0);
                }
            }
        }

        return constraint;
    }


    public void Processbar()
    {
        if (mHorizontal)
        {
            ProcessHorizontalbar();
        }
        else
        {
            ProcessVerticalbar();
        }

        //xumj 对滚动条进行隐藏\
        if (scrollBar)
			scrollBar.alpha = ((showScrollBars == ShowCondition.Always) || scrollBar.barSize != 1f) ? 1f : 0f;
    }

    void ProcessHorizontalbar()
    {
		if (mScroll == null)
			return;
		if (mPanel == null) {
			return;
		}
        if (!bLoop)
        {
            float x = mScroll.transform.localPosition.x;
            int lineCount = (itemCount / rowNumber) + ((itemCount % rowNumber) != 0 ? 1 : 0);
            float lineSize = lineCount * itemXSpace;
            float fTotalX = lineSize - mPanel.finalClipRegion.z;

            float value = 0;
            float offX = 0;
            if (fTotalX <= 0)
            {
                if (x <= mScrollBeginPos.x)
                {
                    offX = mScrollBeginPos.x - x;
                    value = 0;
                }
                else if (x - mScrollBeginPos.x + lineSize > mPanel.finalClipRegion.z)
                {
                    offX = x - mScrollBeginPos.x + lineSize - mPanel.finalClipRegion.z;
                    value = 1;
                }
                else
                {
                    value = 0;
                }
                if (scrollBar != null)
                {
                    scrollBar.barSize = Mathf.Clamp(lineSize / (lineSize + offX), 0.05f, 1.0f);
                }
            }
            else
            {
                if (x > mScrollBeginPos.x)
                {
                    offX = x - mScrollBeginPos.x;
                    value = 0;
                }
                else if (mScrollBeginPos.x - x + mPanel.finalClipRegion.z > lineSize)
                {
                    offX = mScrollBeginPos.x - x + mPanel.finalClipRegion.z - lineSize;
                    value = 1;
                }
                else
                {
                    value = (mScrollBeginPos.x - x) / fTotalX;
                }

                if (scrollBar != null)
                {
                    scrollBar.barSize = Mathf.Clamp(mPanel.finalClipRegion.z / (lineSize + offX), 0.05f, 1.0f);
                }
            }

            //scrollBar.value = value;
            barValue = value;
        }
    }


    void ProcessVerticalbar()
    {
        if (mScroll == null)
            return;
		if (mPanel == null) {
			return;
		}
        if (!bLoop)
        {
            float y = mScroll.transform.localPosition.y;
            int lineCount = (itemCount / rowNumber) + ((itemCount % rowNumber) != 0 ? 1 : 0);
            float lineSize = lineCount * itemYSpace;
            float fTotalY = lineSize - mPanel.finalClipRegion.w;


            float value = 0;
            float offY = 0;
            if (fTotalY <= 0)
            {
                if (y <= mScrollBeginPos.y)
                {
                    offY = mScrollBeginPos.y - y;
                    value = 0;
                }
                else if (y - mScrollBeginPos.y + lineSize > mPanel.finalClipRegion.w)
                {
                    offY = y - mScrollBeginPos.y + lineSize - mPanel.finalClipRegion.w;
                    value = 1;
                }
                else
                {
                    value = 0;
                }

                if (scrollBar != null)
                {
                    scrollBar.barSize = Mathf.Clamp(lineSize / (lineSize + offY), 0.05f, 1.0f);
                }
            }
            else
            {
                if (y <= mScrollBeginPos.y)
                {
                    offY = mScrollBeginPos.y - y;
                    value = 0;
                }
                else if (y - mScrollBeginPos.y + mPanel.finalClipRegion.w > lineSize)
                {
                    offY = y - mScrollBeginPos.y + mPanel.finalClipRegion.w - lineSize;
                    value = 1;
                }
                else
                {
                    value = (y - mScrollBeginPos.y) / fTotalY;
                }

                if (scrollBar != null)
                {
                    scrollBar.barSize = Mathf.Clamp(mPanel.finalClipRegion.w / (lineSize + offY), 0.05f, 1.0f);
                }
            }

            //if (scrollBar != null)
            //{
            //    scrollBar.value = value;
            //}

            barValue = value;
        }
    }


    /// <summary>
    /// Callback triggered by the UIPanel when its clipping region moves (for example when it's being scrolled).
    /// </summary>
    protected virtual void OnMove(UIPanel panel)
    {
        WrapContent();
        if (!mIgnoreCallbacks)
        {

            mIgnoreCallbacks = true;
            Processbar();
            mIgnoreCallbacks = false;
        }
    }

    /// <summary>
    /// Immediately reposition all children.
    /// </summary>

    [ContextMenu("Sort Based on Scroll Movement")]
    public void SortBasedOnScrollMovement()
    {
        if (!CacheScrollView()) return;

        // Cache all children and place them in order
        mChildren.Clear();
        for (int i = 0; i < mTrans.childCount; ++i)
            mChildren.Add(mTrans.GetChild(i));

        if (itemCount == 0)
        {
            itemCount = mChildren.Count;
        }

        // Sort the list of children so that they are in order
        //if (mHorizontal) mChildren.Sort(UIGrid.SortHorizontal);
        //else mChildren.Sort(UIGrid.SortVertical);
        ResetChildPositions();
    }



    /// <summary>
    /// Helper function that resets the position of all the children.
    /// </summary>
    void ResetChildPositions()
    {
        if (rowNumber == 0)
            return;
        if (mPanel == null)
            return;

        beginX = -mPanel.baseClipRegion.z / 2 + itemXSpace / 2 + mPanel.baseClipRegion.x;
        beginY = mPanel.baseClipRegion.w / 2 - itemYSpace / 2 + mPanel.baseClipRegion.y;

        beginX -= transform.localPosition.x;
        beginY -= transform.localPosition.y;

        beginX += mPanel.clipOffset.x;
        beginY += mPanel.clipOffset.y;

        float curX = beginX;
        float curY = beginY;
        int rowIndex = 0;
        for (int i = 0, imax = mChildren.Count; i < imax; ++i)
        {
            Transform t = mChildren[i];
            t.localPosition = new Vector3(curX, curY, 0);
            if (mHorizontal)
            {
                if (((i + 1) % rowNumber == 0))
                {
                    curY = beginY;
                    curX += itemXSpace;

                    rowIndex++;
                }
                else
                {
                    curY -= itemYSpace;
                }
            }
            else
            {
                if (((i + 1) % rowNumber == 0))
                {
                    curX = beginX;
                    curY -= itemYSpace;

                    rowIndex++;
                }
                else
                {
                    curX += itemXSpace;
                }
            }
            //UpdateItem(t, i, rowIndex);

            //if (Application.isPlaying && !Application.isEditor)
            if (Application.isPlaying )
                NGUITools.SetActive(t.gameObject, false, false);
        }

        if (mHorizontal)
        {
            pageCount = rowNumber * ((int)(mPanel.baseClipRegion.z) / itemXSpace);
        }
        else
        {
            pageCount = rowNumber * ((int)(mPanel.baseClipRegion.w) / itemYSpace);
        }
    }

	public void ScrollToIndex(int index)
	{
		float targetPercent = barValue;
		
		int RowTotal = (itemCount / rowNumber + ((itemCount % rowNumber) != 0 ? 1 : 0));
		if (RowTotal == 0)
			return;
		
		int indexRow = index / rowNumber;
		
		if (mHorizontal)
		{
			int showTotal = (int)mPanel.finalClipRegion.z / itemXSpace;
			
			targetPercent = (float)indexRow / (float)(RowTotal - showTotal);
			
		}
		else
		{
			//没测试过
			int showTotal = (int)mPanel.finalClipRegion.w / itemYSpace;
			
			targetPercent = (float)indexRow / (float)(RowTotal - showTotal);
		}
		barValue = targetPercent;
	}

    ///// <summary>
    ///// Helper function that resets the position of all the children.
    ///// </summary>
    //void ResetChildPositions()
    //{
    //    if (rowNumber == 0)
    //        return;

    //    beginX = -mPanel.baseClipRegion.z / 2 + itemXSpace / 2 + mPanel.baseClipRegion.x;
    //    beginY = mPanel.baseClipRegion.w / 2 - itemYSpace / 2 + mPanel.baseClipRegion.y;

    //    beginX -= transform.localPosition.x;
    //    beginY -= transform.localPosition.y;
    //    float curX = beginX;
    //    float curY = beginY;
    //    int rowIndex = 0;
    //    for (int i = 0, imax = mChildren.Count; i < imax; ++i)
    //    {
    //        Transform t = mChildren[i];
    //        t.localPosition = new Vector3(curX, curY, 0);
    //        if (mHorizontal)
    //        {
    //            if (((i + 1) % rowNumber == 0))
    //            {
    //                curY = beginY;
    //                curX += itemXSpace;

    //                rowIndex++;
    //            }
    //            else
    //            {
    //                curY -= itemYSpace;
    //            }
    //        }
    //        else
    //        {
    //            if (((i + 1) % rowNumber == 0))
    //            {
    //                curX = beginX;
    //                curY -= itemYSpace;

    //                rowIndex++;
    //            }
    //            else
    //            {
    //                curX += itemXSpace;
    //            }
    //        }
    //        //UpdateItem(t, i, rowIndex);
    //        NGUITools.SetActive(t.gameObject, false, false);
    //    }

    //    if (mHorizontal)
    //    {
    //        pageCount = rowNumber * ((int)(mPanel.baseClipRegion.z) / itemYSpace);
    //    }
    //    else
    //    {
    //        pageCount = rowNumber * ((int)(mPanel.baseClipRegion.w) / itemXSpace);
    //    }
    //}


    int CutOffRealIndex(int iReadIndex)
    {
        if (itemCount <= 0)
            return 0;

        while (iReadIndex < 0)
        {
            iReadIndex += itemCount;
        }

        if (iReadIndex >= itemCount)
        {
            iReadIndex = iReadIndex % itemCount;
        }

        return iReadIndex;
    }


    /// <summary>
    /// Wrap all content, repositioning all children as needed.
    /// </summary>
    /// 
    //float fffff_LastCenterY = 0;
    private void WrapVerticalContent()
    {
        if (mPanel == null)
            return;

        int lineCount = (mChildren.Count / rowNumber) + ((mChildren.Count % rowNumber) != 0 ? 1 : 0);
        //拥有控件的总共大小的一半
        float extents = itemYSpace * lineCount * 0.5f;
        Vector3[] corners = mPanel.worldCorners;

        for (int i = 0; i < 4; ++i)
        {
            Vector3 v = corners[i];
            v = mTrans.InverseTransformPoint(v);
            corners[i] = v;
        }

        Vector3 center = Vector3.Lerp(corners[0], corners[2], 0.5f);
        //Debug.LogWarning("center :" + center.ToString());
        //if( Mathf.Abs(fffff_LastCenterY - center.y) > 2000 )
        //{
        //    Debug.LogError("fffff_LastCenterY 大于 2000");
        //}
        //fffff_LastCenterY = center.y;
        bool allWithinRange = true;
        float ext2 = extents * 2f;

        float min = corners[0].y - itemYSpace;
        float max = corners[2].y + itemYSpace;

        for (int i = 0, imax = mChildren.Count; i < imax; ++i)
        {
            Transform t = mChildren[i];
            Vector3 pos = t.localPosition;

            int rowIndex = Mathf.RoundToInt((beginY - pos.y) / itemYSpace);
            int tempIndex = rowIndex * rowNumber + i % rowNumber;
            //realIndex = CutOffRealIndex(realIndex);

            if (tempIndex >= itemCount && itemCount <= pageCount)
            {
                NGUITools.SetActive(t.gameObject, false, false);
                allWithinRange = false;
                continue;
            }


            float distance = t.localPosition.y - center.y;
            bool bNeedRefresh = false;

            if (mFirstTime && tempIndex < itemCount)
            {
                int iColIndex = 0;
                if (rowNumber > 1 && itemXSpace > 0)
                {
                    iColIndex = ((int)(pos.x - corners[0].x) + itemXSpace / 2) / itemXSpace - 1;
                }

                UpdateItem(t, tempIndex, iColIndex);
                continue;
            }
            //往上移动时，下面的item超出了veiw的范围,然后把下面的控件移动到上面(y++++)
            //计算索引，回调刷新
            else if (distance < -extents)
            {
                while (distance < -extents)
                {
                    pos.y += ext2;                                              //移动到上面
                    distance = pos.y - center.y;                               //记录新距离
                }

                bNeedRefresh = true;
            }
            //往下移动时，上面的item超出了veiw的范围
            else if (distance > extents)
            {
                while (distance > extents)
                {
                    pos.y -= ext2;
                    distance = pos.y - center.y;
                }

                bNeedRefresh = true;
            }
            else if (bDirty)
            {
                bNeedRefresh = true;
            }


            rowIndex = Mathf.RoundToInt((beginY - pos.y) / itemYSpace);

            int realIndex = rowIndex * rowNumber + i % rowNumber;

            if (!bLoop)
            {
                int dataLineCount = (itemCount / rowNumber) + ((itemCount % rowNumber) != 0 ? 1 : 0);

                if (rowIndex < 0 || rowIndex > dataLineCount || realIndex >= itemCount)
                {
                    allWithinRange = false;
                    //NGUITools.SetActive(t.gameObject, false, false);
                    continue;
                }
            }
            if (bNeedRefresh)
            {
                realIndex = CutOffRealIndex(realIndex);
                //Debug.LogWarning("realIndex: " + realIndex.ToString());

                if (realIndex >= 0 && realIndex < itemCount)
                {
                    int a = itemXSpace - 1;
                    if(a == 0)
                    {
                        a = 1;
                    }
                    int iColIndex = ((int)(pos.x - corners[0].x) + itemXSpace / 2) / a;

                    t.localPosition = pos;
                    UpdateItem(t, realIndex, iColIndex);
                }
                else
                {
                    allWithinRange = false;
                }
            }

            if (cullContent && itemCount >= pageCount)
            {
                distance += mPanel.clipOffset.y - mTrans.localPosition.y;
                if (!UICamera.IsPressed(t.gameObject))
                {
                    bool bCull = (distance > min && distance < max);
                    NGUITools.SetActive(t.gameObject, bCull, false);

                }
            }
        }

        mScroll.restrictWithinPanel = !allWithinRange;

        bDirty = false;
    }


    private void WrapHorizontalContent()
    {
        int lineCount = (mChildren.Count / rowNumber) + ((mChildren.Count % rowNumber) != 0 ? 1 : 0);
        //拥有控件的总共大小的一半
        float extents = itemXSpace * lineCount * 0.5f;
        Vector3[] corners = mPanel.worldCorners;

        for (int i = 0; i < 4; ++i)
        {
            Vector3 v = corners[i];
            v = mTrans.InverseTransformPoint(v);
            corners[i] = v;
        }

        Vector3 center = Vector3.Lerp(corners[0], corners[2], 0.5f);
        bool allWithinRange = true;
        float ext2 = extents * 2f;

        float min = corners[0].x - itemXSpace;
        float max = corners[2].x + itemXSpace;

		//Horizontal
        for (int i = 0, imax = mChildren.Count; i < imax; ++i)
        {
            Transform t = mChildren[i];
			Vector3 pos = t.localPosition;

			int rowIndex = Mathf.RoundToInt((pos.x - beginX) / itemXSpace);
            int tempIndex = rowIndex * rowNumber + i % rowNumber;
			

			
            if (i >= itemCount && itemCount <= pageCount)
            {
                NGUITools.SetActive(t.gameObject, false, false);
                allWithinRange = false;
                continue;
            }

            float distance = t.localPosition.x - center.x;
            bool bNeedRefresh = false;

            if (mFirstTime && tempIndex < itemCount)
            {
                int iColIndex = 0;
                if (rowNumber > 1 && itemXSpace > 0)
                {
                    iColIndex = ((int)(corners[1].y - pos.y) + itemYSpace / 2) / itemYSpace - 1;
                }

                UpdateItem(t, tempIndex, iColIndex);
                continue;
            }
            //往上移动时，下面的item超出了veiw的范围,然后把下面的控件移动到上面(y++++)
            //计算索引，回调刷新
            else if (distance < -extents)
            {
                while (distance < -extents)
                {
                    pos.x += ext2;                                              //移动到上面
                    distance = pos.x - center.x;                                //记录新距离
                }

                bNeedRefresh = true;
            }
            //往下移动时，上面的item超出了veiw的范围
            else if (distance > extents)
            {
                while (distance > extents)
                {
                    pos.x -= ext2;
                    distance = pos.x - center.x;
                }

                bNeedRefresh = true;
            }
            else if (bDirty)
            {
                bNeedRefresh = true;
            }

            rowIndex = Mathf.RoundToInt((pos.x - beginX) / itemXSpace);

            int realIndex = rowIndex * rowNumber + i % rowNumber;

			
            if (!bLoop)
            {
                int dataLineCount = (itemCount / rowNumber) + ((itemCount % rowNumber) != 0 ? 1 : 0);

                if (rowIndex < 0 || rowIndex > dataLineCount || realIndex >= itemCount)
                {
                    allWithinRange = false;
                    //NGUITools.SetActive(t.gameObject, false, false);
                    continue;
                }
            }

            if (bNeedRefresh)
            {

                realIndex = CutOffRealIndex(realIndex);
                if (realIndex >= 0 && realIndex < itemCount)
                {
                    int iColIndex = ((int)(corners[1].y - pos.y) + itemYSpace / 2) / itemYSpace - 1;

                    t.localPosition = pos;
                    UpdateItem(t, realIndex, iColIndex);
                }
                else
                {
                    allWithinRange = false;
                }
            }

            if (cullContent && itemCount >= pageCount)
            {
                distance += mPanel.clipOffset.x - mTrans.localPosition.x;
                if (!UICamera.IsPressed(t.gameObject))
                {
                    bool bCull = (distance > min && distance < max);
                    NGUITools.SetActive(t.gameObject, bCull, false);

                }
            }
        }

        mScroll.restrictWithinPanel = !allWithinRange;

        bDirty = false;
    }

    public void WrapContent()
    {
        if (mHorizontal)
        {
            WrapHorizontalContent();
        }
        else
        {
            WrapVerticalContent();
        }
    }


    /// <summary>
    /// Want to update the content of items as they are scrolled? Override this function.
    /// </summary>
    protected virtual void UpdateItem(Transform item, int index, int rowIndex)
    {
       
        NGUITools.SetActive(item.gameObject, true, false);
        if (onInitializeItem != null)
        {
            onInitializeItem(item.gameObject, index, rowIndex);

//             if (this.gameObject.GetComponent<UIPlaySound>() == null && UIManager.instance != null)
//             {
//                 UIPlaySound dragPlaySound = this.gameObject.AddComponent<UIPlaySound>();
//                 dragPlaySound.audioClip = UIManager.instance.ScrollViewDragSound;
//                 dragPlaySound.volume = UIManager.instance.ScrollViewDragSoundVolume;
// 
//             }
//             
//             NGUITools.PlaySound(this.GetComponent<UIPlaySound>().audioClip);
//             Debug.LogError("ScrollViewDragSound"+"---"+TimeManager.instance.GetServerTime() );
        }
    }
}
