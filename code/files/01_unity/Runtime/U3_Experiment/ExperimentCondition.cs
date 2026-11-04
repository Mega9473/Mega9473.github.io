using System;
using System.Collections.Generic;

namespace Portfolio.Research
{
    // Internal ID identifies a combination; only the two factor IDs go over TCP.
    public readonly struct ExperimentCondition
    {
        public int Id { get; }
        public int StiffnessId { get; }
        public int VibrationId { get; }

        public ExperimentCondition(int id, int stiffnessId, int vibrationId)
        {
            Id = id;
            StiffnessId = stiffnessId;
            VibrationId = vibrationId;
        }

        public static List<ExperimentCondition> BuildCombinations(
            IReadOnlyList<int> stiffnessIds, IReadOnlyList<int> vibrationIds)
        {
            Validate(stiffnessIds, 1, "Stiffness IDs");
            Validate(vibrationIds, 0, "Vibration IDs");
            var result = new List<ExperimentCondition>();
            foreach (int stiffness in stiffnessIds)
                foreach (int vibration in vibrationIds)
                    result.Add(new ExperimentCondition(result.Count + 1, stiffness, vibration));
            return result;
        }

        private static void Validate(IReadOnlyList<int> ids, int minimum, string label)
        {
            if (ids == null || ids.Count == 0) throw new ArgumentException(label + " cannot be empty.");
            var used = new HashSet<int>();
            foreach (int id in ids)
                if (id < minimum || !used.Add(id))
                    throw new ArgumentException(label + " must be unique and >= " + minimum + ".");
        }
    }
}
