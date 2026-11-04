using UnityEngine;

namespace Portfolio.Research
{
    /// <summary>研磨面を基準とした表示点・入力点を取得する掲載用サンプル。</summary>
    public sealed class RelativePoseProvider : MonoBehaviour
    {
        [SerializeField] private Transform origin;
        [SerializeField] private Transform visualPoint;
        [SerializeField] private Transform rawPoint;
        [SerializeField] private ContactFlag contact;
        [SerializeField, Min(1)] private int sampleRateHz = 100;
        [SerializeField] private float positionScale = 1000f;
        [SerializeField] private SurfaceAxis normalAxis = SurfaceAxis.Y;
        [SerializeField] private bool penetrationWhenNegative = true;

        private readonly object sampleLock = new object();
        private PoseSample latest;
        private bool hasSample;
        private float nextSampleTime;
        private long sampleIndex;

        public void SetRawPoint(Transform point) => rawPoint = point;
        private void Start() => nextSampleTime = Time.unscaledTime;

        private void Update()
        {
            if (sampleRateHz <= 0 || origin == null || visualPoint == null || rawPoint == null) return;
            float interval = 1f / sampleRateHz;
            float now = Time.unscaledTime;
            // 元実装と同じ上限10回の追いつき処理。Updateの実行周期を超える測定精度は保証しない。
            for (int guard = 0; now >= nextSampleTime && guard < 10; guard++)
            {
                Capture();
                nextSampleTime += interval;
            }
            if (now - nextSampleTime > 1f) nextSampleTime = now + interval;
        }

        private void Capture()
        {
            PoseSample sample = PoseMath.Measure(origin, visualPoint, rawPoint,
                normalAxis, penetrationWhenNegative, positionScale,
                sampleIndex++, Time.realtimeSinceStartupAsDouble, contact != null && contact.IsTouching);
            lock (sampleLock)
            {
                latest = sample;
                hasSample = true;
            }
        }

        public bool TryGetLatestSample(out PoseSample sample)
        {
            lock (sampleLock) { sample = latest; return hasSample; }
        }
    }
}
