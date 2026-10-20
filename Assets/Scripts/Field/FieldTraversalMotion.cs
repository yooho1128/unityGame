using UnityEngine;

namespace ShadowTheater.Field
{
    public readonly struct TraversalMotionProfile
    {
        public readonly float durationMultiplier;
        public readonly float arcMultiplier;
        public readonly float overshoot;
        public readonly bool allowed;
        public TraversalMotionProfile(float durationMultiplier, float arcMultiplier, float overshoot, bool allowed = true)
        { this.durationMultiplier=durationMultiplier; this.arcMultiplier=arcMultiplier; this.overshoot=overshoot; this.allowed=allowed; }
    }

    /// <summary>타일 이름과 입력 흐름을 달리기·진흙·얼음·바람·사다리·낮은 턱의 이동 감각으로 바꾼다.</summary>
    public static class FieldTraversalMotion
    {
        public static TraversalMotionProfile Profile(string tileName, Vector2Int direction, int heldSteps)
        {
            string value = (tileName ?? string.Empty).ToLowerInvariant();
            float dash = heldSteps >= 4 ? .78f : heldSteps >= 2 ? .89f : 1f;
            if (value.Contains("mud") || value.Contains("bog")) return new TraversalMotionProfile(1.5f, .45f, 0f);
            if (value.Contains("ice") || value.Contains("frost")) return new TraversalMotionProfile(.72f, .25f, .075f);
            if (value.Contains("ladder")) return new TraversalMotionProfile(1.22f, .12f, 0f, direction.x == 0);
            if (value.Contains("ledge")) return new TraversalMotionProfile(.82f, 1.55f, 0f, direction.y < 0);
            if (value.Contains("wind"))
            {
                Vector2Int wind = value.Contains("left") ? Vector2Int.left : value.Contains("right") ? Vector2Int.right :
                    value.Contains("down") ? Vector2Int.down : Vector2Int.up;
                float alignment = Vector2.Dot(direction, wind);
                return new TraversalMotionProfile(alignment > .5f ? .76f : alignment < -.5f ? 1.42f : 1.12f, .65f, 0f);
            }
            return new TraversalMotionProfile(dash, 1f, 0f);
        }
    }
}
