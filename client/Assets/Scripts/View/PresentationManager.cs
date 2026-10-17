using System;
using System.Threading.Tasks;
using GameSync.Input;
using GameSync.View.Camera;
using GameSync.View.Map;
using GameSync.View.UI;
using GameSync.View.Unit;
using UnityEngine;

namespace GameSync.View
{
    /// <summary>管理本次场景的相机和单位外观；不保存或推进模拟状态。</summary>
    public sealed class PresentationManager : Singleton<PresentationManager>
    {
        /// <summary>静态展示中首个单位的世界 XY 坐标，也是输入预览的瞄准起点。</summary>
        public static readonly Vector2 PreviewOrigin = new Vector2(-4.5f, 6.5f);

        /// <summary>当前接管的游戏相机。</summary>
        private UnityEngine.Camera m_Camera;
        /// <summary>恢复场景相机原状态所需的视图对象。</summary>
        private CameraView m_CameraView;
        /// <summary>当前场景单位外观的 ID 映射和生命周期。</summary>
        private UnitViewManager m_UnitViews;
        /// <summary>预览状态使用的 HUD，由启动层持有。</summary>
        private GameHudView m_Hud;
        /// <summary>预览中累计的攻击按下次数。</summary>
        private long m_AttackCount;
        /// <summary>下一次 HUD 文本刷新时间，使用不受 timeScale 影响的秒数。</summary>
        private float m_NextDisplayTime;

        private PresentationManager() { }

        /// <summary>当前场景的单位外观集合；未启动或停止后为空。</summary>
        public UnitViewManager UnitViews { get { return m_UnitViews; } }

        /// <summary>接管本次场景的相机及单位容器。</summary>
        /// <param name="map">包含显示边界与单位父节点的地图容器，不可为空。</param>
        /// <param name="camera">场景游戏相机，不可为空；停止时恢复其原状态。</param>
        /// <param name="hud">由启动层持有的 HUD，用于创建单位血条，不可为空。</param>
        public void Start(MapView map, UnityEngine.Camera camera, GameHudView hud)
        {
            if (m_UnitViews != null || m_CameraView != null)
                throw new InvalidOperationException("表现管理器已经启动。");
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (hud == null) throw new ArgumentNullException(nameof(hud));
            var cameraView = new CameraView(camera, map.Bounds);
            try
            {
                m_UnitViews = new UnitViewManager(map.UnitsRoot, new UnitViewFactory(hud.HealthBarRoot));
                m_Camera = camera;
                m_CameraView = cameraView;
                m_Hud = hud;
                m_AttackCount = 0;
                m_NextDisplayTime = 0;
            }
            catch
            {
                cameraView.Dispose();
                throw;
            }
        }

        /// <summary>按窗口比例刷新游戏相机，使鼠标投影使用本帧显示区域。</summary>
        public void RefreshCamera()
        {
            m_CameraView?.Refresh();
        }

        /// <summary>按显示频率更新输入与可选网络诊断的 HUD 文字。</summary>
        /// <param name="input">本帧已经消费的预览输入，不作为网络 Tick 数据。</param>
        /// <param name="networkStatus">可选 N1 会话状态；未开启诊断时传 null。</param>
        public void UpdateInputPreview(PlayerInputState input, string networkStatus)
        {
            if (m_Hud == null) return;
            if (input.AttackPressed) m_AttackCount++;
            if (Time.unscaledTime < m_NextDisplayTime) return;
            m_NextDisplayTime = Time.unscaledTime + 0.1f;
            m_Hud.SetStatus(string.Format("输入预览（未接模拟） 移动 {0:F2},{1:F2}  瞄准 {2:F2},{3:F2}  攻击 {4}  按下 {5}{6}",
                input.Move.x, input.Move.y, input.Aim.x, input.Aim.y,
                input.AttackHeld ? "按住" : "松开", m_AttackCount,
                networkStatus == null ? string.Empty : "  网络：" + networkStatus));
        }

        /// <summary>创建两名角色和一支箭矢的静态预览，不产生玩法状态。</summary>
        /// <returns>全部外观创建且当前场景仍有效时为 true；失败或停止取消时为 false。</returns>
        public async Task<bool> CreatePreviewAsync()
        {
            UnitViewManager views = m_UnitViews;
            if (views == null) throw new InvalidOperationException("表现管理器尚未启动。");
            UnityEngine.Camera camera = m_Camera;
            UnitView first = await views.CreateAsync(1, "tiny_dungeon_tile_0084", PreviewOrigin);
            if (!ReferenceEquals(views, m_UnitViews) || first == null) return false;
            first.SetHealth(100, 100, camera);
            UnitView second = await views.CreateAsync(2, "tiny_dungeon_tile_0085", new Vector2(-2.5f, 6.5f));
            if (!ReferenceEquals(views, m_UnitViews) || second == null) return false;
            second.SetFacing(true);
            second.SetHealth(60, 100, camera);
            UnitView arrow = await views.CreateAsync(3, "tiny_dungeon_tile_0131", new Vector2(-3.5f, 5.5f));
            if (!ReferenceEquals(views, m_UnitViews) || arrow == null) return false;
            arrow.SetProjectileDirection(Vector2.right);
            return true;
        }

        /// <summary>取消未完成的单位创建，销毁外观并归还相机；允许重复调用。</summary>
        public void Stop()
        {
            UnitViewManager views = m_UnitViews;
            CameraView cameraView = m_CameraView;
            m_UnitViews = null;
            m_CameraView = null;
            m_Camera = null;
            m_Hud = null;
            m_AttackCount = 0;
            m_NextDisplayTime = 0;
            // 先从单例解绑本次场景，使异步续体不能再修改已退出的外观。
            try { views?.Dispose(); }
            finally { cameraView?.Dispose(); }
        }
    }
}
