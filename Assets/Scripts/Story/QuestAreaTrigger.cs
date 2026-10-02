using ShadowTheater.Field;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>특정 장소에 처음 진입했음을 Reach 목표에 전달한다. Trigger Collider2D에 붙인다.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class QuestAreaTrigger : MonoBehaviour
    {
        [SerializeField] private string areaId = "area_moonlit_meadow";
        [SerializeField] private bool notifyOncePerLoad = true;
        private bool _notified;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_notified && notifyOncePerLoad) return;
            if (!other.TryGetComponent<PlayerController>(out _)) return;

            if (QuestManager.Instance == null) return;
            _notified = true;
            QuestManager.Instance.Notify(QuestObjectiveType.Reach, areaId);
        }
    }
}
