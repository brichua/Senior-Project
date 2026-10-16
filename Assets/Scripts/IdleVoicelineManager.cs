using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public sealed class IdleVoicelinePlayer : MonoBehaviour
    {
        private BoardUIController board;
        private GameplayBoardBridge bridge;
        //private VoicelinesManager voicelines;

        [Header("Idle Timing")]
        [Min(1f)] public float minInterval = 12f;
        [Min(1f)] public float maxInterval = 25f;

        private float idleTimer;

        private void Awake()
        {
            board = GetComponent<BoardUIController>();
            //bridge = board.game as GameplayBoardBridge;
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

            //if (bridge.IsPaused || bridge.PhaseAnimationPlaying || bridge.DrawAnimationPlaying) return;
            //if (voicelines.IsVoicelinePlaying()) return;

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
            var character = playerSpeaks ? board.setup.playerClass.identity : board.setup.enemyClass.identity;

            board.voicelines.PlayVoiceline(character, VoicelineEvent.Idle);
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