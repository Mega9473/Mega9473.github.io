using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Portfolio.Research
{
    // Only persisted scores belong here; the selection cursor's default 4 is not an answer.
    public static class QuestionnaireHistory
    {
        public static string FormatSet(int questionCount,
            IEnumerable<(int Set, int Trial, int[] Scores)> rows, int setNumber)
        {
            return Format(questionCount, rows.Where(row => row.Set == setNumber));
        }

        public static string Format(int questionCount,
            IEnumerable<(int Set, int Trial, int[] Scores)> rows)
        {
            var text = new StringBuilder();
            text.Append("Set".PadRight(5)).Append(" | ").Append("Seq".PadRight(5));
            for (int i = 0; i < questionCount; i++)
                text.Append(" | ").Append(("Q" + (i + 1).ToString(CultureInfo.InvariantCulture)).PadRight(5));
            text.AppendLine();
            foreach (var row in rows)
            {
                text.Append(row.Set.ToString(CultureInfo.InvariantCulture).PadRight(5))
                    .Append(" | ").Append(row.Trial.ToString(CultureInfo.InvariantCulture).PadRight(5));
                for (int i = 0; i < questionCount; i++)
                {
                    int score = row.Scores != null && i < row.Scores.Length ? row.Scores[i] : 0;
                    text.Append(" | ").Append((score == 0 ? string.Empty :
                        score.ToString(CultureInfo.InvariantCulture)).PadRight(5));
                }
                text.AppendLine();
            }
            return text.ToString();
        }
    }
}
