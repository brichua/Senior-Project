using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public sealed partial class CardEffectTargetingUI
    {
        public RevealedCardsView revealPrefab;
        private float revealHoldSeconds = 3f, revealEnterSeconds = 0.18f, revealExitSeconds = 0.2f;

        private sealed class RevealPresentation
        {
            public string message;
            public string[] names;
            public Sprite[] art;
        }
        private readonly Queue<RevealPresentation> revealQueue = new Queue<RevealPresentation>();
        private int lastRevealVersion, revealConfiguration = -1;
        private RectTransform revealRoot, revealPanel;
        private CanvasGroup revealGroup;
        private float revealElapsed;
        private Vector2 revealHome;
        private Vector3 revealScale;

        private void ObserveReveal()
        {
            var state = board ? board.State : null;
            if (!bridge || state == null) return;
            if (revealConfiguration != bridge.ConfigurationVersion || state.revealVersion == 0)
            {
                ClearReveals();
                lastRevealVersion = 0;
                revealConfiguration = bridge.ConfigurationVersion;
            }
            if (state.revealVersion == lastRevealVersion) return;
            lastRevealVersion = state.revealVersion;
            var cards = state.revealedCards.Where(c => c != null && c.data).ToArray();
            if (cards.Length == 0) return;
            revealQueue.Enqueue(new RevealPresentation {
                message = state.revealMessage,
                names = cards.Select(c => c.data.cardName).ToArray(),
                art = cards.Select(c => c.data.cardImage).ToArray()
            });
        }

        private void TickReveal(float elapsed)
        {
            if (!revealRoot)
            {
                if (revealQueue.Count == 0 || board.State.pendingEffect != null ||
                    board.phaseAnimator && board.phaseAnimator.IsPlaying || board.drawAnimator && board.drawAnimator.IsPlaying) return;
                BuildReveal(revealQueue.Dequeue());
                if (!revealRoot) return;
            }
            revealElapsed += Mathf.Max(0, elapsed);
            float entrance = Mathf.Max(0.01f, revealEnterSeconds);
            float exit = Mathf.Max(0.01f, revealExitSeconds);
            float hold = Mathf.Max(0, revealHoldSeconds);
            if (revealElapsed >= entrance + hold + exit) { DestroyReveal(); return; }
            float appear = Mathf.SmoothStep(0, 1, Mathf.Clamp01(revealElapsed / entrance));
            float disappear = Mathf.Clamp01((revealElapsed - entrance - hold) / exit);
            revealGroup.alpha = appear * (1 - disappear);
            revealPanel.localScale = revealScale * Mathf.Lerp(0.92f, 1, appear);
            revealPanel.anchoredPosition = revealHome + Vector2.up * (Mathf.Lerp(-24, 0, appear) + 20 * disappear);
        }

        private void BuildReveal(RevealPresentation presentation)
        {
            var prefab = revealPrefab;
            if (!prefab || !prefab.panel || !prefab.cardsRoot || !prefab.cardTemplate || !prefab.group)
            {
                Debug.LogError("Assign a complete RevealedCardsView prefab on BoardUIController.", this);
                return;
            }
            var view = Instantiate(prefab, transform);
            view.Bind(presentation.message, presentation.art, presentation.names);
            revealRoot = (RectTransform)view.transform; revealPanel = view.panel; revealGroup = view.group;
            revealGroup.alpha = 0;
            revealHome = revealPanel.anchoredPosition; revealScale = revealPanel.localScale;
            revealHoldSeconds = view.visibleSeconds; revealEnterSeconds = view.enterSeconds; revealExitSeconds = view.exitSeconds;
            view.gameObject.SetActive(true);
            revealElapsed = 0;
        }

        private void DestroyReveal()
        {
            if (revealRoot)
            {
                revealRoot.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(revealRoot.gameObject); else DestroyImmediate(revealRoot.gameObject);
            }
            revealRoot = revealPanel = null; revealGroup = null;
        }

        private void ClearReveals() { revealQueue.Clear(); DestroyReveal(); }
    }
}
