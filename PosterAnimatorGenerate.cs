using System;
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
    public int AnimationID;
    public int Set;
    public string AnimationClip;
    public string Parameter;
    public bool NeedEndEvent;
    public bool IsDefaultIdle;

    public int[] ToStateArray;
}

public class PosterAnimatorGenerate : EditorWindow
{
    private const string AnimatorSavePath = "Assets/Develop/Art/Roles/Character/";
    private const string AnimatorConfigPath = "Assets/Develop/Lua/Config/Table_SecretaryAnimation.lua";
    private const string AnimatorClipPrefix = "Assets/Develop/Art/Roles/Character/";
    private const string AnimatorClipInfix = "/Dynamic/Poster/Animation/";
    private const string AnimatorControllerSuffix = "_PosterController.controller";
    private const string PosterPrefabPath = "Assets/Resources/Prefabs/Roles/Character/";

    private static string characterName = "";
    private static int cur_set = 0; //当前套装
    private static string prefab_path = "";

    private static float frameRate = 30f;//动画帧率
    private static string[] expression_list = {"Common_Angry", "Common_Smiling", "Common_Suprised", "Common_Sad", "Common_Happy"};
    private static string[] mouse_list = {"a", "e", "ih", "o", "ou"};
    private static AnimationCurve target_curve = AnimationCurve.Linear(0, 0, 15 / frameRate, 100);
    private static AnimationCurve other_curve = AnimationCurve.Linear(0, 0, 15 / frameRate, 0);

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
        prefab_path = PosterPrefabPath + characterName + "/P_"+ characterName + cur_set + "_Poster_HQ.prefab";
    }

    //单个导出
    public static void SingleAnimatorGenerate()
    {
        CreateController();
        AddParameters();
        FaceAnimationGenerate();
        CreateAnimStates();
        BindControllerToPrefab();
    }


    //加载解析看板娘所需配置
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

    //创建AnimatorController
    static void CreateController()
    {
        string path = AnimatorSavePath + characterName + "/Set" + cur_set + "/Dynamic/Poster/Animation/" + characterName + AnimatorControllerSuffix;
        productController = AnimatorController.CreateAnimatorControllerAtPath(path);
        productController.layers = null;
        productController.AddLayer("Default");
        productController.AddLayer("Face");//表情层
        productController.parameters = null;
        var layers = productController.layers;

        layers[0].defaultWeight = 1;
        layers[0].blendingMode = AnimatorLayerBlendingMode.Override;
        layers[1].defaultWeight = 1;
        layers[1].blendingMode = AnimatorLayerBlendingMode.Additive;
        productController.layers = layers;

        // var layer = new UnityEditor.Animations.AnimatorControllerLayer
        // {
        //     name = "Face",
        //     defaultWeight = 1f,
        //     blendingMode = AnimatorLayerBlendingMode.Additive,
        //     stateMachine = new UnityEditor.Animations.AnimatorStateMachine() // Make sure to create a StateMachine as well, as a default one is not created
        // };
        // productController.AddLayer("Face");
        // productController.AddLayer(layer);

    }

    //表情动画生成
    static void FaceAnimationGenerate()
    {
        AnimatorStateMachine stateMachine = productController.layers[1].stateMachine;
        AnimatorState idle_state = stateMachine.AddState("Idle", new Vector2(0, 300));

        Vector2 position = new Vector2(400, 0);
        SkinnedMeshRenderer face_renderer = null;
        GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(prefab_path);
        SkinnedMeshRenderer[] renderers = obj.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var item in renderers)
        {
            if (item.name.Contains("face") || item.name.Contains("Face"))
            {
                face_renderer = item;
                break;
            }
        }
        Mesh sharedMesh = face_renderer.sharedMesh;

        string idle_path = AnimatorClipPrefix + characterName + "/Set" + cur_set 
            + AnimatorClipInfix + characterName + cur_set + "_Poster_Common_Idle.anim";
        AnimationClip idle_clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(idle_path);
        bool is_idle_clip_exist = idle_clip != null;
        if (!is_idle_clip_exist)
        {
            idle_clip = new AnimationClip();
        }
        for (int j = 0; j < sharedMesh.blendShapeCount; j++)
        {
            string name = sharedMesh.GetBlendShapeName(j);
            string propertyName = "blendShape." + name;
            idle_clip.SetCurve(face_renderer.name, typeof(SkinnedMeshRenderer), propertyName, other_curve);
        }
        if (is_idle_clip_exist)
        {
            EditorUtility.SetDirty(idle_clip);
        }
        else
        {
            AssetDatabase.CreateAsset(idle_clip, idle_path);
        }
        idle_state.motion = idle_clip;

        for (int i = 0; i < expression_list.Length; i++)
        {
            string clip_Path = AnimatorClipPrefix + characterName + "/Set" + cur_set 
            + AnimatorClipInfix + characterName + cur_set + "_Poster_"+ expression_list[i] + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clip_Path);
            bool is_clip_exist = clip != null;
            if (!is_clip_exist)
            {
                clip = new AnimationClip();
            }
            string expression = "R_" + characterName + cur_set + "_" + expression_list[i];
            for (int j = 0; j < sharedMesh.blendShapeCount; j++)
            {
                string name = sharedMesh.GetBlendShapeName(j);
                string propertyName = "blendShape." + name;
                if (name.Equals(expression))
                {
                    clip.SetCurve(face_renderer.name, typeof(SkinnedMeshRenderer), propertyName, target_curve);
                }
                else if(Array.IndexOf(mouse_list, name) == -1)
                {
                    clip.SetCurve(face_renderer.name, typeof(SkinnedMeshRenderer), propertyName, other_curve);
                }  
            }
            if (is_clip_exist)
            {
                EditorUtility.SetDirty(clip);
            }
            else
            {
                AssetDatabase.CreateAsset(clip, clip_Path);
            }
            
            position.y = 130*i;
            AnimatorState state_1 = stateMachine.AddState(clip.name, position);
            state_1.motion = clip;
            AnimatorStateTransition transition_1 = idle_state.AddTransition(state_1);
            transition_1.AddCondition(AnimatorConditionMode.If, 0, expression_list[i]);

            AnimatorStateTransition transition_2 = state_1.AddTransition(idle_state);
            transition_2.AddCondition(AnimatorConditionMode.If, 0, expression_list[i] + "_reverse");

            position.y = 130*i + 60;
            AnimatorState state_2 = stateMachine.AddState(clip.name + "_reverse", position);
            state_2.motion = clip;
            state_2.speed = -1;
        }
        EditorUtility.SetDirty(productController);
        // AssetDatabase.Refresh();
    }

    //AnimatorController添加Parameters
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
        for (int i = 0; i < expression_list.Length; i++)
        {
            productController.AddParameter(expression_list[i], AnimatorControllerParameterType.Trigger);
            productController.AddParameter(expression_list[i] + "_reverse", AnimatorControllerParameterType.Trigger);
        }
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
            else if (posterAnimationDatas[i].AnimationClip.Contains("MainInShow"))
            {
                position = new Vector2(400, 200);
            }
            else
            {
                position = new Vector2(0, 80 * i);
            }
            AnimatorState state = stateMachine.AddState(posterAnimationDatas[i].AnimationClip, position);
            state.motion = clip;
            if (posterAnimationDatas[i].NeedEndEvent)
            {
                state.AddStateMachineBehaviour<PosterStateExitBehaviour>();
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
                int to_index = GetToStateRealIndex(posterAnimationDatas[i].ToStateArray[j]);
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

        EditorUtility.SetDirty(productController);
        AssetDatabase.SaveAssets();
    }

    static int GetToStateRealIndex(int index)
    {
        for (int i = 0; i < posterAnimationDatas.Count; i++)
        {
            if (posterAnimationDatas[i].AnimationID == index)
            {
                return i;
            }
        }
        return 0;
    }

    static void BindControllerToPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath(prefab_path, typeof(GameObject)) as GameObject;
        GameObject obj = Instantiate(prefab);
        GameObject body = obj.transform.GetChild(0).GetChild(0).gameObject;
        Animator animator = body.GetComponent<Animator>();
        if (animator == null)
        {
            animator = body.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = productController;
        PrefabUtility.SaveAsPrefabAsset(obj, prefab_path);
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
