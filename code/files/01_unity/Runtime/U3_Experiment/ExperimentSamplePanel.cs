using UnityEngine;

namespace Portfolio.Research
{
    /// <summary>新規の接続例。元のSteamVR質問紙画面の再現ではありません。</summary>
    public sealed class ExperimentSamplePanel : MonoBehaviour
    {
        [SerializeField] private ExperimentSequence sequence;
        [SerializeField] private SetCsvStore csv;
        private int[] scores;
        private int displayedSet = -1, displayedTrial = -1;
        private string status = "";

        private void OnGUI()
        {
            if (sequence == null || csv == null) return;
            GUILayout.BeginArea(new Rect(15, 15, 520, 650), GUI.skin.box);
            GUILayout.Label("研究実験コードの接続サンプル / mouse + keyboard");
            GUILayout.Label("State: " + sequence.State + "  Set: " + sequence.CurrentSet + " Trial: " + sequence.CurrentTrial);
            if (GUILayout.Button("Start experiment")) sequence.StartExperiment();
            if (GUILayout.Button("Start next set")) sequence.StartNextSet();
            if (sequence.State == ExperimentRunState.Presenting)
            {
                if (scores == null || displayedSet != sequence.CurrentSet || displayedTrial != sequence.CurrentTrial)
                {
                    displayedSet = sequence.CurrentSet;
                    displayedTrial = sequence.CurrentTrial;
                    if (!csv.TryGetScores(displayedSet, displayedTrial, out scores))
                        scores = new int[csv.Questions.Count];
                    for (int i = 0; i < scores.Length; i++) if (scores[i] == 0) scores[i] = 4;
                }
                for (int i = 0; i < scores.Length; i++)
                {
                    GUILayout.Label(csv.Questions[i].prompt);
                    scores[i] = GUILayout.SelectionGrid(scores[i] - 1, new[] { "1", "2", "3", "4", "5", "6", "7" }, 7) + 1;
                }
                if (GUILayout.Button("Save answers"))
                    status = csv.SaveAnswers(displayedSet, displayedTrial, scores, out string error) ? "Saved." : error;
                if (GUILayout.Button("Previous trial")) sequence.PreviousTrial();
                if (GUILayout.Button("Next trial")) sequence.NextTrial();
            }
            GUILayout.Label(status);
            GUILayout.EndArea();
        }
    }
}
