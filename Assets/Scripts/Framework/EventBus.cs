using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarthOnline.Framework
{
    /// <summary>
    /// 全局事件总线。所有游戏模块通过它松耦合通信。
    /// 用法：
    ///   订阅: EventBus.Subscribe("OnItemPickup", data => { ... });
    ///   发布: EventBus.Publish("OnItemPickup", new Dictionary<string,object>{{"itemId", 42}});
    ///   取消: EventBus.Unsubscribe("OnItemPickup", handler);
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<string, List<Action<Dictionary<string, object>>>> _listeners
            = new Dictionary<string, List<Action<Dictionary<string, object>>>>();

        public static void Subscribe(string eventType, Action<Dictionary<string, object>> handler)
        {
            if (!_listeners.ContainsKey(eventType))
                _listeners[eventType] = new List<Action<Dictionary<string, object>>>();
            _listeners[eventType].Add(handler);
        }

        public static void Unsubscribe(string eventType, Action<Dictionary<string, object>> handler)
        {
            if (_listeners.ContainsKey(eventType))
                _listeners[eventType].Remove(handler);
        }

        public static void Publish(string eventType, Dictionary<string, object> data = null)
        {
            if (!_listeners.ContainsKey(eventType)) return;
            if (data == null) data = new Dictionary<string, object>();

            var handlers = new List<Action<Dictionary<string, object>>>(_listeners[eventType]);
            foreach (var handler in handlers)
            {
                try { handler.Invoke(data); }
                catch (Exception e) { Debug.LogError($"[EventBus] Error in handler for '{eventType}': {e}"); }
            }
        }

        public static void Clear()
        {
            _listeners.Clear();
        }

        // ── Typed event system (new code, coexists with string-based) ──
        private static readonly Dictionary<string, Delegate> _typedListeners
            = new Dictionary<string, Delegate>();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            string key = typeof(T).Name;
            if (_typedListeners.ContainsKey(key))
            {
                // 去重检查：防止同一个 handler 被重复订阅导致双发
                foreach (Delegate existing in _typedListeners[key].GetInvocationList())
                {
                    if (existing.Equals(handler))
                        return;
                }
                _typedListeners[key] = Delegate.Combine(_typedListeners[key], handler);
            }
            else
                _typedListeners[key] = handler;
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            string key = typeof(T).Name;
            if (_typedListeners.ContainsKey(key))
            {
                var remaining = Delegate.Remove(_typedListeners[key], handler);
                if (remaining == null) _typedListeners.Remove(key);
                else _typedListeners[key] = remaining;
            }
        }

        public static void Publish<T>(T data) where T : struct
        {
            string key = typeof(T).Name;
            if (_typedListeners.ContainsKey(key))
            {
                var handler = _typedListeners[key] as Action<T>;
                if (handler != null)
                {
                    try { handler.Invoke(data); }
                    catch (Exception e) { Debug.LogError($"[EventBus] Typed error in '{key}': {e}"); }
                }
            }
        }
    }
}
