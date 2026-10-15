using System;
using System.Threading.Tasks;
using GameSync.Config;
using GameSync.View.Unit;
using UnityEngine;

namespace GameSync.Application
{
    /// <summary>协调客户端初始化与退出，不实现游戏规则或单位外观细节。</summary>
    public sealed class ClientGame : IDisposable
    {
        /// <summary>单位外观及其生命周期管理器。</summary>
        private readonly UnitViewManager m_UnitViews;
        /// <summary>是否已启动初始化流程。</summary>
        private bool m_Started;
        /// <summary>是否已释放当前对象。</summary>
        private bool m_Disposed;

        public ClientGame(Transform unitsRoot)
        {
            m_UnitViews = new UnitViewManager(unitsRoot, new UnitViewFactory());
        }

        public async Task<bool> StartAsync()
        {
            if (m_Disposed) throw new ObjectDisposedException(nameof(ClientGame));
            if (m_Started) throw new InvalidOperationException("客户端流程已经启动。");
            m_Started = true;
            if (!ConfigManager.Instance.LoadAll()) return false;
            // 暂时展示一个单位；后续改由逻辑或同步结果驱动表现。
            UnitView view = await m_UnitViews.CreateAsync(1, "tiny_dungeon_tile_0084", new Vector3(-4.5f, 6.5f, 0));
            return view != null;
        }

        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;
            m_UnitViews.Dispose();
        }
    }
}
