using System;

namespace Portfolio.Research
{
    public enum DisplacementBand { Normal, NearZero, Active }

    /// <summary>Unityに依存しないデッドバンド。色判定には必ず生の値を使う。</summary>
    public sealed class DisplacementSignal
    {
        private bool initialized;
        public float Filtered { get; private set; }

        public float Observe(float raw, float noiseThreshold)
        {
            if (!initialized || Math.Abs(raw - Filtered) > noiseThreshold)
            {
                Filtered = raw;
                initialized = true;
            }
            return Filtered;
        }

        public static DisplacementBand Classify(float raw, bool nearZeroEnabled,
            float nearZeroThreshold, float activeThreshold)
        {
            float absolute = Math.Abs(raw);
            if (nearZeroEnabled && absolute <= Math.Max(0, nearZeroThreshold))
                return DisplacementBand.NearZero;
            return absolute >= activeThreshold ? DisplacementBand.Active : DisplacementBand.Normal;
        }
    }
}
