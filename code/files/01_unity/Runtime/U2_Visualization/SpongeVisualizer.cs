using UnityEngine;

namespace Portfolio.Research
{
    /// <summary>SpongeVisualizerから入力・フィルタ・描画の責務を分離したサンプル。</summary>
    public sealed class SpongeVisualizer : MonoBehaviour
    {
        [SerializeField] private ExperimentTcpBridge tcp;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private float movementScale = 0.001f;
        [SerializeField] private Vector3 moveDirection = Vector3.forward;
        [SerializeField, Range(0f, 0.5f)] private float smoothTime = 0.1f;
        [SerializeField] private float noiseThreshold = 0.05f;
        [SerializeField] private Color normalColor = Color.white, nearZeroColor = Color.blue, activeColor = Color.red;
        [SerializeField] private bool useNearZeroColor = true;
        [SerializeField] private float nearZeroThreshold = 1f, activeThreshold = 80f;
        [Header("実機なしの表示確認用。研究計測値ではありません")]
        [SerializeField] private bool useDemoInput;
        [SerializeField] private float demoDisplacementMm;

        private readonly DisplacementSignal signal = new DisplacementSignal();
        private Vector3 initialPosition, velocity;

        private void Start()
        {
            initialPosition = transform.localPosition;
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            if (!useDemoInput && tcp == null) Debug.LogWarning("TCP参照を設定してください。", this);
            UpdateColor(useDemoInput ? demoDisplacementMm : tcp != null ? tcp.Displacement : 0f);
        }

        private void Update()
        {
            if (!useDemoInput && tcp == null) return;
            float raw = useDemoInput ? demoDisplacementMm : tcp.Displacement;
            float filtered = signal.Observe(raw, noiseThreshold);
            Vector3 target = initialPosition + moveDirection * filtered * movementScale;
            transform.localPosition = Vector3.SmoothDamp(transform.localPosition, target, ref velocity, smoothTime);
            UpdateColor(raw);
        }

        private void UpdateColor(float raw)
        {
            if (targetRenderer == null) return;
            DisplacementBand band = DisplacementSignal.Classify(raw, useNearZeroColor, nearZeroThreshold, activeThreshold);
            Color color = band == DisplacementBand.NearZero ? nearZeroColor :
                          band == DisplacementBand.Active ? activeColor : normalColor;
            if (targetRenderer.material.color != color) targetRenderer.material.color = color;
        }
    }
}
