using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameSync.View.Unit
{
    /// <summary>维护单位 ID 与外观的对应关系，并管理外观生命周期。</summary>
    public sealed class UnitViewManager : IDisposable
    {
        /// <summary>所有单位外观的父节点。</summary>
        private readonly Transform m_Root;
        /// <summary>负责加载和创建单位外观的工厂。</summary>
        private readonly UnitViewFactory m_Factory;
        /// <summary>按单位 ID 索引已创建的外观。</summary>
        private readonly Dictionary<long, UnitView> m_Views = new Dictionary<long, UnitView>();
        /// <summary>正在异步创建外观的单位 ID，防止重复创建。</summary>
        private readonly HashSet<long> m_Creating = new HashSet<long>();
        /// <summary>管理器生命周期的取消信号。</summary>
        private readonly CancellationTokenSource m_Lifetime = new CancellationTokenSource();
        /// <summary>是否已释放当前对象。</summary>
        private bool m_Disposed;

        public UnitViewManager(Transform root, UnitViewFactory factory)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            m_Root = root;
            m_Factory = factory;
        }

        public async Task<UnitView> CreateAsync(long unitId, string spriteId, Vector3 position)
        {
            ThrowIfDisposed();
            if (m_Views.ContainsKey(unitId) || !m_Creating.Add(unitId))
                throw new InvalidOperationException("单位外观已存在或正在创建：" + unitId);
            try
            {
                UnitView view = await m_Factory.CreateAsync(unitId, spriteId, m_Root, position, m_Lifetime.Token);
                if (view == null) return null;
                if (m_Disposed)
                {
                    if (view != null) Object.Destroy(view.gameObject);
                    throw new OperationCanceledException("单位表现管理器已释放。");
                }
                m_Views.Add(unitId, view);
                return view;
            }
            finally
            {
                m_Creating.Remove(unitId);
            }
        }

        public bool TryGet(long unitId, out UnitView view)
        {
            ThrowIfDisposed();
            return m_Views.TryGetValue(unitId, out view);
        }

        public bool Remove(long unitId)
        {
            ThrowIfDisposed();
            UnitView view;
            if (!m_Views.TryGetValue(unitId, out view)) return false;
            m_Views.Remove(unitId);
            if (view != null) Object.Destroy(view.gameObject);
            return true;
        }

        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;
            m_Lifetime.Cancel();
            foreach (UnitView view in m_Views.Values)
            {
                if (view != null) Object.Destroy(view.gameObject);
            }
            m_Views.Clear();
            m_Lifetime.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (m_Disposed) throw new ObjectDisposedException(nameof(UnitViewManager));
        }
    }
}
