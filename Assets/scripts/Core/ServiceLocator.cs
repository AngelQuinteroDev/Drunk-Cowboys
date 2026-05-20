using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPSMultiplayer.Core
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new();

        public static void Register<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        public static T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return service as T;

            Debug.LogError($"[ServiceLocator] Service {typeof(T).Name} not registered.");
            return null;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var s))
            {
                service = s as T;
                return true;
            }
            service = null;
            return false;
        }

        public static void Unregister<T>() => _services.Remove(typeof(T));
    }
}