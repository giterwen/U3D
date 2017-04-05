using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;


public class WrapVo 
{
    public int index;
}


public abstract class  WrapItem : MonoBehaviour
{
    public GameObject root;
    public int index;

    public abstract void SetItem(WrapVo vo);
}



public class WrapContentImpl<TItem> where TItem : WrapItem, new()
{
    public static Action<WrapContentImpl<TItem>, int, int> Action_ShowItems;

    public UIWrapContentEx m_List;

    Dictionary<GameObject, TItem> m_Contents = new Dictionary<GameObject, TItem>();
    Dictionary<int, GameObject> m_ContentIndexs = new Dictionary<int, GameObject>();

    public void Init(int poolsCount, GameObject prefabs, UIWrapContentEx parent, Action<TItem> callInit = null, bool usePrefab = true)
    {
        for (int k = 0; k < poolsCount; ++k)
        {
            GameObject root = null;
            TItem item = null;
            if (k == 0 && usePrefab )
            {
                root = prefabs;
            }
            else
            {
                root = NGUITools.AddChild(parent.gameObject, prefabs);
            }

            item = root.GetComponent<TItem>();
            item.root = root;
            item.root.name = k.ToString();

            //Callback for init item UIWidgets
            if( callInit != null ) callInit(item);

            m_Contents.Add(item.root, item);
        }

        parent.SortBasedOnScrollMovement();
        parent.ResetContent(poolsCount);

        parent.onInitializeItem = OnItems;

        m_List = parent;
    }


    void OnItems(GameObject go, int realIndex, int rowIndex)
    {
        if (go == null)
            return;

        int iDeShowIndex = -1;
        if (m_Contents.ContainsKey(go))
        {
            iDeShowIndex = m_Contents[go].index;
        }

        m_ContentIndexs[realIndex] = go;
        m_Contents[go].index = realIndex;

        if (Action_ShowItems != null)
        {
            Action_ShowItems(this, realIndex, iDeShowIndex);
        }
    }


    public void ResetAwards(int iCount, bool rePos)
    {
        m_List.ResetContent(iCount, rePos);
    }


    public TItem GetItem(int index)
    {
        if (!m_ContentIndexs.ContainsKey(index))
            return null;

        if (!m_Contents.ContainsKey(m_ContentIndexs[index]))
            return null;

        TItem item = m_Contents[m_ContentIndexs[index]];

        return item;
    }

    public void SetItem(int index, WrapVo vo)
    {
        var item = GetItem(index);
        if( item == null )
            return;

        item.SetItem(vo);
    }
}
