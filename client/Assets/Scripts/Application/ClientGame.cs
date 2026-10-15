using System;
using System.Threading.Tasks;
using GameSync.Config;
using GameSync.View.Unit;
using GameSync.View.Map;
using GameSync.View.Camera;
using GameSync.View.UI;
using UnityEngine;

namespace GameSync.Application
{
    /// <summary>协调客户端初始化与退出，不实现游戏规则或单位外观细节。</summary>
    public sealed class ClientGame : IDisposable
    {
        /// <summary>单位外观及其生命周期管理器。</summary>
        private readonly UnitViewManager m_UnitViews;
        /// <summary>只管理相机显示区域。</summary>
        private readonly CameraView m_CameraView;
        /// <summary>血条投影使用的场景相机。</summary>
        private readonly Camera m_Camera;
        /// <summary>应用状态显示。</summary>
        private readonly GameHudView m_Hud;
        /// <summary>是否已启动初始化流程。</summary>
        private bool m_Started;
        /// <summary>是否已释放当前对象。</summary>
        private bool m_Disposed;

        public UnitViewManager UnitViews { get { return m_UnitViews; } }

        public ClientGame(MapView map, Camera camera, GameHudView hud)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (hud == null) throw new ArgumentNullException(nameof(hud));
            m_CameraView = new CameraView(camera, map.Bounds);
            m_Camera = camera;
            m_Hud = hud;
            m_UnitViews = new UnitViewManager(map.UnitsRoot, new UnitViewFactory(hud.HealthBarRoot));
        }

        public async Task<bool> StartAsync()
        {
            if (m_Disposed) throw new ObjectDisposedException(nameof(ClientGame));
            if (m_Started) throw new InvalidOperationException("客户端流程已经启动。");
            m_Started = true;
            m_Hud.SetStatus("正在加载外观…");
            m_CameraView.Refresh();
            if (!ConfigManager.Instance.LoadAll()) return false;
            // 静态展示公共表现接口；不推进位置、伤害或重生逻辑。
            UnitView first = await m_UnitViews.CreateAsync(1, "tiny_dungeon_tile_0084", new Vector2(-4.5f, 6.5f));
            if (m_Disposed || first == null) return false;
            first.SetHealth(100, 100, m_Camera);
            UnitView second = await m_UnitViews.CreateAsync(2, "tiny_dungeon_tile_0085", new Vector2(-2.5f, 6.5f));
            if (m_Disposed || second == null) return false;
            second.SetFacing(true);
            second.SetHealth(60, 100, m_Camera);
            UnitView arrow = await m_UnitViews.CreateAsync(3, "tiny_dungeon_tile_0131", new Vector2(-3.5f, 5.5f));
            if (m_Disposed || arrow == null) return false;
            arrow.SetProjectileDirection(Vector2.right);
            m_Hud.SetStatus("公共表现预览 · 两个角色与箭矢 · 尚未接入操作");
            return true;
        }

        public void LateUpdate()
        {
            if (!m_Disposed) m_CameraView.Refresh();
        }

        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;
            m_UnitViews.Dispose();
            m_CameraView.Dispose();
            if (m_Hud != null) m_Hud.SetStatus(string.Empty);
        }
    }
}
