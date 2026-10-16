using System;
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    public class BossInvestigatorNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private int act;
        public void Interact(PlayerController player)
        {
            ShadowData boss = BossInvestigationService.NextBossForAct(act);
            if (boss == null) { SaveFeedbackController.Instance?.ShowMessage("이 생활권에서 더 조사할 보스 흔적은 없습니다."); return; }
            BossInvestigationService.DiscoverNext(boss, out string clue);
            SaveManager.Instance?.Save();
            SaveFeedbackController.Instance?.ShowMessage(clue);
        }
    }
}
