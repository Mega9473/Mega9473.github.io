using System;
using System.Collections.Generic;

namespace Portfolio.Research
{
    /// <summary>元ExperimentSequenceのseed式とFisher–Yates順序を維持。</summary>
    public static class ConditionSchedule
    {
        public static List<List<int>> Create(IReadOnlyList<ExperimentCondition> conditions, int totalSets, int seed)
        {
            if (conditions == null || conditions.Count == 0) throw new ArgumentException("条件が必要です。");
            if (totalSets < 1) throw new ArgumentOutOfRangeException(nameof(totalSets));
            var sets = new List<List<int>>();
            for (int set = 1; set <= totalSets; set++)
            {
                var order = new List<int>();
                foreach (ExperimentCondition condition in conditions) order.Add(condition.Id);
                Shuffle(order, new Random(unchecked(seed + set * 1009)));
                sets.Add(order);
            }
            return sets;
        }

        private static void Shuffle<T>(IList<T> list, Random random)
        {
            for (int index = list.Count - 1; index > 0; index--)
            {
                int swap = random.Next(index + 1);
                T value = list[index];
                list[index] = list[swap];
                list[swap] = value;
            }
        }
    }
}
