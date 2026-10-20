using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>고전 휴대용 RPG 방식의 4방향 2프레임 필드 애니메이션.</summary>
    public class PixelFieldAnimator : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Sprite[] downFrames;
        [SerializeField] private Sprite[] upFrames;
        [SerializeField] private Sprite[] sideFrames;
        private void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            if (controller == null || target == null) return;
            Sprite[] frames = FramesFor(controller.Facing);
            if (frames == null || frames.Length == 0) return;
            int index = 0;
            if (controller.IsMoving && frames.Length > 1)
            {
                // 월드 시간 대신 실제 보폭에 묶어 저속 이동에서도 발이 미끄러지지 않게 한다.
                int phase = Mathf.Min(frames.Length - 1,
                    Mathf.FloorToInt(controller.StepProgress * frames.Length));
                index = (controller.CompletedSteps + phase) % frames.Length;
            }
            target.sprite = frames[index];
            FieldSpriteSizing.Fit(target);
            target.flipX = controller.Facing == FacingDir.Left;
        }

        private Sprite[] FramesFor(FacingDir direction)
        {
            switch (direction)
            {
                case FacingDir.Up: return upFrames;
                case FacingDir.Left:
                case FacingDir.Right: return sideFrames;
                default: return downFrames;
            }
        }
    }
}
