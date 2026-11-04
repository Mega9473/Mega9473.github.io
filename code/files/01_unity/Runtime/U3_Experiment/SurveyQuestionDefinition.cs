using System;
using UnityEngine;

namespace Portfolio.Research
{
    [Serializable]
    public sealed class SurveyQuestionDefinition
    {
        [Tooltip("CSVの列名に使う識別子。半角英数字と_を推奨します。")]
        public string id = "Repulsion";

        [TextArea(2, 4)]
        public string prompt = "反発感をどの程度強く感じましたか？";

        public string leftLabel = "まったく感じない";
        public string middleLabel = "中程度";
        public string rightLabel = "非常に強く感じる";

        public SurveyQuestionDefinition Clone()
        {
            return new SurveyQuestionDefinition
            {
                id = id,
                prompt = prompt,
                leftLabel = leftLabel,
                middleLabel = middleLabel,
                rightLabel = rightLabel
            };
        }
    }
}
