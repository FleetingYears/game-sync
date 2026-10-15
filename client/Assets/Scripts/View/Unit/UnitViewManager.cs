using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameSync.Logging;
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
        private readonly Dictionary<long, CancellationTokenSource> m_Creating = new Dictionary<long, CancellationTokenSource>();
        /// <summary>是否已释放当前对象。</summary>
        private bool m_Disposed;

        public UnitViewManager(Transform root, UnitViewFactory factory)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            m_Root = root;
            m_Factory = factory;
        }

        public async Task<UnitView> CreateAsync(long unitId, string spriteId, Vector2 position)
        {
            if (m_Disposed || m_Views.ContainsKey(unitId) || m_Creating.ContainsKey(unitId)) return null;
            var request = new CancellationTokenSource();
            m_Creating.Add(unitId, request);
            UnitView view = null;
            try
            {
                view = await m_Factory.CreateAsync(unitId, spriteId, m_Root, position, request.Token);
                if (view == null) return null;
                request.Token.ThrowIfCancellationRequested();
                m_Views.Add(unitId, view);
                return view;
            }
            catch (OperationCanceledException)
            {
                DestroyView(view);
                return null;
            }
            catch (Exception exception)
            {
                DestroyView(view);
                Log.Error("创建单位外观失败：" + unitId, exception);
                return null;
            }
            finally
            {
                CancellationTokenSource current;
                if (m_Creating.TryGetValue(unitId, out current) && ReferenceEquals(current, request))
                    m_Creating.Remove(unitId);
                request.Dispose();
            }
        }

        public bool TryGet(long unitId, out UnitView view)
        {
            view = null;
            return !m_Disposed && m_Views.TryGetValue(unitId, out view) && view != null;
        }

        public bool Remove(long unitId)
        {
            if (m_Disposed) return false;
            CancellationTokenSource request;
            bool removed = m_Creating.TryGetValue(unitId, out request);
            if (removed)
            {
                m_Creating.Remove(unitId);
                request.Cancel();
            }
            UnitView view;
            if (!m_Views.TryGetValue(unitId, out view)) return removed;
            m_Views.Remove(unitId);
            DestroyView(view);
            return true;
        }

        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;
            foreach (CancellationTokenSource request in m_Creating.Values) request.Cancel();
            m_Creating.Clear();
            foreach (UnitView view in m_Views.Values)
            {
                DestroyView(view);
            }
            m_Views.Clear();
        }

        private static void DestroyView(UnitView view)
        {
            if (view == null) return;
            view.gameObject.SetActive(false);
            Object.Destroy(view.gameObject);
        }
    }
}
