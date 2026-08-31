using Cave.InputSystem;
using UnityEngine;

namespace Cave.Player
{
    public enum PlayerAimOctant
    {
        Right,
        UpRight,
        Up,
        UpLeft,
        Left,
        DownLeft,
        Down,
        DownRight
    }

    [DisallowMultipleComponent]
    public sealed class PlayerAimDirection : MonoBehaviour
    {
        [Header("Current Aim (Read Only)")]
        [SerializeField] private PlayerAimOctant currentOctant = PlayerAimOctant.Right;
        [SerializeField] private Vector2 currentDirection = Vector2.right;
        [SerializeField] private float facingDirection = 1f;

        public PlayerAimOctant CurrentOctant => currentOctant;
        public Vector2 CurrentDirection => currentDirection;
        public Vector2 FacingDirection => facingDirection >= 0f ? Vector2.right : Vector2.left;

        private void Update()
        {
            RefreshDirection();
        }

        public Vector2 ReadDirection()
        {
            RefreshDirection();
            return currentDirection;
        }

        public void SetFacingDirection(float horizontalDirection)
        {
            if (Mathf.Abs(horizontalDirection) <= 0.001f)
            {
                return;
            }

            facingDirection = Mathf.Sign(horizontalDirection);
            currentDirection = FacingDirection;
            currentOctant = ToOctant(currentDirection);
        }

        private void RefreshDirection()
        {
            float horizontal = GameInput.Horizontal;
            if (!Mathf.Approximately(horizontal, 0f))
            {
                facingDirection = Mathf.Sign(horizontal);
            }

            float vertical = GameInput.AimVertical;
            if (Mathf.Approximately(horizontal, 0f) && Mathf.Approximately(vertical, 0f))
            {
                currentDirection = FacingDirection;
            }
            else
            {
                currentDirection = new Vector2(
                    Mathf.Sign(horizontal),
                    Mathf.Sign(vertical)).normalized;
            }

            currentOctant = ToOctant(currentDirection);
        }

        private static PlayerAimOctant ToOctant(Vector2 direction)
        {
            int horizontal = Mathf.RoundToInt(Mathf.Sign(direction.x));
            int vertical = Mathf.RoundToInt(Mathf.Sign(direction.y));
            if (horizontal > 0)
            {
                return vertical > 0
                    ? PlayerAimOctant.UpRight
                    : vertical < 0
                        ? PlayerAimOctant.DownRight
                        : PlayerAimOctant.Right;
            }

            if (horizontal < 0)
            {
                return vertical > 0
                    ? PlayerAimOctant.UpLeft
                    : vertical < 0
                        ? PlayerAimOctant.DownLeft
                        : PlayerAimOctant.Left;
            }

            return vertical < 0 ? PlayerAimOctant.Down : PlayerAimOctant.Up;
        }
    }
}
