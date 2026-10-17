using System;
using System.Threading.Tasks;
using GameSync.Config;
using GameSync.Input;
using GameSync.Logic.Network;
using GameSync.View;
using GameSync.View.Map;
using GameSync.View.UI;
using GameSync.View.Unit;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameSync.Application
{
    /// <summary>按顺序启动、更新和停止客户端各功能管理器。</summary>
    public sealed class ClientGame : IDisposable
    {
        /// <summary>场景提供的地图及单位根节点。</summary>
        private readonly MapView m_Map;
        /// <summary>场景游戏相机，由表现管理器暂时接管。</summary>
        private readonly Camera m_Camera;
        /// <summary>启动层持有的 HUD。</summary>
        private readonly GameHudView m_Hud;
        /// <summary>场景绑定的输入动作原资产。</summary>
        private readonly InputActionAsset m_InputActions;
        /// <summary>桌面端是否预览触屏控件。</summary>
        private readonly bool m_PreviewTouch;
        /// <summary>是否开启 N1 网络会话诊断。</summary>
        private readonly bool m_EnableNetworkSession;
        /// <summary>N1 服务端地址。</summary>
        private readonly string m_ServerHost;
        /// <summary>N1 服务端 UDP 端口。</summary>
        private readonly int m_ServerPort;
        /// <summary>是否已经进入启动流程。</summary>
        private bool m_Started;
        /// <summary>是否已经结束本次应用流程。</summary>
        private bool m_Disposed;

        /// <summary>当前应用拥有的单位外观集合；未启动或已退出时为空。</summary>
        public UnitViewManager UnitViews { get { return m_Disposed ? null : PresentationManager.Instance.UnitViews; } }
        /// <summary>最近一次预览采样；攻击按下边沿只对应那次消费。</summary>
        public PlayerInputState CurrentInput { get { return m_Disposed ? default : InputManager.Instance.CurrentInput; } }

        /// <summary>保存场景配置；构造阶段不接管相机、加载资源或启动网络。</summary>
        /// <param name="map">已验证的地图表现容器，不可为空。</param>
        /// <param name="camera">用于世界显示和鼠标投影的场景游戏相机，不可为空。</param>
        /// <param name="hud">由 Bootstrap 创建并持有的 HUD，不可为空。</param>
        /// <param name="inputActions">Inspector 绑定的输入动作原资产，不可为空。</param>
        /// <param name="previewTouch">true 时桌面也显示触屏控件；移动平台始终显示。</param>
        /// <param name="enableNetworkSession">true 时启动 N1 局域网会话诊断。</param>
        /// <param name="serverHost">诊断服务端地址；诊断关闭时不使用。</param>
        /// <param name="serverPort">诊断服务端 UDP 端口；诊断关闭时不使用。</param>
        public ClientGame(MapView map, Camera camera, GameHudView hud, InputActionAsset inputActions,
            bool previewTouch = false, bool enableNetworkSession = false, string serverHost = "127.0.0.1", int serverPort = 9050)
        {
            m_Map = map ?? throw new ArgumentNullException(nameof(map));
            m_Camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            m_Hud = hud != null ? hud : throw new ArgumentNullException(nameof(hud));
            m_InputActions = inputActions != null ? inputActions :
                throw new ArgumentException("请在 GameMainBootstrap 绑定 PlayerControls.inputactions，并重新加载场景。");
            m_PreviewTouch = previewTouch;
            m_EnableNetworkSession = enableNetworkSession;
            m_ServerHost = serverHost;
            m_ServerPort = serverPort;
        }

        /// <summary>加载配置和静态展示外观，然后启动输入与可选网络诊断。</summary>
        /// <returns>所需外观和输入准备完成时为 true；配置或外观创建失败、退出取消时为 false。</returns>
        /// <remarks>只能调用一次；Bootstrap 负责在失败或异常后释放本次流程。</remarks>
        public async Task<bool> StartAsync()
        {
            if (m_Disposed) throw new ObjectDisposedException(nameof(ClientGame));
            if (m_Started) throw new InvalidOperationException("客户端流程已经启动。");
            m_Started = true;
            m_Hud.SetStatus("正在加载外观…");
            PresentationManager.Instance.Start(m_Map, m_Camera, m_Hud);
            PresentationManager.Instance.RefreshCamera();
            if (!ConfigManager.Instance.LoadAll()) return false;
            if (!await PresentationManager.Instance.CreatePreviewAsync()) return false;
            if (m_Disposed) return false;
            // 触屏控件须先于输入读取器创建；网络诊断只在输入准备后启动。
            InputManager.Instance.Start(m_Hud, m_Camera, m_InputActions, m_PreviewTouch, PresentationManager.PreviewOrigin);
            NetworkManager.Instance.Start(m_EnableNetworkSession, m_ServerHost, m_ServerPort);
            m_Hud.SetStatus("输入已接入：WASD/方向键移动意图，鼠标瞄准、左键攻击；尚未接入模拟");
            return true;
        }

        /// <summary>先刷新显示相机，再轮询网络并消费本帧输入预览。</summary>
        public void LateUpdate()
        {
            if (m_Disposed) return;
            PresentationManager.Instance.RefreshCamera();
            NetworkManager.Instance.Poll();
            if (!InputManager.Instance.Sample()) return;
            PresentationManager.Instance.UpdateInputPreview(InputManager.Instance.CurrentInput, NetworkManager.Instance.Status);
        }

        /// <summary>转交焦点与暂停合成状态；输入尚未创建时也会缓存。</summary>
        /// <param name="active">true 表示有焦点且未暂停；false 停止采集并清空预览输入。</param>
        public void SetInputActive(bool active)
        {
            if (!m_Disposed) InputManager.Instance.SetActive(active);
        }

        /// <summary>幂等停止输入和网络，销毁静态外观并归还场景相机。</summary>
        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;
            // 输入回调先退出，再释放触屏控件、网络连接和单位外观。
            try { InputManager.Instance.Stop(); }
            finally
            {
                try { NetworkManager.Instance.Stop(); }
                finally
                {
                    try { PresentationManager.Instance.Stop(); }
                    finally { if (m_Hud != null) m_Hud.SetStatus(string.Empty); }
                }
            }
        }
    }
}
