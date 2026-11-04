using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Portfolio.Research
{
    public sealed class SetCsvStore : MonoBehaviour
    {
        [Header("Session")]
        [SerializeField] private string participantId = "P001";
        [SerializeField] private string outputFolderName = "ExperimentResults";

        [Header("7-point Questions")]
        [Tooltip("実験開始後に行った変更は、次のセッションから反映されます。")]
        [SerializeField] private List<SurveyQuestionDefinition> questions =
            new List<SurveyQuestionDefinition> { new SurveyQuestionDefinition() };

        [Header("Runtime Status (Read Only)")]
        [SerializeField] private string currentSessionDirectory;

        private sealed class TrialRow
        {
            public int Trial;
            public int Condition;
            public int Stiffness;
            public int Vibration;
            public int[] Scores;
            public string AnsweredAt = string.Empty;
            public int Revision;
        }

        private readonly Dictionary<int, List<TrialRow>> rowsBySet =
            new Dictionary<int, List<TrialRow>>();
        private List<SurveyQuestionDefinition> activeQuestions =
            new List<SurveyQuestionDefinition>();
        private string cachedAnswerHistory;
        private readonly Dictionary<int, string> cachedSetHistory = new Dictionary<int, string>();

        public bool SessionActive { get; private set; }
        public string OutputRootDirectory => Path.GetFullPath(
            Path.Combine(Application.dataPath, outputFolderName, "si2026"));
        public string CurrentSessionDirectory => currentSessionDirectory;
        public IReadOnlyList<SurveyQuestionDefinition> Questions =>
            SessionActive ? activeQuestions : questions;
        public int MaxCreatedSet => rowsBySet.Count == 0 ? 0 : rowsBySet.Keys.Max();

        public void BeginSession()
        {
            rowsBySet.Clear();
            cachedSetHistory.Clear();
            cachedAnswerHistory = null;
            activeQuestions = BuildQuestionSnapshot();

            string safeParticipantId = SanitizeFileName(participantId, "Participant");
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string rootDirectory = OutputRootDirectory;
            currentSessionDirectory = CreateUniqueDirectory(rootDirectory, $"{safeParticipantId}_{timestamp}");
            Directory.CreateDirectory(currentSessionDirectory);
            SessionActive = true;

            Debug.Log($"[SetCsvStore] Session directory: {currentSessionDirectory}", this);
        }

        public void CreateSet(int setNumber, IReadOnlyList<ExperimentCondition> conditionOrder)
        {
            if (!SessionActive)
            {
                BeginSession();
            }

            if (setNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(setNumber));
            }

            if (conditionOrder == null || conditionOrder.Count == 0)
            {
                throw new ArgumentException("At least one condition is required.", nameof(conditionOrder));
            }

            List<TrialRow> rows = new List<TrialRow>(conditionOrder.Count);
            for (int index = 0; index < conditionOrder.Count; index++)
            {
                rows.Add(new TrialRow
                {
                    Trial = index + 1,
                    Condition = conditionOrder[index].Id,
                    Stiffness = conditionOrder[index].StiffnessId,
                    Vibration = conditionOrder[index].VibrationId,
                    Scores = new int[activeQuestions.Count]
                });
            }

            rowsBySet[setNumber] = rows;
            cachedSetHistory.Remove(setNumber);
            cachedAnswerHistory = null;
            WriteSetFile(setNumber);
        }

        public bool HasSet(int setNumber)
        {
            return rowsBySet.ContainsKey(setNumber);
        }

        public int GetTrialCount(int setNumber)
        {
            return rowsBySet.TryGetValue(setNumber, out List<TrialRow> rows) ? rows.Count : 0;
        }

        public bool TryGetCondition(int setNumber, int trialNumber, out int conditionId)
        {
            conditionId = 0;
            if (!TryGetRow(setNumber, trialNumber, out TrialRow row))
            {
                return false;
            }

            conditionId = row.Condition;
            return true;
        }

        public bool TryGetScores(int setNumber, int trialNumber, out int[] scores)
        {
            scores = null;
            if (!TryGetRow(setNumber, trialNumber, out TrialRow row))
            {
                return false;
            }

            scores = (int[])row.Scores.Clone();
            return true;
        }

        public bool TryGetConditionPair(int setNumber, int trialNumber, out ExperimentCondition condition)
        {
            condition = default;
            if (!TryGetRow(setNumber, trialNumber, out TrialRow row)) return false;
            condition = new ExperimentCondition(row.Condition, row.Stiffness, row.Vibration);
            return true;
        }

        public string GetAnswerHistoryText()
        {
            if (cachedAnswerHistory == null)
                cachedAnswerHistory = QuestionnaireHistory.Format(Questions.Count,
                    rowsBySet.OrderBy(pair => pair.Key).SelectMany(pair =>
                        pair.Value.Select(row => (pair.Key, row.Trial, row.Scores))));
            return cachedAnswerHistory;
        }

        public string GetAnswerHistoryText(int setNumber)
        {
            if (!rowsBySet.TryGetValue(setNumber, out List<TrialRow> rows))
                return string.Empty;
            if (!cachedSetHistory.TryGetValue(setNumber, out string history))
            {
                history = QuestionnaireHistory.FormatSet(Questions.Count,
                    rows.Select(row => (setNumber, row.Trial, row.Scores)), setNumber);
                cachedSetHistory[setNumber] = history;
            }
            return history;
        }

        public bool SaveAnswers(int setNumber, int trialNumber, IReadOnlyList<int> scores, out string error)
        {
            error = string.Empty;
            if (!TryGetRow(setNumber, trialNumber, out TrialRow row))
            {
                error = $"Set {setNumber}, Trial {trialNumber} does not exist.";
                return false;
            }

            if (scores == null || scores.Count != activeQuestions.Count)
            {
                error = $"Expected {activeQuestions.Count} answer(s).";
                return false;
            }

            for (int index = 0; index < scores.Count; index++)
            {
                if (scores[index] < 1 || scores[index] > 7)
                {
                    error = $"Answer {index + 1} must be from 1 to 7.";
                    return false;
                }
            }

            int[] previousScores = row.Scores;
            string previousAnsweredAt = row.AnsweredAt;
            int previousRevision = row.Revision;
            row.Scores = scores.ToArray();
            row.AnsweredAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            row.Revision++;

            try
            {
                WriteSetFile(setNumber);
                cachedAnswerHistory = null;
                cachedSetHistory.Remove(setNumber);
                return true;
            }
            catch (Exception exception)
            {
                row.Scores = previousScores;
                row.AnsweredAt = previousAnsweredAt;
                row.Revision = previousRevision;
                error = exception.Message;
                Debug.LogError($"[SetCsvStore] Save failed: {exception}", this);
                return false;
            }
        }

        private bool TryGetRow(int setNumber, int trialNumber, out TrialRow row)
        {
            row = null;
            if (!rowsBySet.TryGetValue(setNumber, out List<TrialRow> rows) ||
                trialNumber < 1 || trialNumber > rows.Count)
            {
                return false;
            }

            row = rows[trialNumber - 1];
            return true;
        }

        private void WriteSetFile(int setNumber)
        {
            if (!rowsBySet.TryGetValue(setNumber, out List<TrialRow> rows))
            {
                return;
            }

            StringBuilder csv = new StringBuilder();
            csv.Append("Trial,Condition,Stiffness,Vibration,AnsweredAt,Revision");
            foreach (SurveyQuestionDefinition question in activeQuestions)
            {
                csv.Append(',').Append(EscapeCsv(question.id));
            }
            csv.AppendLine();

            foreach (TrialRow row in rows)
            {
                csv.Append(row.Trial.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.Condition.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.Stiffness.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.Vibration.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(EscapeCsv(row.AnsweredAt)).Append(',')
                    .Append(row.Revision.ToString(CultureInfo.InvariantCulture));

                foreach (int score in row.Scores)
                {
                    csv.Append(',');
                    if (score > 0)
                    {
                        csv.Append(score.ToString(CultureInfo.InvariantCulture));
                    }
                }
                csv.AppendLine();
            }

            string path = GetSetFilePath(setNumber);
            string temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, csv.ToString(), new UTF8Encoding(true));

            if (!File.Exists(path))
            {
                File.Move(temporaryPath, path);
                return;
            }

            try
            {
                File.Replace(temporaryPath, path, null);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(temporaryPath, path, true);
                File.Delete(temporaryPath);
            }
        }

        private string GetSetFilePath(int setNumber)
        {
            string safeParticipantId = SanitizeFileName(participantId, "Participant");
            return Path.Combine(
                currentSessionDirectory,
                $"{safeParticipantId}_Set{setNumber:00}.csv");
        }

        private List<SurveyQuestionDefinition> BuildQuestionSnapshot()
        {
            List<SurveyQuestionDefinition> snapshot = questions
                .Where(question => question != null)
                .Select(question => question.Clone())
                .ToList();

            if (snapshot.Count == 0)
            {
                snapshot.Add(new SurveyQuestionDefinition());
            }

            HashSet<string> usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < snapshot.Count; index++)
            {
                string id = SanitizeColumnId(snapshot[index].id, $"Question{index + 1}");
                string uniqueId = id;
                int suffix = 2;
                while (!usedIds.Add(uniqueId))
                {
                    uniqueId = $"{id}_{suffix++}";
                }
                snapshot[index].id = uniqueId;
            }

            return snapshot;
        }

        private static string CreateUniqueDirectory(string root, string baseName)
        {
            string candidate = Path.Combine(root, baseName);
            int suffix = 2;
            while (Directory.Exists(candidate))
            {
                candidate = Path.Combine(root, $"{baseName}_{suffix++}");
            }
            return candidate;
        }

        private static string SanitizeFileName(string value, string fallback)
        {
            string result = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalidCharacter, '_');
            }
            return string.IsNullOrWhiteSpace(result) ? fallback : result;
        }

        private static string SanitizeColumnId(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            StringBuilder result = new StringBuilder();
            foreach (char character in value.Trim())
            {
                if (char.IsLetterOrDigit(character) || character == '_')
                {
                    result.Append(character);
                }
                else
                {
                    result.Append('_');
                }
            }

            return result.Length == 0 ? fallback : result.ToString();
        }

        private static string EscapeCsv(string value)
        {
            value ??= string.Empty;
            if (!value.Contains(",") && !value.Contains("\"") &&
                !value.Contains("\n") && !value.Contains("\r"))
            {
                return value;
            }

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
