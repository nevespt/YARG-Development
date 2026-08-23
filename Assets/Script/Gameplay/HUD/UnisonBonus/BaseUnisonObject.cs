using System;
using System.Collections.Generic;
using DG.Tweening;
using YARG.Core;

namespace YARG.Gameplay.HUD
{
    public abstract class BaseUnisonObject : GameplayBehaviour
    {
        protected bool[] ParticipantFailState;
        protected int[]  ParticipantTotalNotes;
        protected int[]  ParticipantNotesHit;
        protected int    ParticipantCount;

        protected float ParticipantProgress(int engineId)
        {
            if (ParticipantTotalNotes == null || engineId < 0 || engineId >= ParticipantTotalNotes.Length || ParticipantTotalNotes[engineId] == 0)
            {
                return 0f;
            }
            return YargMath.InverseLerpF(0f, ParticipantTotalNotes[engineId], ParticipantNotesHit[engineId]);
        }

        protected void EnsureCapacity(int size)
        {
            if (ParticipantTotalNotes == null || ParticipantTotalNotes.Length < size)
            {
                int newSize = Math.Max(size, Math.Max(4, (ParticipantTotalNotes?.Length ?? 0) * 2));
                Array.Resize(ref ParticipantFailState, newSize);
                Array.Resize(ref ParticipantTotalNotes, newSize);
                Array.Resize(ref ParticipantNotesHit, newSize);
            }
        }

        public void Initialize(int playerCount)
        {
            int size = Math.Max(playerCount, 4);
            ParticipantFailState = new bool[size];
            ParticipantTotalNotes = new int[size];
            ParticipantNotesHit = new int[size];
            ParticipantCount = 0;
        }

        public virtual void ResetState()
        {
            if (ParticipantFailState != null)
            {
                int len = Math.Clamp(ParticipantCount, 0, ParticipantFailState.Length);
                Array.Clear(ParticipantFailState, 0, len);
                Array.Clear(ParticipantTotalNotes, 0, len);
                Array.Clear(ParticipantNotesHit, 0, len);
            }
            ParticipantCount = 0;
        }

        public virtual void AddParticipant(int participantId, int totalNotes)
        {
            EnsureCapacity(participantId + 1);
            ParticipantTotalNotes[participantId] = totalNotes;
            ParticipantNotesHit[participantId] = 0;
            ParticipantFailState[participantId] = false;
            ParticipantCount++;
        }

        public virtual void SetNotesHit(int engineId, int notesHit)
        {
            EnsureCapacity(engineId + 1);
            if (!ParticipantFailState[engineId])
            {
                ParticipantNotesHit[engineId] = notesHit;
            }
        }

        public virtual void FailUnison(int engineId)
        {
            EnsureCapacity(engineId + 1);
            ParticipantFailState[engineId] = true;
        }
    }
}