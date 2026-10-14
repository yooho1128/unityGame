using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>숨겨진 지역의 짧은 순서 퍼즐. 진행도는 임시지만 해결 여부는 세이브에 영구 보존한다.</summary>
    public class HiddenRegionSequencePuzzle : MonoBehaviour
    {
        [SerializeField] private string puzzleId;
        [SerializeField] private int[] sequence = { 0, 1, 2 };
        [SerializeField] private HiddenRegionPuzzleNode[] nodes;
        [SerializeField] private GameObject rewardRoot;
        [SerializeField] private string completionDialogueId;
        [SerializeField] private Color accent = new Color(.75f, .55f, .32f, 1f);

        public string CompletionFlag => "hidden_puzzle_" + puzzleId + "_complete";
        public bool IsSolved { get; private set; }

        private int _step;

        private void Start()
        {
            IsSolved = !string.IsNullOrEmpty(puzzleId) && SaveManager.HasFlag(CompletionFlag);
            if (rewardRoot != null) rewardRoot.SetActive(IsSolved);
            RefreshNodes();
        }

        public void Activate(int nodeIndex)
        {
            if (IsSolved || sequence == null || sequence.Length == 0) return;

            if (nodeIndex == sequence[_step])
            {
                _step++;
                RefreshNodes();
                if (_step >= sequence.Length) Complete();
                else SaveFeedbackController.Instance?.ShowMessage($"울림이 이어졌다 · {_step}/{sequence.Length}");
                return;
            }

            // 틀렸더라도 첫 장치부터 다시 누른 경우 입력을 살려 불필요한 반복을 줄인다.
            _step = nodeIndex == sequence[0] ? 1 : 0;
            RefreshNodes();
            SaveFeedbackController.Instance?.ShowMessage(_step == 1
                ? $"첫 울림부터 다시 잇는다 · 1/{sequence.Length}"
                : "울림이 흩어졌다 · 단서의 첫 순서부터 다시");
        }

        private void Complete()
        {
            IsSolved = true;
            SaveManager.SetFlag(CompletionFlag);
            if (rewardRoot != null) rewardRoot.SetActive(true);
            RefreshNodes();
            if (SaveManager.Instance != null && SaveManager.Current != null) SaveManager.Instance.Save();

            DialogueController dialogue = DialogueController.Instance;
            if (dialogue != null && !dialogue.IsPlaying && !string.IsNullOrEmpty(completionDialogueId))
                dialogue.Play(completionDialogueId, accent);
            else
                SaveFeedbackController.Instance?.ShowMessage("숨겨진 기억의 길이 열렸다");
        }

        private void RefreshNodes()
        {
            if (nodes == null) return;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == null) continue;
                bool lit = IsSolved;
                if (!lit)
                    for (int step = 0; step < _step && step < sequence.Length; step++)
                        if (sequence[step] == nodes[i].NodeIndex) { lit = true; break; }
                nodes[i].SetLit(lit);
            }
        }
    }
}
