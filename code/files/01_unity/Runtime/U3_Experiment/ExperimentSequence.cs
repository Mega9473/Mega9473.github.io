using System;
using System.Collections.Generic;
using UnityEngine;

namespace Portfolio.Research
{
    public enum ExperimentRunState
    {
        NotStarted,
        Presenting,
        WaitingForNextSet,
        Finished
    }

    public sealed class ExperimentSequence : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ExperimentTcpBridge tcpBridge;
        [SerializeField] private SetCsvStore csvStore;

        [Header("Experiment Settings")]
        [Tooltip("実験開始時に全セットの条件順・CSVを準備します。開始後の設定変更は次の実験から反映されます。")]
        [SerializeField, Min(1)] private int totalSets = 5;
        [Tooltip("剛性条件の番号。初期値は2,3,4。正の整数で重複不可。")]
        [SerializeField] private List<int> stiffnessIds = new List<int> { 2, 3, 4 };
        [Tooltip("振動条件の番号。初期値は1,2。全剛性との組み合わせを1セットにします。0も使用可能。")]
        [SerializeField] private List<int> vibrationIds = new List<int> { 1, 2 };
        [Tooltip("0なら実験開始時に自動生成します。0以外なら同じ順番を再現できます。")]
        [SerializeField] private int randomSeed;

        [Header("Runtime Status (Read Only)")]
        [SerializeField, HideInInspector] private ExperimentRunState state = ExperimentRunState.NotStarted;
        [SerializeField, HideInInspector] private int sessionSeed;
        [SerializeField, HideInInspector] private int currentSet;
        [SerializeField, HideInInspector] private int currentTrial;
        [SerializeField, HideInInspector] private int currentConditionId;
        [SerializeField, HideInInspector] private List<int> currentOrder = new List<int>();

        public event Action CurrentTrialChanged;
        private readonly List<List<int>> plannedOrders = new List<List<int>>();
        private List<ExperimentCondition> activeConditions = new List<ExperimentCondition>();

        public ExperimentRunState State => state;
        public int TotalSets => plannedOrders.Count > 0 ? plannedOrders.Count : totalSets;
        public int SessionSeed => sessionSeed;
        public int CurrentSet => currentSet;
        public int CurrentTrial => currentTrial;
        public int CurrentConditionId => currentConditionId;
        public int CurrentStiffnessId => GetConditionDefinition(currentConditionId).StiffnessId;
        public int CurrentVibrationId => GetConditionDefinition(currentConditionId).VibrationId;
        public IReadOnlyList<int> CurrentOrder => currentOrder;

        public ExperimentCondition GetConditionDefinition(int id)
        {
            return id >= 1 && id <= activeConditions.Count ? activeConditions[id - 1] : default;
        }

        private void OnEnable()
        {
            if (tcpBridge != null)
            {
                tcpBridge.NextRequested += HandleNextRequest;
            }
        }

        private void OnDisable()
        {
            if (tcpBridge != null)
            {
                tcpBridge.NextRequested -= HandleNextRequest;
            }
        }

        public void StartExperiment()
        {
            if (state != ExperimentRunState.NotStarted && state != ExperimentRunState.Finished)
            {
                Debug.LogWarning("[ExperimentSequence] Experiment is already running.", this);
                return;
            }

            if (!ValidateSettings())
            {
                return;
            }

            sessionSeed = randomSeed == 0 ? Environment.TickCount : randomSeed;
            activeConditions = ExperimentCondition.BuildCombinations(stiffnessIds, vibrationIds);
            plannedOrders.Clear();
            plannedOrders.AddRange(ConditionSchedule.Create(activeConditions, totalSets, sessionSeed));
            currentSet = 1;
            csvStore.BeginSession();
            // Prepare every answer target before presentation; later set starts
            // must reuse these rows instead of clearing answers entered in advance.
            for (int set = 1; set <= plannedOrders.Count; set++)
            {
                var conditions = new List<ExperimentCondition>();
                foreach (int id in plannedOrders[set - 1]) conditions.Add(GetConditionDefinition(id));
                csvStore.CreateSet(set, conditions);
            }
            BeginCurrentSet();
        }

        public void StartNextSet()
        {
            if (state != ExperimentRunState.WaitingForNextSet)
            {
                Debug.LogWarning("[ExperimentSequence] Next set can only start while waiting.", this);
                return;
            }

            if (currentSet >= TotalSets)
            {
                state = ExperimentRunState.Finished;
                return;
            }

            currentSet++;
            BeginCurrentSet();
        }

        public void NextTrial()
        {
            if (state != ExperimentRunState.Presenting)
            {
                return;
            }

            int currentIndex = currentTrial - 1;
            if (currentIndex + 1 < currentOrder.Count)
            {
                currentTrial++;
                ApplyCurrentCondition();
                return;
            }

            CompleteCurrentSet();
        }

        public void PreviousTrial()
        {
            if (state != ExperimentRunState.Presenting || currentTrial <= 1)
            {
                return;
            }

            currentTrial--;
            ApplyCurrentCondition();
        }

        public void JumpToTrial(int trialNumber)
        {
            if (state != ExperimentRunState.Presenting ||
                trialNumber < 1 || trialNumber > currentOrder.Count)
            {
                return;
            }

            currentTrial = trialNumber;
            ApplyCurrentCondition();
        }

        public void ReapplyCurrentCondition()
        {
            if (state == ExperimentRunState.Presenting)
            {
                ApplyCurrentCondition();
            }
        }

        public void ReopenLastTrial()
        {
            if ((state != ExperimentRunState.WaitingForNextSet &&
                 state != ExperimentRunState.Finished) ||
                currentOrder.Count == 0)
            {
                return;
            }

            state = ExperimentRunState.Presenting;
            currentTrial = currentOrder.Count;
            ApplyCurrentCondition();
        }

        private void BeginCurrentSet()
        {
            currentOrder = new List<int>(plannedOrders[currentSet - 1]);
            currentTrial = 1;
            state = ExperimentRunState.Presenting;

            ApplyCurrentCondition();

            Debug.Log(
                $"[ExperimentSequence] Set {currentSet} started. Order: {string.Join(", ", currentOrder)}",
                this);
        }

        private void ApplyCurrentCondition()
        {
            currentConditionId = currentOrder[currentTrial - 1];
            tcpBridge?.PublishCondition(CurrentStiffnessId, CurrentVibrationId);
            CurrentTrialChanged?.Invoke();

            Debug.Log(
                $"[ExperimentSequence] Set {currentSet}, Trial {currentTrial}, Condition {currentConditionId}, Stiffness {CurrentStiffnessId}, Vibration {CurrentVibrationId}",
                this);
        }

        private void CompleteCurrentSet()
        {
            currentConditionId = 0;
            tcpBridge?.PublishCondition(0, 0);
            CurrentTrialChanged?.Invoke();

            if (currentSet >= TotalSets)
            {
                state = ExperimentRunState.Finished;
                Debug.Log("[ExperimentSequence] Experiment finished.", this);
            }
            else
            {
                state = ExperimentRunState.WaitingForNextSet;
                Debug.Log(
                    $"[ExperimentSequence] Set {currentSet} finished. Start the next set from the Inspector.",
                    this);
            }
        }

        private void HandleNextRequest()
        {
            NextTrial();
        }

        private bool ValidateSettings()
        {
            if (csvStore == null)
            {
                Debug.LogError("[ExperimentSequence] SetCsvStore is not assigned.", this);
                return false;
            }

            try
            {
                ExperimentCondition.BuildCombinations(stiffnessIds, vibrationIds);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError("[ExperimentSequence] " + exception.Message, this);
                return false;
            }

            return true;
        }

        private void OnValidate()
        {
            totalSets = Mathf.Max(1, totalSets);
        }
    }
}
