#if UNITY_EDITOR
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GameSync.Editor
{
    /// <summary>在 Scene 视图叠加地图网格，仅用于坐标调试。</summary>
    public sealed class MapGridWindow : EditorWindow
    {
        /// <summary>提供地图尺寸的场景启动组件。</summary>
        [SerializeField] private GameMainBootstrap m_Target;
        /// <summary>网格显示开关。</summary>
        [SerializeField] private bool m_ShowGrid = true;
        /// <summary>放大后是否显示所有格心坐标。</summary>
        [SerializeField] private bool m_ShowCoordinates;
        /// <summary>网格线颜色。</summary>
        [SerializeField] private Color m_GridColor = new Color(1, 1, 1, 0.3f);
        /// <summary>坐标标签样式。</summary>
        private GUIStyle m_LabelStyle;

        [MenuItem("GameSync/调试/地图网格")]
        public static void Open() { GetWindow<MapGridWindow>("地图网格"); }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawGrid;
            SceneView.duringSceneGui += DrawGrid;
            if (m_Target == null) FindTarget();
            SceneView.RepaintAll();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawGrid;
            SceneView.RepaintAll();
        }

        private void FindTarget()
        {
            m_Target = null;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (GameMainBootstrap candidate in root.GetComponentsInChildren<GameMainBootstrap>(true))
                {
                    // 多个地图时要求显式选择，避免绘制错误目标。
                    if (m_Target != null) { m_Target = null; return; }
                    m_Target = candidate;
                }
            }
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            m_Target = (GameMainBootstrap)EditorGUILayout.ObjectField("地图启动组件", m_Target, typeof(GameMainBootstrap), true);
            m_ShowGrid = EditorGUILayout.Toggle("显示网格", m_ShowGrid);
            m_ShowCoordinates = EditorGUILayout.Toggle("显示全部格心坐标", m_ShowCoordinates);
            m_GridColor = EditorGUILayout.ColorField("格线颜色", m_GridColor);
            if (GUILayout.Button("读取当前场景地图")) { FindTarget(); SceneView.RepaintAll(); }
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            int width;
            int height;
            if (!TryGetSize(out width, out height))
            {
                EditorGUILayout.HelpBox("请选择场景中的 GameMainBootstrap。尺寸需为 1～256，MapRoot 需保持原点、无旋转和单位缩放。", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("地图尺寸", width + " × " + height);
            EditorGUILayout.HelpBox("一格 = 1 单位，Z=0。列 c 向右、行 r 向下；世界 Y 向上。悬停显示格心坐标，全部标签需放大后显示。关闭窗口即停止绘制，不修改场景。", MessageType.None);
        }

        private bool TryGetSize(out int width, out int height)
        {
            width = height = 0;
            if (m_Target == null || !m_Target.gameObject.scene.IsValid()) return false;
            Transform root = m_Target.transform;
            if (root.position != Vector3.zero || root.rotation != Quaternion.identity || root.lossyScale != Vector3.one) return false;
            using (var serialized = new SerializedObject(m_Target))
            {
                SerializedProperty widthProperty = serialized.FindProperty("m_MapWidth");
                SerializedProperty heightProperty = serialized.FindProperty("m_MapHeight");
                if (widthProperty == null || heightProperty == null) return false;
                width = widthProperty.intValue;
                height = heightProperty.intValue;
            }
            return width > 0 && height > 0 && width <= 256 && height <= 256;
        }

        private void DrawGrid(SceneView view)
        {
            int width;
            int height;
            if (!m_ShowGrid || !TryGetSize(out width, out height)) return;
            if (Event.current.type == EventType.MouseMove) view.Repaint();
            if (Event.current.type != EventType.Repaint) return;
            Color previousColor = Handles.color;
            CompareFunction previousTest = Handles.zTest;
            Matrix4x4 previousMatrix = Handles.matrix;
            try
            {
                Handles.matrix = Matrix4x4.identity;
                Handles.zTest = CompareFunction.Always;
                float left = -width * 0.5f;
                float top = height * 0.5f;
                Handles.color = m_GridColor;
                for (int c = 0; c <= width; c++)
                    Handles.DrawLine(new Vector3(left + c, -top, 0), new Vector3(left + c, top, 0));
                for (int r = 0; r <= height; r++)
                    Handles.DrawLine(new Vector3(left, top - r, 0), new Vector3(-left, top - r, 0));
                Handles.color = Color.red;
                Handles.DrawLine(new Vector3(left, 0, 0), new Vector3(-left, 0, 0));
                Handles.color = Color.green;
                Handles.DrawLine(new Vector3(0, -top, 0), new Vector3(0, top, 0));
                Handles.Label(Vector3.zero, "原点 (0, 0)", LabelStyle());
                float cellSize = Vector2.Distance(HandleUtility.WorldToGUIPoint(Vector3.zero), HandleUtility.WorldToGUIPoint(Vector3.right));
                if (m_ShowCoordinates && cellSize >= 75)
                {
                    for (int r = 0; r < height; r++)
                    {
                        for (int c = 0; c < width; c++)
                        {
                            var center = new Vector3(left + c + 0.5f, top - r - 0.5f, 0);
                            Vector3 viewport = view.camera.WorldToViewportPoint(center);
                            if (viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1)
                                Handles.Label(center, FormatPosition(center), LabelStyle());
                        }
                    }
                }
                if (mouseOverWindow != view) return;
                Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                float distance;
                if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out distance)) return;
                Vector3 point = ray.GetPoint(distance);
                int column = Mathf.FloorToInt(point.x - left);
                int row = Mathf.FloorToInt(top - point.y);
                if (column < 0 || column >= width || row < 0 || row >= height) return;
                var hovered = new Vector3(left + column + 0.5f, top - row - 0.5f, 0);
                Handles.color = Color.yellow;
                Handles.DrawWireCube(hovered, new Vector3(1, 1, 0));
                Handles.Label(hovered + Vector3.up * 0.3f,
                    "c=" + column + " r=" + row + "\n格心 " + FormatPosition(hovered), LabelStyle());
            }
            finally
            {
                Handles.color = previousColor;
                Handles.zTest = previousTest;
                Handles.matrix = previousMatrix;
            }
        }

        private GUIStyle LabelStyle()
        {
            if (m_LabelStyle == null)
            {
                m_LabelStyle = new GUIStyle(EditorStyles.helpBox);
                m_LabelStyle.normal.textColor = Color.white;
                m_LabelStyle.fontSize = 11;
            }
            return m_LabelStyle;
        }

        private static string FormatPosition(Vector3 position)
        {
            return "(" + position.x.ToString("0.#", CultureInfo.InvariantCulture) + ", " +
                position.y.ToString("0.#", CultureInfo.InvariantCulture) + ")";
        }
    }
}
#endif
