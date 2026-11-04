using System;
using UnityEngine;

namespace Portfolio.Research
{
    public enum SurfaceAxis { X, Y, Z }

    [Serializable]
    public struct PoseSample
    {
        public long Index;
        public double TimeSeconds;
        public Vector3 VisualPositionMm, RawPositionMm;
        public Vector3 VisualEulerDegrees, RawEulerDegrees;
        public float PenetrationMm;
        public bool Contact;
    }

    public static class PoseMath
    {
        public static PoseSample Measure(Transform origin, Transform visual, Transform raw,
            SurfaceAxis axis, bool negativeIsPenetration, float scale,
            long index, double time, bool contact)
        {
            Vector3 visualLocal = origin.InverseTransformPoint(visual.position);
            Vector3 rawLocal = origin.InverseTransformPoint(raw.position);
            Quaternion inverseOrigin = Quaternion.Inverse(origin.rotation);
            float normal = axis == SurfaceAxis.X ? rawLocal.x :
                           axis == SurfaceAxis.Y ? rawLocal.y : rawLocal.z;
            return new PoseSample
            {
                Index = index, TimeSeconds = time,
                VisualPositionMm = visualLocal * scale,
                RawPositionMm = rawLocal * scale,
                VisualEulerDegrees = Normalize((inverseOrigin * visual.rotation).eulerAngles),
                RawEulerDegrees = Normalize((inverseOrigin * raw.rotation).eulerAngles),
                PenetrationMm = Mathf.Max(0f, negativeIsPenetration ? -normal : normal) * scale,
                Contact = contact
            };
        }

        private static Vector3 Normalize(Vector3 angles) =>
            new Vector3(Signed(angles.x), Signed(angles.y), Signed(angles.z));
        private static float Signed(float value) => value > 180f ? value - 360f : value;
    }
}
