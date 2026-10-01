using System.Collections.Generic;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.Battle
{
    /// <summary>
    /// UI 없이 전투 로직을 바로 돌려보는 테스트용 컴포넌트.
    /// 빈 씬에 BattleManager와 함께 붙이고, 인스펙터에 ShadowData를 넣은 뒤 Play.
    /// 좌상단 OnGUI 버튼으로 행동 선택, 결과는 Console에 출력.
    /// </summary>
    public class BattleTestBootstrap : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private List<ShadowData> playerShadows = new List<ShadowData>();
        [SerializeField] private int playerLevel = 5;
        [SerializeField] private ShadowData enemyShadow;
        [SerializeField] private int enemyLevel = 4;
        [SerializeField] private BattleMode mode = BattleMode.Wild;
        [SerializeField] private ItemData testPotion;

        private void Start()
        {
            if (battleManager == null) battleManager = BattleManager.Instance;
            battleManager.OnBattleEnded += o => Debug.Log($"[Test] 전투 종료: {o.result}, 포획={o.capturedShadow?.shadowId}");
            StartTest();
        }

        private void StartTest()
        {
            var party = new List<ShadowInstance>();
            foreach (var d in playerShadows) if (d != null) party.Add(new ShadowInstance(d, playerLevel));

            var inventory = new Dictionary<ItemData, int>();
            if (testPotion != null) inventory[testPotion] = 3;

            battleManager.StartBattle(new BattleContext
            {
                mode = mode,
                playerParty = party,
                enemyParty = new List<ShadowInstance> { new ShadowInstance(enemyShadow, enemyLevel) },
                inventory = inventory,
                encounterId = "test_encounter"
            });
        }

        private void OnGUI()
        {
            var bm = battleManager;
            if (bm == null || bm.Context == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 320, 600), GUI.skin.box);
            GUILayout.Label($"Turn {bm.Turn}  |  State: {bm.State}  |  Auto: {bm.IsAuto}");
            GUILayout.Label($"FP  나 {bm.GetFp(BattleSide.Player)}/{bm.MaxFp}   적 {bm.GetFp(BattleSide.Enemy)}/{bm.MaxFp}");
            GUILayout.Label($"[나] {bm.PlayerActive.Name} Lv.{bm.PlayerActive.Level}  HP {bm.PlayerActive.Hp}/{bm.PlayerActive.MaxHp}  {bm.PlayerActive.MajorStatus?.type}");
            GUILayout.Label($"[적] {bm.EnemyActive.Name} Lv.{bm.EnemyActive.Level}  HP {bm.EnemyActive.Hp}/{bm.EnemyActive.MaxHp}  {bm.EnemyActive.MajorStatus?.type}");

            if (bm.State == BattleState.WaitingForInput)
            {
                if (GUILayout.Button("공격")) bm.SubmitAction(BattleAction.Attack(BattleSide.Player, null));
                foreach (var s in bm.PlayerActive.Data.skills)
                {
                    GUI.enabled = bm.CanUseSkill(s);
                    if (GUILayout.Button($"스킬: {s.displayName} (FP {s.fpCost})"))
                        bm.SubmitAction(BattleAction.UseSkill(BattleSide.Player, s));
                    GUI.enabled = true;
                }
                if (bm.Context.CanCapture && GUILayout.Button("각본 기록")) bm.SubmitAction(BattleAction.Record());
                if (testPotion != null && GUILayout.Button($"도구: {testPotion.displayName}"))
                    bm.SubmitAction(BattleAction.UseItem(BattleSide.Player, testPotion));
                if (bm.Context.CanEscape && GUILayout.Button("도주")) bm.SubmitAction(BattleAction.Escape());
            }

            if (bm.State == BattleState.WaitingForInput || bm.State == BattleState.ForcedSwitch)
            {
                for (int i = 0; i < bm.PlayerUnits.Count; i++)
                {
                    if (!bm.CanSwitchTo(i)) continue;
                    if (GUILayout.Button($"교체 → {bm.PlayerUnits[i].Name}"))
                        bm.SubmitAction(BattleAction.Switch(BattleSide.Player, i));
                }
            }

            if (GUILayout.Button(bm.IsAuto ? "Auto OFF" : "Auto ON")) bm.SetAuto(!bm.IsAuto);
            if (bm.IsEnded && GUILayout.Button("다시 시작")) StartTest();
            GUILayout.EndArea();
        }
    }
}
