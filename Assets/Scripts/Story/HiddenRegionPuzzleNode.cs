using ShadowTheater.Field;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>플레이어가 조사해 순서 퍼즐에 입력하는 봉화·발자국·공명석 노드.</summary>
    public class HiddenRegionPuzzleNode : MonoBehaviour, IInteractable
    {
        [SerializeField] private HiddenRegionSequencePuzzle controller;
        [SerializeField] private int nodeIndex;
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Color inactiveColor = new Color(.28f, .28f, .34f, 1f);
        [SerializeField] private Color activeColor = new Color(1f, .78f, .35f, 1f);

        public int NodeIndex => nodeIndex;

        public void Interact(PlayerController player)
        {
            controller?.Activate(nodeIndex);
        }

        public void SetLit(bool lit)
        {
            if (target != null) target.color = lit ? activeColor : inactiveColor;
        }
    }
}
