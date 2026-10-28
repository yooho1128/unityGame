#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ShadowTheater.Data;
using ShadowTheater.Battle;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    /// <summary>191종을 같은 성장 등급끼리 AI 대전시켜 승률 편차를 찾고 소폭 보정한다.</summary>
    public static class ShadowBattleSimulator
    {
        private const string DatabasePath = "Assets/Resources/ShadowDatabase.asset";
        private const string ReportPath = "Docs/Generated/SHADOW_AI_WIN_RATES.tsv";
        private const string PartyReportPath = "Docs/Generated/SHADOW_PARTY_WIN_RATES.tsv";
        private const int SimulationLevel = 50;
        private const int TrialsPerPair = 8;
        private const float MaxCorrection = .03f;

        [MenuItem("Tools/Shadow Theater/Balance/Simulate 191 Shadow Win Rates")]
        public static void Analyze() => Run(false);

        [MenuItem("Tools/Shadow Theater/Balance/Simulate 3-6 Member Party Combinations")]
        public static void AnalyzeParties()
        {
            ShadowDatabase database=AssetDatabase.LoadAssetAtPath<ShadowDatabase>(DatabasePath);List<ShadowData> shadows=database?.shadows?.Where(x=>x!=null).ToList();
            if(shadows==null||shadows.Count<12){Debug.LogError("[PartySimulation] 시뮬레이션할 그림자 데이터가 부족합니다.");return;}
            var results=shadows.ToDictionary(x=>x.shadowId,x=>new PartyResult(x),StringComparer.Ordinal);var compositions=new Dictionary<string,CompositionResult>(StringComparer.Ordinal);int battles=0;
            foreach(int size in Enumerable.Range(3,4))for(int sample=0;sample<240;sample++)
            {
                var random=new System.Random(StableHash("party|"+size+"|"+sample));ShadowData[] shuffled=shadows.OrderBy(_=>random.Next()).ToArray();
                ShadowData[] a=BuildBalancedParty(shuffled.Take(size*3),size,random);ShadowData[] b=BuildBalancedParty(shuffled.Skip(size*3).Take(size*3),size,random);if(a.Length!=size||b.Length!=size)continue;
                int outcome=PartyBattle(a,b,random.Next());RecordParty(results,a,outcome>=0,outcome==0);RecordParty(results,b,outcome<=0,outcome==0);RecordComposition(compositions,a,outcome>=0,outcome==0);RecordComposition(compositions,b,outcome<=0,outcome==0);battles++;
            }
            var lines=new List<string>{"구분\tID/조합\t이름/구성\t인원\t경기\t승\t무\t승률\t판정"};
            foreach(var r in results.Values.OrderBy(x=>x.shadow.shadowId))lines.Add($"그림자\t{Clean(r.shadow.shadowId)}\t{Clean(r.shadow.displayName)}\t-\t{r.games}\t{r.wins}\t{r.draws}\t{r.WinRate*100f:0.0}%\t{PartyVerdict(r.WinRate,r.games)}");
            foreach(var pair in compositions.OrderByDescending(x=>x.Value.games).ThenBy(x=>x.Key).Take(240)){var r=pair.Value;lines.Add($"조합\t{Clean(pair.Key)}\t{Clean(r.names)}\t{r.size}\t{r.games}\t{r.wins}\t{r.draws}\t{r.WinRate*100f:0.0}%\t{PartyVerdict(r.WinRate,r.games)}");}
            Directory.CreateDirectory(Path.GetDirectoryName(PartyReportPath));File.WriteAllLines(PartyReportPath,lines,new UTF8Encoding(true));AssetDatabase.Refresh();Debug.Log($"[PartySimulation] 3~6인 {battles}경기 · {PartyReportPath}");
        }

        [MenuItem("Tools/Shadow Theater/Balance/Apply Conservative Win Rate Correction")]
        public static void ApplyCorrection()
        {
            if (!EditorUtility.DisplayDialog("승률 기반 자동 보정",
                "같은 성장 등급 내 승률이 42% 미만 또는 58% 초과인 그림자의 능력치를 한 번에 최대 3% 보정합니다.",
                "시뮬레이션 후 보정", "취소")) return;
            Run(true);
        }

        private static void Run(bool apply)
        {
            ShadowDatabase database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>(DatabasePath);
            List<ShadowData> shadows = database?.shadows?.Where(x => x != null).ToList();
            if (shadows == null || shadows.Count == 0)
            { Debug.LogError("[AiSimulation] 먼저 Generate and Validate Full Game을 실행하세요."); return; }
            if (shadows.Count != 191)
                Debug.LogWarning($"[AiSimulation] 예상 로스터 191종과 다릅니다: {shadows.Count}종");

            var results = shadows.ToDictionary(x => x.shadowId, x => new Result(x), StringComparer.Ordinal);
            foreach (IGrouping<GrowthTier, ShadowData> group in shadows.GroupBy(x => x.growthTier))
            {
                ShadowData[] entries = group.OrderBy(x => x.shadowId, StringComparer.Ordinal).ToArray();
                for (int a = 0; a < entries.Length; a++)
                for (int b = a + 1; b < entries.Length; b++)
                for (int trial = 0; trial < TrialsPerPair; trial++)
                {
                    int seed = StableHash(entries[a].shadowId + "|" + entries[b].shadowId + "|" + trial);
                    bool swap = (trial & 1) == 1;
                    int outcome = Duel(swap ? entries[b] : entries[a], swap ? entries[a] : entries[b], seed);
                    Result first = results[swap ? entries[b].shadowId : entries[a].shadowId];
                    Result second = results[swap ? entries[a].shadowId : entries[b].shadowId];
                    first.games++; second.games++;
                    if (outcome > 0) first.wins++; else if (outcome < 0) second.wins++;
                    else { first.draws++; second.draws++; }
                }
            }

            var lines = new List<string> { "ID\t이름\t등급\t경기\t승\t무\t승률\t판정\t보정" };
            int changed = 0;
            foreach (Result result in results.Values.OrderBy(x => x.shadow.growthTier).ThenBy(x => x.shadow.shadowId))
            {
                float rate = result.WinRate;
                string verdict = result.games == 0 ? "표본 없음" : rate < .42f ? "약세" : rate > .58f ? "강세" : "안정";
                float correction = apply ? CorrectionFor(rate, result.games) : 1f;
                if (apply && Math.Abs(correction - 1f) > .0001f)
                {
                    Tune(result.shadow, correction);
                    changed++;
                }
                lines.Add(string.Join("\t", new[] { Clean(result.shadow.shadowId), Clean(result.shadow.displayName),
                    result.shadow.growthTier.ToString(), result.games.ToString(), result.wins.ToString(), result.draws.ToString(),
                    (rate * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%", verdict,
                    correction.ToString("0.000", CultureInfo.InvariantCulture) }));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllLines(ReportPath, lines, new UTF8Encoding(true));
            if (apply) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
            Debug.Log($"[AiSimulation] {shadows.Count}종 · {results.Values.Sum(x => x.games) / 2}경기 · " +
                      $"{(apply ? $"보정 {changed}종" : "분석 전용")} · {ReportPath}");
        }

        /// <returns>1=선공 승, -1=후공 승, 0=무승부</returns>
        public static int Duel(ShadowData first, ShadowData second, int seed)
        {
            if (first == null || second == null) return 0;
            var random = new System.Random(seed);
            SimUnit a = new SimUnit(first, SimulationLevel), b = new SimUnit(second, SimulationLevel);
            for (int turn = 1; turn <= 80 && a.hp > 0 && b.hp > 0; turn++)
            {
                a.fp = Math.Min(5, a.fp + 1); b.fp = Math.Min(5, b.fp + 1);
                SimUnit lead = a.speed > b.speed || a.speed == b.speed && random.NextDouble() < .5 ? a : b;
                SimUnit follow = lead == a ? b : a;
                Act(lead, follow, random, turn);
                if (follow.hp > 0) Act(follow, lead, random, turn);
            }
            if (a.hp == b.hp) return 0;
            if (a.hp <= 0) return -1;
            if (b.hp <= 0) return 1;
            float ar = a.hp / (float)a.maxHp, br = b.hp / (float)b.maxHp;
            return Math.Abs(ar - br) < .025f ? 0 : ar > br ? 1 : -1;
        }

        public static float CorrectionFor(float winRate, int games)
        {
            if (games < 8 || winRate >= .42f && winRate <= .58f) return 1f;
            return Mathf.Clamp(1f + (.5f - winRate) * .18f, 1f - MaxCorrection, 1f + MaxCorrection);
        }

        public static void ValidatePolicyRegression(List<string> errors)
        {
            if (CorrectionFor(.5f, 100) != 1f || CorrectionFor(.2f, 100) <= 1f || CorrectionFor(.8f, 100) >= 1f ||
                CorrectionFor(.1f, 100) > 1f + MaxCorrection + .0001f)
                errors.Add("191종 AI 시뮬레이션: 승률 보정 정책 오류");
            if(BuildBalancedParty(new ShadowData[0],3,new System.Random(1)).Length!=0)
                errors.Add("파티 AI 시뮬레이션: 빈 로스터 처리 오류");
            var roles=(ShadowRole[])Enum.GetValues(typeof(ShadowRole));var samples=new List<ShadowData>();for(int i=0;i<6;i++){var sample=ScriptableObject.CreateInstance<ShadowData>();sample.shadowId="party_policy_"+i;sample.role=roles[i%roles.Length];samples.Add(sample);}
            if(BuildBalancedParty(samples,6,new System.Random(2)).Length!=6)errors.Add("파티 AI 시뮬레이션: 6인 구성 정책 오류");
            foreach(var sample in samples)UnityEngine.Object.DestroyImmediate(sample);
        }

        private static ShadowData[] BuildBalancedParty(IEnumerable<ShadowData> pool,int size,System.Random random)
        {var source=pool.Where(x=>x!=null).Distinct().ToList();var party=new List<ShadowData>();foreach(ShadowRole role in Enum.GetValues(typeof(ShadowRole))){var candidate=source.Where(x=>x.role==role&&!party.Contains(x)).OrderBy(_=>random.Next()).FirstOrDefault();if(candidate!=null&&party.Count<size)party.Add(candidate);}foreach(var candidate in source.OrderBy(_=>random.Next()))if(party.Count<size&&!party.Contains(candidate))party.Add(candidate);return party.Take(size).ToArray();}
        private static int PartyBattle(ShadowData[] first,ShadowData[] second,int seed)
        {var random=new System.Random(seed);var a=first.Select(x=>new SimUnit(x,SimulationLevel)).ToList();var b=second.Select(x=>new SimUnit(x,SimulationLevel)).ToList();int ai=0,bi=0;for(int turn=0;turn<360&&ai<a.Count&&bi<b.Count;turn++){SimUnit au=a[ai],bu=b[bi];au.fp=Math.Min(5,au.fp+1);bu.fp=Math.Min(5,bu.fp+1);SimUnit lead=au.speed>bu.speed||au.speed==bu.speed&&random.NextDouble()<.5?au:bu;SimUnit follow=lead==au?bu:au;Act(lead,follow,random,turn+1);if(follow.hp>0)Act(follow,lead,random,turn+1);if(au.hp<=0)ai++;if(bu.hp<=0)bi++;}if(ai>=a.Count&&bi>=b.Count)return 0;if(ai>=a.Count)return -1;if(bi>=b.Count)return 1;float ah=a.Skip(ai).Sum(x=>Math.Max(0,x.hp)/(float)x.maxHp),bh=b.Skip(bi).Sum(x=>Math.Max(0,x.hp)/(float)x.maxHp);return Math.Abs(ah-bh)<.05f?0:ah>bh?1:-1;}
        private static void RecordParty(Dictionary<string,PartyResult> results,IEnumerable<ShadowData> party,bool won,bool draw){foreach(var shadow in party){var r=results[shadow.shadowId];r.games++;if(draw)r.draws++;else if(won)r.wins++;}}
        private static void RecordComposition(Dictionary<string,CompositionResult> results,ShadowData[] party,bool won,bool draw){string roles=string.Join("+",party.Select(x=>x.role.ToString()).OrderBy(x=>x));string elements=string.Join("+",party.Select(x=>x.element.ToString()).OrderBy(x=>x));string key=$"{party.Length}|{roles}|{elements}";if(!results.TryGetValue(key,out var r)){r=new CompositionResult{size=party.Length,names=$"역할 {roles} / 속성 {elements}"};results.Add(key,r);}r.games++;if(draw)r.draws++;else if(won)r.wins++;}
        private static string PartyVerdict(float rate,int games)=>games<3?"표본 부족":rate<.42f?"조합 약세":rate>.58f?"조합 강세":"안정";

        private static void Act(SimUnit actor, SimUnit target, System.Random random, int turn)
        {
            SkillData skill = Choose(actor, target, turn);
            if (skill == null) { target.hp = Math.Max(0, target.hp - 1); return; }
            actor.fp = Math.Max(0, actor.fp + skill.fpGain - skill.fpCost);
            if (skill.target == SkillTarget.Self && skill.healRatio > 0f)
            {
                if (actor.healing.TryConsume(turn))
                    actor.hp = Math.Min(actor.maxHp, actor.hp + Math.Max(1, Mathf.RoundToInt(actor.maxHp * skill.healRatio)));
                return;
            }
            if (!skill.DealsDamage || random.NextDouble() > skill.accuracy * (1f - target.evasion)) return;
            float defense = target.defense * (skill.damageType == DamageType.Magical ? .5f : 1f);
            float element = ElementChart.GetMultiplier(skill.ResolveElement(actor.data.element),
                target.data.element);
            float raw = actor.attack * skill.damageMultiplier * (100f / (100f + defense)) * element;
            if (random.NextDouble() < Mathf.Clamp01(actor.crit + skill.bonusCritRate)) raw *= 1.5f;
            target.hp = Math.Max(0, target.hp - Math.Max(1, Mathf.RoundToInt(raw * (.9f + (float)random.NextDouble() * .1f))));
        }

        private static SkillData Choose(SimUnit actor, SimUnit target, int turn)
        {
            IEnumerable<SkillData> skills = (actor.data.skills ?? new List<SkillData>())
                .Where(x => x != null && x.requiredLevel <= SimulationLevel && x.fpCost <= actor.fp &&
                    (!BattleUnit.IsHealingSkill(x) || actor.healing.CanUse(turn)));
            SkillData best = actor.data.basicAttack;
            float bestScore = Score(actor, target, best);
            foreach (SkillData skill in skills)
            {
                float score = Score(actor, target, skill);
                if (score > bestScore) { best = skill; bestScore = score; }
            }
            return best;
        }

        private static float Score(SimUnit actor, SimUnit target, SkillData skill)
        {
            if (skill == null) return 0f;
            if (skill.target == SkillTarget.Self) return actor.hp < actor.maxHp * .45f ? skill.healRatio * actor.maxHp * 1.25f : 0f;
            float defense = target.defense * (skill.damageType == DamageType.Magical ? .5f : 1f);
            ShadowElement element = skill.ResolveElement(actor.data.element);
            return actor.attack * skill.damageMultiplier * 100f / (100f + defense) *
                   ElementChart.GetMultiplier(element, target.data.element) * skill.accuracy + skill.statusChance * 8f;
        }

        private static void Tune(ShadowData shadow, float factor)
        {
            Undo.RecordObject(shadow, "191종 승률 기반 보정");
            shadow.baseHp = Math.Max(1, Mathf.RoundToInt(shadow.baseHp * factor));
            shadow.baseAtk = Math.Max(1, Mathf.RoundToInt(shadow.baseAtk * factor));
            shadow.baseDef = Math.Max(0, Mathf.RoundToInt(shadow.baseDef * factor));
            shadow.hpGrowth = Mathf.Max(.1f, shadow.hpGrowth * factor);
            shadow.atkGrowth = Mathf.Max(.05f, shadow.atkGrowth * factor);
            shadow.defGrowth = Mathf.Max(.05f, shadow.defGrowth * factor);
            EditorUtility.SetDirty(shadow);
        }

        private static int StableHash(string value) { unchecked { int hash = 23; foreach (char c in value ?? "") hash = hash * 31 + c; return hash; } }
        private static string Clean(string value) => (value ?? string.Empty).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");

        private sealed class Result
        {
            public readonly ShadowData shadow; public int games, wins, draws;
            public float WinRate => games <= 0 ? .5f : (wins + draws * .5f) / games;
            public Result(ShadowData shadow) { this.shadow = shadow; }
        }
        private sealed class PartyResult{public readonly ShadowData shadow;public int games,wins,draws;public float WinRate=>games==0?.5f:(wins+draws*.5f)/games;public PartyResult(ShadowData shadow){this.shadow=shadow;}}
        private sealed class CompositionResult{public int size,games,wins,draws;public string names;public float WinRate=>games==0?.5f:(wins+draws*.5f)/games;}
        private sealed class SimUnit
        {
            public readonly HealingSkillBudget healing = new HealingSkillBudget();
            public readonly ShadowData data; public readonly int maxHp, attack, defense, speed; public readonly float crit, evasion;
            public int hp, fp;
            public SimUnit(ShadowData data, int level)
            { this.data = data; maxHp = hp = data.GetHp(level); attack = data.GetAtk(level); defense = data.GetDef(level);
              speed = data.GetSpd(level); crit = data.critRate; evasion = data.evasion; }
        }
    }
}
#endif
