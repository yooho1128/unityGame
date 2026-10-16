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
    /// <summary>복구 단계에 맞춰 마을 건물·배경 장식·조명·주민 동선을 함께 바꾼다.</summary>
    public class TownRestorationVisualController : MonoBehaviour
    {
        [SerializeField] private string regionId;
        [SerializeField] private Color restoredLightColor = new Color(1f,.78f,.48f);
        [SerializeField] private List<GameObject> stageOneObjects = new List<GameObject>();
        [SerializeField] private List<GameObject> stageTwoObjects = new List<GameObject>();
        [SerializeField] private List<GameObject> stageThreeObjects = new List<GameObject>();
        private IEnumerator Start(){if(SaveManager.Current==null)yield return new WaitUntil(()=>SaveManager.Current!=null);Apply(TownRestorationService.Stage(regionId));}
        private void OnEnable()=>TownRestorationService.StageChanged+=OnStageChanged;
        private void OnDisable()=>TownRestorationService.StageChanged-=OnStageChanged;
        private void OnStageChanged(string id,int stage){if(id==regionId)Apply(stage);}
        public void Apply(int stage)
        {
            Set(stageOneObjects,stage>=1);Set(stageTwoObjects,stage>=2);Set(stageThreeObjects,stage>=3);
            foreach(Light2D light in GetComponentsInChildren<Light2D>(true))
            {if(light.lightType==Light2D.LightType.Global){light.intensity=Mathf.Lerp(.62f,1f,stage/3f);light.color=Color.Lerp(light.color,restoredLightColor,stage*.16f);}}
            foreach(TownNpcPatrol patrol in GetComponentsInChildren<TownNpcPatrol>(true))patrol.SetRestorationStage(stage);
            foreach(TownRecoveryNpc npc in GetComponentsInChildren<TownRecoveryNpc>(true))npc.gameObject.SetActive(stage>=npc.RequiredStage);
        }
        private static void Set(List<GameObject> values,bool active){foreach(GameObject value in values)if(value!=null)value.SetActive(active);}
    }
}
