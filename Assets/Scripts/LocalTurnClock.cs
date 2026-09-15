using System;
using UnityEngine;

namespace VocaloidTCG.BoardUI
{
    public sealed class LocalTurnClock : MonoBehaviour
    {
        public event Action Expired;
        public float RemainingSeconds { get; private set; }
        public bool Paused { get; set; }
        private bool running;

        public void BeginTurn(float seconds = 60){
            RemainingSeconds = Mathf.Max(0, seconds); running = true;
        }

        public void Stop(){
            running = false;
        }

        private void Update(){
            if(!running || Paused) return;
            RemainingSeconds = Mathf.Max(0, RemainingSeconds - Time.unscaledDeltaTime);
            if(RemainingSeconds > 0) return;
            running = false;
            if(Expired != null) Expired();
        }
    }
}
