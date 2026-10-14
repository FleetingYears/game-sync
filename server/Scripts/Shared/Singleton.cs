using System;

namespace GameSync
{
    public abstract class Singleton<T> where T : Singleton<T>
    {
        private static readonly Lazy<T> _instance = new Lazy<T>(CreateInstance);

        public static T Instance
        {
            get { return _instance.Value; }
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
