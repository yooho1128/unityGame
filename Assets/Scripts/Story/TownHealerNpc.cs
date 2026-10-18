using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShadowTheater.Story
{
    public class TownHealerNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private string regionId;
        [SerializeField, Min(0)] private int restCost = 30;

        public void Interact(PlayerController player)
        {
            RegionData current = RegionRepository.GetByScene(SaveManager.Current?.mapId);
            if (current == null || current.regionId != regionId || !current.isSettlement) return;
            int cost = TownServicePolicy.RestCost(regionId, restCost);
            bool success = SaveManager.TryRestParty(cost, out string message);
            GameFeedbackAudio.Play(success ? FeedbackCue.Heal : FeedbackCue.Denied);
            if (success)
            {
                TownRestorationService.AddActivity(regionId, 1);
                if (!SaveManager.Instance.TrySave())
                    message += "\n" + L10n.Get("save.failed", "저장하지 못했습니다. 현재 게임을 유지하고 다시 저장해 주세요.");
            }
            SaveFeedbackController.Instance?.ShowMessage(message);
        }
    }
}
