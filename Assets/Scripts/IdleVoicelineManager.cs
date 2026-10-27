using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public sealed class IdleVoicelinePlayer : MonoBehaviour
    {
        private BoardUIController board;
        private GameplayBoardBridge bridge;

        [Header("Idle Timing")]
        [Min(1f)] public float minInterval = 12f;
        [Min(1f)] public float maxInterval = 25f;

        private float idleTimer;

        private void Awake()
        {
            board = GetComponent<BoardUIController>();
            bridge = board ? board.game as GameplayBoardBridge : null;
            ResetTimer();
        }

        private void Update()
        {
            if (!board || board.State == null) return;

            if (board.State.phase != RoundPhase.Preparation)
            {
                ResetTimer();
                return;
            }

            if(!bridge) bridge=board.game as GameplayBoardBridge;
            if(!bridge || !board.voicelines || bridge.IsPaused || bridge.PhaseAnimationPlaying || bridge.DrawAnimationPlaying ||
                board.sfx && board.sfx.cardAudioSourceIsPlaying || board.voicelines.IsVoicelinePlaying()) return;

            idleTimer -= Time.unscaledDeltaTime;

            if (idleTimer <= 0f)
            {
                PlayRandomIdleVoiceline();
                ResetTimer();
            }
        }

        private void PlayRandomIdleVoiceline()
        {
            if (!board.setup) return;
            bool playerSpeaks = Random.value < 0.5f;
            board.PlayVoiceline(playerSpeaks,VoicelineEvent.Idle);
        }

        private void ResetTimer()
        {
            idleTimer = Random.Range(
                Mathf.Max(1f, minInterval),
                Mathf.Max(minInterval, maxInterval)
            );
        }
    }
}
