using UnityEngine;

namespace Colony.Interactions
{
    public interface IPresentationSpeedSource
    {
        float PresentationSpeedFactor { get; }
    }

    public static class PresentationTime
    {
        private static IPresentationSpeedSource source;

        public static float SpeedFactor
        {
            get
            {
                if (source == null)
                    return 1f;

                float value = source.PresentationSpeedFactor;
                if (float.IsNaN(value) || float.IsInfinity(value))
                    return 1f;

                return Mathf.Max(0f, value);
            }
        }

        public static float DeltaTime => Time.unscaledDeltaTime * SpeedFactor;

        public static void RegisterSource(IPresentationSpeedSource value)
        {
            source = value;
        }

        public static void UnregisterSource(IPresentationSpeedSource value)
        {
            if (ReferenceEquals(source, value))
                source = null;
        }
    }
}
