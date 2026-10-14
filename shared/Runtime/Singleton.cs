using System;

namespace GameSync
{
    public abstract class Singleton<T> where T : Singleton<T>
    {
        /// <summary>延迟创建并缓存唯一实例。</summary>
        private static readonly Lazy<T> m_Instance = new Lazy<T>(CreateInstance);

        public static T Instance
        {
            get { return m_Instance.Value; }
        }

        protected Singleton() { }

        private static T CreateInstance()
        {
            // 派生类使用私有无参构造，避免外部直接 new。
            var instance = Activator.CreateInstance(typeof(T), true) as T;
            if (instance == null)
            {
                throw new InvalidOperationException("无法创建单例：" + typeof(T).Name);
            }

            return instance;
        }
    }
}
