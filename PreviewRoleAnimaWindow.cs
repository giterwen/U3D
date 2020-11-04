#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using Sirenix.Utilities;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;

namespace YGame.Drama
{
    public class Element : SerializedScriptableObject
    {
        [HideInInspector]
        public string _name;
        [HideInInspector]
        public string path;
        [HideInInspector]
        public string meshPath;
        [HideInInspector]
        public bool select;
        [HideInInspector]
        public Texture icon;
        [ReadOnly]
        public GameObject meshAsset;
        [ReadOnly]
        public AnimationClip asset;
    }

    [CustomEditor(typeof(Element))]
    public class ElementInspector : OdinEditor
    {
        private GameObject fbx;
        private AnimationClip animationClip;
        private GameObject m_root;
        private GameObject m_PreviewInstance;
        private Vector3 centerPosition;
        private int cullingLayer = 0;
        private float time = 0;
        private bool m_Playing = true;
        private float m_lastUpdateTime = 0;
        private float m_AvatarScale = 1f;
        private float m_ZoomFactor = 1f;
        private Vector3 m_PivotPositionOffset = new Vector3(0, 1, 0);
        private float m_BoundingVolumeScale;
        private ViewTool m_ViewTool;
        private Vector2 m_PreviewDir = new Vector2(120f, -20f);

        public override void OnInspectorGUI()
        {
            if (target == null)
            {
                return;
            }
            var _target = target as Element;
            ////////////////////////////
            base.OnInspectorGUI();

            //EditorGUILayout.BeginVertical();
            fbx = _target.meshAsset;
            animationClip = _target.asset;
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("播放"))
            {
                m_Playing = true;
                time = 0;
            }
            if (GUILayout.Button("暂停"))
            {
                m_Playing = false;
                time = 0;
                AnimationMode.StartAnimationMode();
                AnimationMode.SampleAnimationClip(m_PreviewInstance, animationClip, time);
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
            var lastRect = GUILayoutUtility.GetLastRect();
            var pos = GUIHelper.CurrentWindow.position;
            // Debug.Log(GUIHelper.CurrentWindow.position);
            var rect = new Rect(lastRect.xMin + 50, lastRect.yMax + 50, pos.width * 0.9f, pos.height * 0.9f);
            Handles.DrawCamera(rect, camera);
            GetPreviewObject();
            var m_PreviewHint = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().GetHashCode();
            var m_PreviewSceneHint = m_PreviewHint;
            var controlID = GUIUtility.GetControlID(m_PreviewHint, FocusType.Passive, rect);
            var typeForControl = Event.current.GetTypeForControl(controlID);
            DoRenderPreview();
            int controlID2 = GUIUtility.GetControlID(m_PreviewSceneHint, FocusType.Passive);
            typeForControl = Event.current.GetTypeForControl(controlID2);
            HandleViewTool(Event.current, typeForControl, controlID2, rect);
            DoAvatarPreviewFrame(Event.current, typeForControl, rect);
        }

        /// <summary>
        /// 视图工具
        /// </summary>
        protected enum ViewTool
        {
            None,
            /// <summary>
            /// 平移
            /// </summary>
            Pan,
            /// <summary>
            /// 缩放
            /// </summary>
            Zoom,
            /// <summary>
            /// 旋转
            /// </summary>
            Orbit
        }
        private Camera m_camera;
        private Camera camera
        {
            get
            {
                if (m_camera == null)
                {
                    if (m_root == null)
                    {
                        m_root = new GameObject("m_previewRoot");
                    }
                    m_camera = new GameObject("EditorCamera").AddComponent<Camera>();
                    var light = m_camera.gameObject.AddComponent<Light>();
                    light.Reset();
                    light.type = LightType.Directional;
                    cullingLayer = LayerMask.NameToLayer("PreviewCullingLayer");
                    m_camera.transform.SetParent(m_root.transform);
                    m_camera.cullingMask = 1 << cullingLayer;
                    m_camera.fieldOfView = 30f;
                    m_camera.clearFlags = CameraClearFlags.Color;
                    m_camera.backgroundColor = Color.grey;
                    m_camera.depth = -10;
                    DestroyImmediate(m_camera.GetComponent<AudioListener>());
                }
                return m_camera;
            }
        }
        Camera GetPreviewCamera()
        {
            return camera;
        }
        protected ViewTool viewTool
        {
            get
            {
                Event current = Event.current;
                if (m_ViewTool == ViewTool.None)
                {
                    bool flag = current.control && Application.platform == RuntimePlatform.OSXEditor;
                    bool actionKey = EditorGUI.actionKey;
                    bool flag2 = !actionKey && !flag && !current.alt;
                    if ((current.button <= 0 && flag2) || (current.button <= 0 && actionKey) || current.button == 2)
                    {
                        m_ViewTool = ViewTool.Pan;
                    }
                    else
                    {
                        if ((current.button <= 0 && flag) || (current.button == 1 && current.alt))
                        {
                            m_ViewTool = ViewTool.Zoom;
                        }
                        else
                        {
                            if ((current.button <= 0 && current.alt) || current.button == 1)
                            {
                                m_ViewTool = ViewTool.Orbit;
                            }
                        }
                    }
                }
                return m_ViewTool;
            }
        }

        protected MouseCursor currentCursor
        {
            get
            {
                switch (m_ViewTool)
                {
                    case ViewTool.Pan:
                        return MouseCursor.Pan;
                    case ViewTool.Zoom:
                        return MouseCursor.Zoom;
                    case ViewTool.Orbit:
                        return MouseCursor.Orbit;
                    default:
                        return MouseCursor.Arrow;
                }
            }
        }

        public Vector3 bodyPosition
        {
            get
            {
                if (m_PreviewInstance == null)
                {
                    return Vector3.zero;
                }
                return m_PreviewInstance.transform.position;
            }
        }

        protected override void OnEnable()
        {
            EditorApplication.update -= InspectorUpdate;
            EditorApplication.update += InspectorUpdate;
            Clear();
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            Clear();
            EditorApplication.update -= InspectorUpdate;
            base.OnDisable();
        }

        void InspectorUpdate()
        {
            float curTime = Time.realtimeSinceStartup;
            m_lastUpdateTime = Mathf.Min(m_lastUpdateTime, curTime);//very important!!!
            float dt = curTime - m_lastUpdateTime;
            PlayAnima(dt);
            if (m_Playing)
            {
                Repaint();
            }
            m_lastUpdateTime = curTime;
        }

        void Clear()
        {
            if (m_root == null)
            {
                m_root = GameObject.Find("m_previewRoot");
            }
            if (m_root)
            {
                GameObject.DestroyImmediate(m_root);// DestroyImmediate(m_root);
            }
            animationClip = null;
            m_root = null;
            m_camera = null;
            m_PreviewInstance = null;
        }

        private void DoRenderPreview()
        {
            Vector3 bodyPosition = this.bodyPosition;
            Quaternion quaternion = Quaternion.identity;
            Vector3 vector = Vector3.zero;
            Quaternion quaternion2 = Quaternion.identity;
            Vector3 pivotPos = Vector3.zero;
            GetPreviewCamera().nearClipPlane = 0.5f * m_ZoomFactor;
            GetPreviewCamera().farClipPlane = 100f * m_AvatarScale;
            Quaternion rotation = Quaternion.Euler(-m_PreviewDir.y, -m_PreviewDir.x, 0f);
            Vector3 position2 = rotation * (Vector3.forward * -5.5f * m_ZoomFactor) + bodyPosition + m_PivotPositionOffset;
            GetPreviewCamera().transform.position = position2;
            GetPreviewCamera().transform.rotation = rotation;
        }

        void GetPreviewObject()
        {
            if (m_PreviewInstance)
            {
                if (m_PreviewInstance.name != fbx.name)
                {
                    DestroyImmediate(m_PreviewInstance);
                    m_PreviewInstance = null;
                }
            }
            if (m_PreviewInstance == null)
            {
                if (fbx == null)
                {
                    return;
                }
                m_PreviewInstance = Instantiate(fbx);
                if (m_root == null)
                {
                    m_root = new GameObject("m_previewRoot");
                }
                m_PreviewInstance.transform.SetParent(m_root.transform);
                m_PreviewInstance.name = fbx.name;
                m_PreviewInstance.transform.position = Vector3.one * 2000;
                m_PreviewInstance.layer = cullingLayer;
                foreach (var i in m_PreviewInstance.transform.GetComponentsInChildren<Transform>())
                {
                    i.gameObject.layer = cullingLayer;
                }
                var anima = m_PreviewInstance.AddComponent<Animation>();
                Bounds bounds = new Bounds(m_PreviewInstance.transform.position, Vector3.zero);
                foreach (var _renderer in m_PreviewInstance.GetComponentsInChildren<Renderer>())
                {
                    bounds.Encapsulate(_renderer.bounds);
                }
                centerPosition = bounds.center;
                camera.transform.position = centerPosition + Vector3.forward * -5;
            }
        }
        void PlayAnima(float dt)
        {
            if (m_PreviewInstance == null || animationClip == null)
            {
                return;
            }
            if (m_Playing)
            {
                if (time <= 1)
                {
                    time += dt;
                    AnimationMode.StartAnimationMode();
                    AnimationMode.SampleAnimationClip(m_PreviewInstance, animationClip, time);
                    if (time >= 1)
                    {
                        time = 0;
                    }
                }
            }
            else
            {
                time = 0;
            }

        }
        protected void HandleMouseDown(Event evt, int id, Rect previewRect)
        {
            if (viewTool != ViewTool.None && previewRect.Contains(evt.mousePosition))
            {
                EditorGUIUtility.SetWantsMouseJumping(1);
                evt.Use();
                GUIUtility.hotControl = id;
            }
        }

        protected void HandleMouseUp(Event evt, int id)
        {
            if (GUIUtility.hotControl == id)
            {
                m_ViewTool = ViewTool.None;
                GUIUtility.hotControl = 0;
                EditorGUIUtility.SetWantsMouseJumping(0);
                evt.Use();
            }
        }

        protected void HandleMouseDrag(Event evt, int id, Rect previewRect)
        {
            if (m_PreviewInstance == null)
            {
                return;
            }
            if (GUIUtility.hotControl == id)
            {
                switch (m_ViewTool)
                {
                    case ViewTool.Pan:
                        DoAvatarPreviewPan(evt);
                        break;
                    case ViewTool.Zoom:
                        DoAvatarPreviewZoom(evt, -HandleUtility.niceMouseDeltaZoom * ((!evt.shift) ? 0.5f : 2f));
                        break;
                    case ViewTool.Orbit:
                        DoAvatarPreviewOrbit(evt, previewRect);
                        break;
                    default:
                        Debug.Log("Enum value not handled");
                        break;
                }
            }
        }

        protected void HandleViewTool(Event evt, EventType eventType, int id, Rect previewRect)
        {
            switch (eventType)
            {
                case EventType.MouseDown:
                    HandleMouseDown(evt, id, previewRect);
                    break;
                case EventType.MouseUp:
                    HandleMouseUp(evt, id);
                    break;
                case EventType.MouseDrag:
                    HandleMouseDrag(evt, id, previewRect);
                    break;
                case EventType.ScrollWheel:
                    DoAvatarPreviewZoom(evt, HandleUtility.niceMouseDeltaZoom * ((!evt.shift) ? 0.5f : 2f));
                    break;
            }
        }

        public void DoAvatarPreviewOrbit(Event evt, Rect previewRect)
        {
            m_PreviewDir -= evt.delta * (float)((!evt.shift) ? 1 : 3) / Mathf.Min(previewRect.width, previewRect.height) * 140f;
            m_PreviewDir.y = Mathf.Clamp(m_PreviewDir.y, -90f, 90f);
            evt.Use();
        }

        public void DoAvatarPreviewPan(Event evt)
        {
            Vector3 vector = camera.WorldToScreenPoint(bodyPosition + m_PivotPositionOffset);
            Vector3 a = new Vector3(-evt.delta.x, evt.delta.y, 0f);
            vector += a * Mathf.Lerp(0.25f, 2f, m_ZoomFactor * 0.5f);
            Vector3 b = camera.ScreenToWorldPoint(vector) - (bodyPosition + m_PivotPositionOffset);
            m_PivotPositionOffset += b;
            evt.Use();
        }

        /// <summary>
        /// 定位事件
        /// 按F近距离查看对象
        /// 按G视图平移到鼠标位置
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="type"></param>
        /// <param name="previewRect"></param>
        public void DoAvatarPreviewFrame(Event evt, EventType type, Rect previewRect)
        {
            if (type == EventType.KeyDown && evt.keyCode == KeyCode.F)
            {
                m_PivotPositionOffset = new Vector3(0, 1, 0);
                m_ZoomFactor = m_AvatarScale;
                evt.Use();
            }
            if (type == EventType.KeyDown && Event.current.keyCode == KeyCode.G)
            {
                m_PivotPositionOffset = GetCurrentMouseWorldPosition(evt, previewRect) - bodyPosition;
                evt.Use();
            }
            if (type == EventType.KeyDown && evt.keyCode == KeyCode.Space)
            {
                if (m_Playing)
                {
                    m_Playing = false;
                }
                else
                {
                    m_Playing = true;
                }
                evt.Use();
            }
        }

        protected Vector3 GetCurrentMouseWorldPosition(Event evt, Rect previewRect)
        {
            float scaleFactor = 1;
            return camera.ScreenToWorldPoint(new Vector3((evt.mousePosition.x - previewRect.x) * scaleFactor, (previewRect.height - (evt.mousePosition.y - previewRect.y)) * scaleFactor, 0f)
            {
                z = Vector3.Distance(bodyPosition, camera.transform.position)
            });
        }

        public void DoAvatarPreviewZoom(Event evt, float delta)
        {
            float num = -delta * 0.05f;
            m_ZoomFactor += m_ZoomFactor * num;
            m_ZoomFactor = Mathf.Max(m_ZoomFactor, m_AvatarScale / 10f);
            evt.Use();
        }

    }

    public class PreviewRoleAnimaWindow : OdinMenuEditorWindow
    {
        private Dictionary<string, List<Element>> dic = new Dictionary<string, List<Element>>();
        private string path = "Assets/Develop/Art/Story/Common/Character/Cromwell/Set0";
        private string meshPath = "Assets/Develop/Art/Roles/Character/Cromwell/Set0/Rig/R_Cromwell0_HQ.FBX";

        [MenuItem("YGame/Tools/RoleAnimaPreview[角色动画预览]")]
        public static void OpenWindow()
        {
            var winodw = GetWindow<PreviewRoleAnimaWindow>()
                 .position = GUIHelper.GetEditorWindowRect().AlignCenter(800, 600).SetSize(800, 600);
            // winodw.MaxHeight(800);
            // winodw.MaxWidth(800);
            // winodw.MinHeight(800);
            // winodw.MinWidth(800);
        }
        protected override void OnDestroy()
        {
            if (dic != null)
            {
                foreach (var i in dic.Values)
                {
                    foreach (var x in i)
                    {
                        ScriptableObject.DestroyImmediate(x);
                    }
                }
                dic.Clear();
            }
            var m_root = GameObject.Find("m_previewRoot");
            if (m_root)
            {
                DestroyImmediate(m_root);
            }
        }
        protected override OdinMenuTree BuildMenuTree()
        {
            AddTreeElement();
            var tree = new OdinMenuTree();
            tree.DefaultMenuStyle.IconSize = 15;
            tree.Config.DrawSearchToolbar = true;
            tree.Config.SearchToolbarHeight = 30;
            tree.DefaultMenuStyle.Height = 25;
            foreach (var i in dic)
            {
                if (i.Value == null || i.Value.Count == 0)
                {
                    tree.Add(i.Key, null);
                }
                else
                {
                    foreach (var x in i.Value)
                    {
                        var key = i.Key + "/" + x._name;
                        tree.Add(key, x, x.icon);
                    }
                }
            }
            tree.EnumerateTree().Where(x => x.Value as Element).ForEach(AddDragHandles);
            tree.SortMenuItemsByName();
            return tree;
        }
        private void AddDragHandles(OdinMenuItem menuItem)
        {
            menuItem.OnDrawItem += x => Drag(menuItem);
        }
        private void Drag(OdinMenuItem menuItem)
        {
            if (menuItem.IsSelected)
            {
                if (Event.current.type == EventType.MouseDown)
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.StartDrag("elemnet");
                    var g = (menuItem.Value as Element).asset;
                    DragAndDrop.objectReferences = new Object[1] { g };
                }
                switch (Event.current.type)
                {
                    case EventType.DragUpdated:
                    case EventType.DragPerform:
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        if (Event.current.type == EventType.DragPerform)
                        {
                            DragAndDrop.AcceptDrag();
                        }
                        Event.current.Use();
                        break;
                    default:
                        break;
                }
            }
        }

        private void AddTreeElement()
        {
            var roles = YGame.Drama.DramaImporter.GetDramaRoleList();
            if (roles != null)
            {
                dic = new Dictionary<string, List<Element>>();
                foreach (var i in roles)
                {
                    if (string.IsNullOrEmpty(i.NameEditor) || string.IsNullOrEmpty(i.RoleAnimatorRoot) || string.IsNullOrEmpty(i.RoleBodyMeshPath))
                    {
                        continue;
                    }
                    var _key = i.NameEditor + "[" + i.NameEditorCN + "]";
                    var _path = i.RoleAnimatorRoot;
                    var _meshPath = i.RoleBodyMeshPath;
                    AddTreeElements(_key, _path, _meshPath);
                }
            }
        }

        private void AddTreeElements(string _key = "ROLE", string _path = "", string _meshPath = "")
        {
            if (dic.ContainsKey(_key))
            {
                return;
            }
            var elements = new List<Element>();
            var _meshAsset = AssetDatabase.LoadAssetAtPath<GameObject>(_meshPath);
            if (Directory.Exists(_path) == false)
            {
                return;
            }
            foreach (var i in Directory.GetFiles(_path))
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(i);
                UnityEngine.Object asset = null;
                foreach (var x in assets)
                {
                    if (x is AnimationClip)
                    {
                        var id = x.GetInstanceID();
                        asset = EditorUtility.InstanceIDToObject(id);
                        break;
                    }
                }
                if (asset == null)
                {
                    continue;
                }
                var element = ScriptableObject.CreateInstance<Element>();
                element._name = asset.name;
                element.path = i;
                var assetPath = AssetDatabase.GetAssetPath(asset);
                element.icon = AssetDatabase.GetCachedIcon(assetPath);
                element.asset = asset as AnimationClip;
                element.meshAsset = _meshAsset;
                elements.Add(element);
            }
            dic.Add(_key, elements);
        }
    }
}
























#endif