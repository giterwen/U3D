using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using LitJson;

public class PosterAnimationData
{
    public int ID;
    public int AvatarID;
    public string Name;
    public int Set;
    public string AnimationClip;
    public string Parameter;
    public bool NeedEndEvent;
    public bool IsDefaultIdle;

    public int[] ToStateArray;
}

public class PosterAnimatorGenerate : EditorWindow
{
    private const string AnimatorSavePath = "Assets/Develop/Art/Animators/Poster/";
    private const string AnimatorConfigPath = "Assets/Develop/Lua/Config/Table_SecretaryAnimation.lua";
    private const string AnimatorClipPrefix = "Assets/Develop/Art/Roles/Character/";
    private const string AnimatorClipInfix = "/Dynamic/Poster/Animation/";
    private const string AnimatorControllerSuffix = "_PosterController.controller";
    private const string PosterPrefabPath = "Assets/Resources/Prefabs/Roles/Character/";

    private static string characterName = "";
    private static int cur_set = 0;

    private static Dictionary<int, PosterAnimationData> animationDic = new Dictionary<int, PosterAnimationData>();
    private static Dictionary<int, List<PosterAnimationData>> posterDic = new Dictionary<int, List<PosterAnimationData>>();
    private static List<PosterAnimationData> posterAnimationDatas;

    private static AnimatorController productController;
    [MenuItem("YGame/看板娘/状态机生成界面")]
    public static void AnimatorGenerate()
    {
        LoadConfig();
        PosterAnimatorGenerate window = GetWindow<PosterAnimatorGenerate>(true, "PosterAnimatorGenerate");
        window.minSize = new Vector2(600, 500);
    }

    void OnGUI()
    {
        GUIContent gUIContent = new GUIContent();
        GUILayout.BeginArea(new Rect(2f, 2f, 1000f, 600f));
        foreach (var item in posterDic)
        {
            GUILayout.BeginHorizontal();
            gUIContent.text = item.Value[0].Name + item.Value[0].Set;
            GUILayout.Label(gUIContent, GUILayout.ExpandWidth(false));
            if (GUILayout.Button("ExportAnimator", GUILayout.ExpandWidth(false)))
            {
                SetCurConfig(item.Value);
                SingleAnimatorGenerate();
                EditorUtility.DisplayDialog("提示", "导出成功", "确定");
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }
        if (GUILayout.Button("ExportAllAnimator", GUILayout.ExpandWidth(false)))
        {
            int count = 0;
            foreach (var item in posterDic)
            {
                count++;
                SetCurConfig(item.Value);
                EditorUtility.DisplayProgressBar("导出进度", characterName + cur_set, count/posterDic.Count);
                SingleAnimatorGenerate();
                EditorUtility.ClearProgressBar();
            }
            EditorUtility.DisplayDialog("提示", "导出成功", "确定");
        }
        GUILayout.EndArea();
    }

    public static void SetCurConfig(List<PosterAnimationData> _posterAnimationDatas)
    {
        posterAnimationDatas = _posterAnimationDatas;
        characterName = posterAnimationDatas[0].Name;
        cur_set = posterAnimationDatas[0].Set;
    }

    //单个导出
    public static void SingleAnimatorGenerate()
    {
        CreateController();
        AddParameters();
        CreateAnimStates();
        BindControllerToPrefab();
    }

    static void LoadConfig()
    {
        animationDic.Clear();
        posterDic.Clear();
        if (!File.Exists(AnimatorConfigPath))
        {
            Debug.LogError("找不到数据:" + AnimatorConfigPath);
            return;
        }
        var data = GetTextFromFile(AnimatorConfigPath);
        var json = Json2TableTools.GetJsonFromLua(data);
        var jsonData = JsonMapper.ToObject<Dictionary<string, PosterAnimationData>>(json);
        foreach (var item in jsonData)
        {
            animationDic.Add(int.Parse(item.Key), item.Value);
            int avatar_id = item.Value.AvatarID;
            List<PosterAnimationData> poster_list;
            if (!posterDic.TryGetValue(avatar_id, out poster_list))
            {
                poster_list = new List<PosterAnimationData>();
                posterDic.Add(avatar_id, poster_list);
            }
            poster_list.Add(item.Value);
        }
    }

    static void CreateController()
    {
        productController = AnimatorController.CreateAnimatorControllerAtPath(AnimatorSavePath + characterName + AnimatorControllerSuffix);
    }

    static void AddParameters()
    {
        HashSet<string> hashSet = new HashSet<string>();
        for (int i = 0; i < posterAnimationDatas.Count; i++)
        {
            string trigger = posterAnimationDatas[i].Parameter;
            if (!string.IsNullOrEmpty(trigger))
            {
                if (!hashSet.Contains(trigger))
                {
                    hashSet.Add(trigger);
                    productController.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                }
            }
        }
        hashSet.Clear();
    }

    static void CreateAnimStates()
    {
        AnimatorStateMachine stateMachine = null;
        List<AnimatorState> state_list = new List<AnimatorState>();
        for (int i = 0; i < posterAnimationDatas.Count; i++)
        {
            string clip_Path = AnimatorClipPrefix + characterName + "/Set" + posterAnimationDatas[i].Set 
            + AnimatorClipInfix + posterAnimationDatas[i].AnimationClip + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath(clip_Path, typeof(AnimationClip)) as AnimationClip;
            stateMachine = productController.layers[0].stateMachine;
            Vector2 position = Vector2.zero;
            if (posterAnimationDatas[i].AnimationClip.Contains("Idle"))
            {
                position = new Vector2(400, 600);
            }
            else if (posterAnimationDatas[i].AnimationClip.Contains("Touch"))
            {
                position = new Vector2(800, 80 * i);
            }
            else if (posterAnimationDatas[i].AnimationClip.Contains("Show"))
            {
                position = new Vector2(400, 200);
            }
            AnimatorState state = stateMachine.AddState(posterAnimationDatas[i].AnimationClip, position);
            state.motion = clip;
            if (posterAnimationDatas[i].NeedEndEvent)
            {
                state.AddStateMachineBehaviour<PosterBehaviour>();
            }
            if (posterAnimationDatas[i].IsDefaultIdle)
            {
                stateMachine.defaultState = state;
            }
            state_list.Add(state);
        }
        for (int i = 0; i < state_list.Count; i++)
        {
            for (int j = 0; j < posterAnimationDatas[i].ToStateArray.Length; j++)
            {
                int to_index = GetAnimatorState(posterAnimationDatas[i].ToStateArray[j]);
                AnimatorStateTransition transition = state_list[i].AddTransition(state_list[to_index]);
                if (!string.IsNullOrEmpty(posterAnimationDatas[to_index].Parameter))
                {
                    transition.hasExitTime = false;
                    transition.AddCondition(AnimatorConditionMode.If, 0, posterAnimationDatas[to_index].Parameter);
                }
                else
                {
                    transition.hasExitTime = true;
                    transition.exitTime = 0.75f;
                }
            }
        }
    }

    static int GetAnimatorState(int index)
    {
        for (int i = 0; i < posterAnimationDatas.Count; i++)
        {
            if (posterAnimationDatas[i].ID == index)
            {
                return i;
            }
        }
        return 0;
    }

    static void BindControllerToPrefab()
    {
        string path = PosterPrefabPath + characterName + "/P_"+ characterName + cur_set + "_Poster_HQ.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) as GameObject;
        GameObject obj = Instantiate(prefab);
        GameObject body = obj.transform.GetChild(0).GetChild(0).gameObject;
        Animator animator = body.GetComponent<Animator>();
        if (animator == null)
        {
            animator = body.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = productController;
        PrefabUtility.SaveAsPrefabAsset(obj, path);
        DestroyImmediate(obj);
    }

    static string GetTextFromFile(string filePath)
    {
        var sr = new StreamReader(filePath);
        var data = sr.ReadToEnd();
        sr.Close();
        sr.Dispose();
        return data;
    }
}
