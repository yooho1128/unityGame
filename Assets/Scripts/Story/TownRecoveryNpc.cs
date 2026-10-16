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
    public class TownRecoveryNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private string regionId;
        [SerializeField, Range(1, 3)] private int requiredStage = 2;
        public int RequiredStage => requiredStage;

        private IEnumerator Start()
        {
            if (SaveManager.Current == null) yield return new WaitUntil(() => SaveManager.Current != null);
            gameObject.SetActive(TownRestorationService.Stage(regionId) >= requiredStage);
        }

        public void Interact(PlayerController player)
        {
            int stage = TownRestorationService.Stage(regionId);
            string message = requiredStage >= 3
                ? "되돌아온 상인들이 희귀 물자를 진열했습니다. 이곳은 다시 사람들의 목적지가 되었습니다."
                : "복구 인력이 길과 무대를 고쳤습니다. 상점의 전문 물품도 늘어났습니다.";
            SaveFeedbackController.Instance?.ShowMessage($"[{TownRestorationService.StageLabel(stage)}] {message}");
        }
    }
}
