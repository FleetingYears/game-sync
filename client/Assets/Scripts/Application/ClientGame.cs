using System;
using System.Threading.Tasks;
using GameSync.Config;
using GameSync.Input;
using GameSync.Logic.Network;
using GameSync.View.Unit;
using GameSync.View.Map;
using GameSync.View.Camera;
using GameSync.View.UI;
using UnityEngine;
using UnityEngine.InputSystem;

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
        /// <summary>是否在桌面编辑器中预览触屏操作。</summary>
        private readonly bool m_PreviewTouch;
        /// <summary>键鼠与触屏的统一输入采样器。</summary>
        private PlayerInputReader m_Input;
        /// <summary>当前应用拥有的触屏操作区。</summary>
        private TouchInputView m_TouchInput;
        /// <summary>焦点和暂停状态允许输入时为真。</summary>
        private bool m_InputActive = true;
        /// <summary>用于验证短按采集的累计次数。</summary>
        private long m_AttackCount;
        /// <summary>下一次调试文本更新的显示时间。</summary>
        private float m_NextInputDisplayTime;
        /// <summary>场景绑定的输入动作配置，不直接启用原资产。</summary>
        private readonly InputActionAsset m_InputActions;
        /// <summary>是否在表现预览中启动 N1 会话诊断。</summary>
        private readonly bool m_EnableNetworkSession;
        /// <summary>Inspector 配置的局域网服务端地址。</summary>
        private readonly string m_ServerHost;
        /// <summary>Inspector 配置的局域网 UDP 端口。</summary>
        private readonly int m_ServerPort;
        /// <summary>由应用流程持有和释放的 N1 客户端连接。</summary>
        private SessionClient m_NetworkSession;

        /// <summary>当前应用拥有的单位外观管理器，生命周期随应用一起结束。</summary>
        public UnitViewManager UnitViews { get { return m_UnitViews; } }
        /// <summary>最近一次 LateUpdate 的输入预览；按下边沿只对应那次消费，不可直接充当网络 Tick 输入。</summary>
        public PlayerInputState CurrentInput { get; private set; }

        /// <summary>连接公共表现与输入配置，构造阶段不加载资源或启用输入。</summary>
        /// <param name="map">已验证的地图表现容器，提供单位父节点与显示边界，不可为空。</param>
        /// <param name="camera">场景游戏相机，用于地图显示、血条世界投影及鼠标瞄准，不是 UI 相机。</param>
        /// <param name="hud">已初始化的 HUD，提供血条容器及输入预览文字，所有权属于 Bootstrap。</param>
        /// <param name="inputActions">Inspector 绑定的输入配置原资产，运行时由输入模块复制使用，不可为空。</param>
        /// <param name="previewTouch">true 时桌面也创建触屏控件；false 时仅移动平台自动创建。</param>
        /// <param name="enableNetworkSession">true 时启动 N1 局域网会话诊断。</param>
        /// <param name="serverHost">N1 服务端地址；诊断关闭时不使用。</param>
        /// <param name="serverPort">N1 服务端 UDP 端口；诊断关闭时不使用。</param>
        public ClientGame(MapView map, Camera camera, GameHudView hud, InputActionAsset inputActions,
            bool previewTouch = false, bool enableNetworkSession = false, string serverHost = "127.0.0.1", int serverPort = 9050)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (hud == null) throw new ArgumentNullException(nameof(hud));
            if (inputActions == null) throw new ArgumentException("请在 GameMainBootstrap 绑定 PlayerControls.inputactions，并重新加载场景。");
            m_InputActions = inputActions;
            m_CameraView = new CameraView(camera, map.Bounds);
            m_Camera = camera;
            m_Hud = hud;
            m_PreviewTouch = previewTouch;
            m_EnableNetworkSession = enableNetworkSession;
            m_ServerHost = serverHost;
            m_ServerPort = serverPort;
            m_UnitViews = new UnitViewManager(map.UnitsRoot, new UnitViewFactory(hud.HealthBarRoot));
        }

        /// <summary>加载配置和展示外观，在展示准备完成后创建并启用输入采集。</summary>
        /// <returns>所需外观与输入初始化成功时为 true；配置/创建失败或异步创建期间已释放时为 false。</returns>
        /// <remarks>只能启动一次；其他未处理初始化异常交由 Bootstrap 捕获并释放。</remarks>
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
            // 先建立触屏 UI，让屏幕控件创建并标记虚拟设备，再创建监听这些设备的玩法读取器。
            // 桌面默认只采集键鼠，预览开关用于调试触屏布局，不改变原输入配置。
            if (UnityEngine.Application.isMobilePlatform || m_PreviewTouch)
                m_TouchInput = TouchInputView.Create(m_Hud);
            m_Input = new PlayerInputReader(m_InputActions, m_TouchInput);
            // 初始化等待期间也可能收到失焦/暂停事件，使用缓存状态而非无条件启用输入。
            m_Input.SetActive(m_InputActive);
            if (m_EnableNetworkSession)
            {
                m_NetworkSession = new SessionClient();
                m_NetworkSession.Connect(m_ServerHost, m_ServerPort);
            }
            m_Hud.SetStatus("输入已接入：WASD/方向键移动意图，鼠标瞄准、左键攻击；尚未接入模拟");
            return true;
        }

        /// <summary>刷新相机，并在渲染帧中采样输入用于当前 HUD 预览。</summary>
        public void LateUpdate()
        {
            if (m_Disposed) return;
            // 先刷新相机，再用它计算鼠标世界方向，避免使用上一次显示区域进行投影。
            m_CameraView.Refresh();
            // 网络轮询固定在 Unity 主线程，且由适配器限制本次处理的包与事件数量。
            m_NetworkSession?.Poll();
            if (m_Input == null) return;
            // 这里只做输入预览，用明确的展示初始坐标作为瞄准原点。
            // Consume 会清空攻击边沿；接入模拟后应由固定 Tick 适配器决定消费时机并提供逻辑位置。
            CurrentInput = m_Input.Consume(new Vector2(-4.5f, 6.5f), m_Camera);
            if (CurrentInput.AttackPressed) m_AttackCount++;
            // 输入每帧采集，文字每 0.1 秒才更新，避免反复重建 UI 文本。
            // 使用不受 timeScale 影响的显示时间，便于暂停模拟时继续观察输入。
            if (Time.unscaledTime < m_NextInputDisplayTime) return;
            m_NextInputDisplayTime = Time.unscaledTime + 0.1f;
            m_Hud.SetStatus(string.Format("输入预览（未接模拟） 移动 {0:F2},{1:F2}  瞄准 {2:F2},{3:F2}  攻击 {4}  按下 {5}{6}",
                CurrentInput.Move.x, CurrentInput.Move.y, CurrentInput.Aim.x, CurrentInput.Aim.y,
                CurrentInput.AttackHeld ? "按住" : "松开", m_AttackCount,
                m_NetworkSession == null ? string.Empty : "  网络：" + m_NetworkSession.Status));
        }

        /// <summary>同步焦点/暂停状态，兼容输入读取器尚未完成初始化的时段。</summary>
        /// <param name="active">true 表示当前有焦点且未暂停；false 停止采集并清空对外输入预览。</param>
        public void SetInputActive(bool active)
        {
            // 先缓存，使 StartAsync 中尚未创建的读取器稍后也能继承正确状态。
            m_InputActive = active;
            m_Input?.SetActive(active);
            if (!active) CurrentInput = default;
        }

        /// <summary>幂等停止输入并释放本应用创建的操作区和外观，归还场景相机。</summary>
        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;
            // 先停止动作回调，再销毁触屏控件和外观，避免清理期间继续产生玩法输入。
            m_Input?.Dispose();
            m_NetworkSession?.Dispose();
            CurrentInput = default;
            if (m_TouchInput != null)
            {
                m_TouchInput.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(m_TouchInput.gameObject);
            }
            m_UnitViews.Dispose();
            m_CameraView.Dispose();
            if (m_Hud != null) m_Hud.SetStatus(string.Empty);
        }
    }
}
