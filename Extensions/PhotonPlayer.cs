using Photon.Pun;
namespace LagBalancer.Extensions
{
    public static class PhotonPlayerExtensions
    {
        public static void SetProperty(this Photon.Realtime.Player instance, string key, object value)
        {
            var propKey = LagBalancer.GetCustomPropertyKey(key);
            var props = instance.CustomProperties;

            if (!props.ContainsKey(propKey))
            {
                props.Add(propKey, value);
            }
            else
            {
                props[propKey] = value;
            }

            instance.SetCustomProperties(props);
        }

        public static T GetProperty<T>(this Photon.Realtime.Player instance, string key)
        {
            var propKey = LagBalancer.GetCustomPropertyKey(key);
            var props = instance.CustomProperties;

            if (!props.ContainsKey(propKey))
            {
                LagBalancer.LogError($"Property \"{key}\" not found on Photon Player instance.");
                return default;
            }

            return (T)props[propKey];
        }
        public static bool TryGetProperty<T>(this Photon.Realtime.Player instance, string key, out T property)
        {
            var propKey = LagBalancer.GetCustomPropertyKey(key);
            var props = instance.CustomProperties;

            if (!props.ContainsKey(propKey))
            {
                property = default;
                return false;
            }

            property = (T)props[propKey];
            return true;
        }

    }
}

