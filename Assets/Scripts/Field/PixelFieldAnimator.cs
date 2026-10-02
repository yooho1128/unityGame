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
        [SerializeField, Min(1f)] private float framesPerSecond = 6f;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (target == null) target = GetComponent<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            if (controller == null || target == null) return;
            Sprite[] frames = FramesFor(controller.Facing);
            if (frames == null || frames.Length == 0) return;
            int index = controller.IsMoving && frames.Length > 1
                ? Mathf.FloorToInt(Time.time * framesPerSecond) % frames.Length
                : 0;
            target.sprite = frames[index];
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
