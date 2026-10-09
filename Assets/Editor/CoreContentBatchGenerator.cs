#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowTheater.Data;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    /// <summary>초반 플레이에 필요한 스킬·아이템·그림자를 JSON에서 반복 생성한다.</summary>
    public static class CoreContentBatchGenerator
    {
        private const string CatalogPath = "Assets/Resources/Data/CoreContentCatalog.json";
        private static readonly string[] AdditionalCatalogPaths =
        {
            "Assets/Resources/Data/Act2ContentCatalog.json",
            "Assets/Resources/Data/Act3ContentCatalog.json",
            "Assets/Resources/Data/Act4ContentCatalog.json",
            "Assets/Resources/Data/Act5ContentCatalog.json",
            "Assets/Resources/Data/Act6ContentCatalog.json",
            "Assets/Resources/Data/Act7ContentCatalog.json",
            "Assets/Resources/Data/Act8ContentCatalog.json"
        };
        private const string SkillFolder = "Assets/Data/Generated/Skills";
        private const string ItemFolder = "Assets/Data/Generated/Items";
        private const string ShadowFolder = "Assets/Data/Generated/Shadows";
        private const string ArtFolder = "Assets/Art/Generated/Core";
        private const string FinalPortraitFolder = "Assets/Art/Final/Portraits";
        private const string DatabasePath = "Assets/Resources/ShadowDatabase.asset";
        private const string RegionCatalogPath = "Assets/Resources/Data/RegionCatalog.json";
        private const string LegendaryCatalogPath = "Assets/Resources/Data/LegendaryGrowthCatalog.json";
        private const string RosterManifestPath = "Assets/Resources/Data/RegionalRosterManifest.json";
        private const int TargetCoreShadowCount = 177; // 별도 성장 전설 3종을 더해 총 180종

        [MenuItem("Tools/Shadow Theater/Generate Core Content Data")]
        public static void Generate()
        {
            if (!File.Exists(CatalogPath))
            {
                Debug.LogError($"[CoreContent] 카탈로그가 없습니다: {CatalogPath}");
                return;
            }

            var catalogs = new List<CoreCatalog>
            {
                JsonUtility.FromJson<CoreCatalog>(File.ReadAllText(CatalogPath))
            };
            foreach (string path in AdditionalCatalogPaths)
                if (File.Exists(path)) catalogs.Add(JsonUtility.FromJson<CoreCatalog>(File.ReadAllText(path)));
            var baseCatalog = Merge(catalogs);
            catalogs.Add(BuildRegionalExpansion(baseCatalog));
            var catalog = Merge(catalogs);
            int generatedGrowthForms = ApplyTierGrowthPolicy(catalog);
            if (catalog.shadows == null || catalog.shadows.Length != TargetCoreShadowCount)
            {
                Debug.LogError($"[CoreContent] 목표 로스터 불일치: {catalog.shadows?.Length ?? 0}/{TargetCoreShadowCount}");
                return;
            }
            if (!Validate(catalog, out string error))
            {
                Debug.LogError($"[CoreContent] {error}");
                return;
            }

            EnsureFolders();
            var skills = GenerateSkills(catalog.skills);
            var items = GenerateItems(catalog.items);
            var shadows = GenerateShadows(catalog.shadows, skills);
            UpdateDatabase(skills.Values, items, shadows);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = shadows.FirstOrDefault();
            Debug.Log($"[CoreContent] 생성 완료: 그림자 {shadows.Count}종, 스킬 {skills.Count}개, " +
                      $"도구 {items.Count}개, 등급 기반 성장 형태 {generatedGrowthForms}개 보완");
        }

        /// <summary>
        /// 수작업 성장 데이터가 없는 그림자만 등급 원칙으로 보완한다.
        /// Standard는 단일 형태, Rare는 기억 복원, RegionalBoss/Legendary는 양쪽 진명까지 제공한다.
        /// </summary>
        public static void ValidateGrowthPolicyRegression(List<string> errors)
        {
            var manual = new FormSpec
            {
                formName = "수작업 진명", requiredLevel = 61, requiredFlag = "preserved_flag",
                hpMultiplier = 1.7f, bonusSkills = new[] { "manual_ultimate" }
            };
            var incomplete = new FormSpec
            {
                formName = "기존 원한", requiredLevel = 55, bonusSkills = new[] { "ordinary_skill" }
            };
            var samples = new[]
            {
                new ShadowSpec { shadowId = "null_forms", displayName = "샘플", growthTier = "RegionalBoss" },
                new ShadowSpec { shadowId = "empty_forms", displayName = "샘플", growthTier = "Legendary",
                    restored = new FormSpec(), salvation = new FormSpec(), grudge = new FormSpec() },
                new ShadowSpec { shadowId = "manual_forms", displayName = "샘플", growthTier = "Legendary",
                    salvation = manual, grudge = incomplete },
                new ShadowSpec { shadowId = "standard", displayName = "샘플", growthTier = "Standard" },
                new ShadowSpec { shadowId = "rare", displayName = "샘플", growthTier = "Rare" }
            };
            var catalog = new CoreCatalog
            {
                shadows = samples, skills = new[]
                {
                    new SkillSpec { skillId = "manual_ultimate", isUltimate = true },
                    new SkillSpec { skillId = "ordinary_skill" }
                }
            };
            ApplyTierGrowthPolicy(catalog);
            int skillCount = catalog.skills.Length;
            string snapshot = JsonUtility.ToJson(catalog);
            ApplyTierGrowthPolicy(catalog);
            if (catalog.skills.Length != skillCount || JsonUtility.ToJson(catalog) != snapshot)
                errors.Add("진명 생성: 반복 생성 결과 변경 또는 기술 중복");
            foreach (var shadow in samples.Take(3))
            foreach (var form in new[] { shadow.salvation, shadow.grudge })
                if (form == null || string.IsNullOrWhiteSpace(form.formName) || form.requiredLevel < 1 ||
                    !catalog.skills.Any(x => x.isUltimate && (form.bonusSkills ?? Array.Empty<string>()).Contains(x.skillId)))
                    errors.Add($"진명 생성: null/빈 형태 또는 필살기 보완 실패 ({shadow.shadowId})");
            if (!ReferenceEquals(samples[2].salvation, manual) || manual.requiredFlag != "preserved_flag" ||
                manual.hpMultiplier != 1.7f || manual.bonusSkills.Length != 1 || manual.bonusSkills[0] != "manual_ultimate")
                errors.Add("진명 생성: 수작업 형태 또는 필살기 변경");
            if (!ReferenceEquals(samples[2].grudge, incomplete) || !incomplete.bonusSkills.Contains("ordinary_skill"))
                errors.Add("진명 생성: 기존 일반 기술 또는 형태 유실");
            if (samples[3].restored != null || samples[3].salvation != null || samples[3].grudge != null ||
                samples[4].restored == null || samples[4].salvation != null || samples[4].grudge != null)
                errors.Add("진명 생성: Standard/Rare 성장 단계 규칙 위반");
        }

        private static int ApplyTierGrowthPolicy(CoreCatalog catalog)
        {
            if (catalog?.shadows == null) return 0;
            var skills = new List<SkillSpec>(catalog.skills ?? Array.Empty<SkillSpec>());
            var skillIds = new HashSet<string>(skills.Select(x => x.skillId), StringComparer.Ordinal);
            int forms = 0;
            foreach (ShadowSpec shadow in catalog.shadows)
            {
                if (shadow == null || shadow.growthTier == "Standard") continue;
                bool fullGrowth = shadow.growthTier == "RegionalBoss" || shadow.growthTier == "Legendary";
                int restoreLevel = Mathf.Clamp(14 + shadow.expReward / 12, 18, 62);
                int trueNameLevel = Mathf.Clamp(restoreLevel + (fullGrowth ? 12 : 0), 30, 75);

                if (shadow.restored == null || string.IsNullOrWhiteSpace(shadow.restored.formName))
                {
                    shadow.restored = AutoForm($"기억을 되찾은 {shadow.displayName}", shadow.accentColor,
                        restoreLevel, 1.22f, 1.18f, 1.16f, 1.10f,
                        $"흩어진 장면이 이어지며 {shadow.displayName}은(는) 자신이 지키려 했던 마지막 약속을 기억했다.");
                    forms++;
                }
                if (!fullGrowth) continue;

                if (shadow.salvation == null || string.IsNullOrWhiteSpace(shadow.salvation.formName))
                {
                    string skillId = shadow.shadowId + "_salvation_ultimate";
                    if (skillIds.Add(skillId)) skills.Add(AutoUltimate(shadow, skillId, trueNameLevel, true));
                    shadow.salvation = AutoForm($"진명·새벽의 {shadow.displayName}", "#F7E7A9", trueNameLevel,
                        1.48f, 1.42f, 1.38f, 1.20f,
                        "비극을 지우지 않고 품어 낸 진명이 다음 장면을 비추기 시작했다.", skillId);
                    shadow.salvation.bonusCritRate = .06f;
                    shadow.salvation.bonusEvasion = .03f;
                    forms++;
                }
                if (shadow.grudge == null || string.IsNullOrWhiteSpace(shadow.grudge.formName))
                {
                    string skillId = shadow.shadowId + "_grudge_ultimate";
                    if (skillIds.Add(skillId)) skills.Add(AutoUltimate(shadow, skillId, trueNameLevel, false));
                    shadow.grudge = AutoForm($"진명·심연의 {shadow.displayName}", "#B62E68", trueNameLevel,
                        1.34f, 1.62f, 1.24f, 1.32f,
                        "상처를 놓지 않은 진명이 원한을 칼날로 벼려 마지막 막을 찢는다.", skillId);
                    shadow.grudge.bonusCritRate = .13f;
                    shadow.grudge.bonusEvasion = .05f;
                    forms++;
                }
                EnsureTrueNameUltimate(shadow, shadow.salvation, skills, skillIds, trueNameLevel, true);
                EnsureTrueNameUltimate(shadow, shadow.grudge, skills, skillIds, trueNameLevel, false);
            }
            catalog.skills = skills.ToArray();
            return forms;
        }

        private static void EnsureTrueNameUltimate(ShadowSpec shadow, FormSpec form, List<SkillSpec> skills,
            HashSet<string> skillIds, int fallbackLevel, bool salvation)
        {
            // JsonUtility가 null 성장 항목을 빈 객체로 복원하거나 수작업 형태에 기술이 없는 경우도 보완한다.
            if (form == null) return;
            var bonus = new List<string>(form.bonusSkills ?? Array.Empty<string>());
            if (skills.Any(x => x != null && x.isUltimate && bonus.Contains(x.skillId))) return;
            string id = shadow.shadowId + (salvation ? "_salvation_ultimate" : "_grudge_ultimate");
            int level = form.requiredLevel > 0 ? form.requiredLevel : fallbackLevel;
            if (skillIds.Add(id)) skills.Add(AutoUltimate(shadow, id, level, salvation));
            if (!bonus.Contains(id)) bonus.Add(id);
            form.bonusSkills = bonus.ToArray();
        }

        private static FormSpec AutoForm(string name, string color, int level, float hp, float atk,
            float def, float spd, string lore, params string[] bonusSkills) => new FormSpec
        {
            formName = name, accentColor = color, requiredLevel = level, requiredFlag = string.Empty,
            hpMultiplier = hp, atkMultiplier = atk, defMultiplier = def, spdMultiplier = spd,
            bonusSkills = bonusSkills ?? Array.Empty<string>(), loreAppend = lore
        };

        private static SkillSpec AutoUltimate(ShadowSpec shadow, string id, int level, bool salvation)
        {
            string style = shadow.element == "Flame" ? "FlameCrown"
                : shadow.element == "Frost" ? "FrozenArchive"
                : shadow.element == "Shade" ? "LivingScript" : "CosmicAudience";
            bool magical = shadow.role == "MagicNuker" || shadow.role == "Support";
            return new SkillSpec
            {
                skillId = id,
                displayName = salvation ? $"진명·{shadow.displayName}의 새벽" : $"진명·{shadow.displayName}의 심연",
                description = salvation ? "되찾은 이름으로 비극을 가르고 다음 장면을 연다."
                    : "지워지지 않은 원한을 한순간에 폭발시킨다.",
                requiredLevel = level, fpCost = 4, damageType = magical ? "Magical" : "Physical",
                element = string.IsNullOrEmpty(shadow.element) ? "None" : shadow.element,
                damageMultiplier = salvation ? 3.25f : 3.5f, accuracy = salvation ? 1f : .94f,
                bonusCritRate = salvation ? .08f : .16f, target = "Enemy",
                statusEffect = salvation ? "DefenseDown" : "Bleed", statusChance = salvation ? 1f : .85f,
                statusDuration = 3, isUltimate = true, ultimateFxStyle = style,
                primaryFxColor = salvation ? "#FFF0B5" : "#C12E72",
                secondaryFxColor = string.IsNullOrEmpty(shadow.accentColor) ? "#8F7CC9" : shadow.accentColor,
                ultimateBurstCount = salvation ? 32 : 38, cameraShake = salvation ? 1.05f : 1.25f,
                hitStopDuration = .09f, sfxVolume = .95f, sfxPitch = salvation ? 1.08f : .92f
            };
        }

        private static CoreCatalog BuildRegionalExpansion(CoreCatalog source)
        {
            int needed = Mathf.Max(0, TargetCoreShadowCount - (source.shadows?.Length ?? 0));
            if (needed == 0) return new CoreCatalog { skills = Array.Empty<SkillSpec>(), items = Array.Empty<ItemSpec>(), shadows = Array.Empty<ShadowSpec>() };
            var regionCatalog = File.Exists(RegionCatalogPath)
                ? JsonUtility.FromJson<RegionCatalogSpec>(File.ReadAllText(RegionCatalogPath)) : null;
            if (regionCatalog?.regions == null || regionCatalog.regions.Length == 0)
                throw new InvalidOperationException("지역 로스터 생성을 위한 RegionCatalog이 없습니다.");

            var existingNames = new HashSet<string>((source.shadows ?? Array.Empty<ShadowSpec>()).Select(x => x.displayName));
            if (File.Exists(LegendaryCatalogPath))
            {
                var legendary = JsonUtility.FromJson<CoreCatalog>(File.ReadAllText(LegendaryCatalogPath));
                foreach (ShadowSpec shadow in legendary?.shadows ?? Array.Empty<ShadowSpec>())
                    if (!string.IsNullOrEmpty(shadow.displayName)) existingNames.Add(shadow.displayName);
            }
            var specs = new List<ShadowSpec>(needed);
            var manifest = new RegionalRosterManifest { entries = new List<RegionalRosterEntry>() };
            var slots = regionCatalog.regions.ToDictionary(x => x.regionId, _ => 1);

            // 월드맵에 선언됐지만 실제 데이터가 없던 대표 그림자를 최우선으로 채운다.
            foreach (RegionSpec region in regionCatalog.regions)
            foreach (string featured in region.featuredShadows ?? Array.Empty<string>())
            {
                if (specs.Count >= needed) break;
                if (string.IsNullOrEmpty(featured) || existingNames.Contains(featured)) continue;
                AddRegionalShadow(specs, manifest, region, slots[region.regionId]++, featured, true);
                existingNames.Add(featured);
            }

            string[] suffixes = { "장막쥐", "잔향나비", "기억여우", "가면사슴", "등불새", "문장벌레", "안개늑대", "종소리망령" };
            int pass = 0;
            while (specs.Count < needed)
            {
                RegionSpec region = regionCatalog.regions[pass % regionCatalog.regions.Length];
                int slot = slots[region.regionId]++;
                string name = $"{region.displayName}의 {suffixes[(slot + region.order) % suffixes.Length]}";
                if (existingNames.Add(name)) AddRegionalShadow(specs, manifest, region, slot, name, false);
                pass++;
            }

            File.WriteAllText(RosterManifestPath, JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(RosterManifestPath, ImportAssetOptions.ForceSynchronousImport);
            return new CoreCatalog { skills = Array.Empty<SkillSpec>(), items = Array.Empty<ItemSpec>(), shadows = specs.ToArray() };
        }

        private static void AddRegionalShadow(List<ShadowSpec> output, RegionalRosterManifest manifest,
            RegionSpec region, int slot, string displayName, bool featured)
        {
            int act = Mathf.Max(1, region.act);
            string element = act == 2 ? "Flame" : act == 3 || act == 7 ? "Frost" : act >= 4 ? "Shade" : "None";
            string[] roles = { "PhysicalDealer", "MagicNuker", "SpeedUtility", "Tank", "Support" };
            string[] shapes = { "Knight", "Mage", "Crow", "Beast", "Mask" };
            string role = roles[(region.order + slot) % roles.Length];
            string shape = shapes[(region.order * 2 + slot) % shapes.Length];
            bool rare = featured || (region.order + slot) % 7 == 0;
            int level = Mathf.RoundToInt((region.recommendedLevelMin + region.recommendedLevelMax) * .5f);
            string id = $"exp_{region.regionId}_{slot:00}";
            string basic = role == "MagicNuker" || role == "Support" ? "inkshot" : shape == "Crow" ? "peck" : shape == "Beast" ? "claw" : "slash";
            string[] skills = element == "Flame" ? new[] { "ember_strike", "cinder_peck" }
                : element == "Frost" ? new[] { "frost_sigil", "frost_breath" }
                : element == "Shade" ? new[] { "night_leap", "black_ink" }
                : new[] { "cut_string", "stitch" };
            output.Add(new ShadowSpec
            {
                shadowId=id, displayName=displayName, title=$"{region.environment}에 남은 {(featured ? "대표 배역" : "떠도는 잔영")}",
                growthTier=rare ? "Rare" : "Standard", element=element, role=role, shape=shape,
                accentColor=string.IsNullOrEmpty(region.accentHex) ? "#8F7CC9" : "#" + region.accentHex,
                baseHp=85+level*2+(role=="Tank"?35:0), baseAtk=18+Mathf.RoundToInt(level*.38f),
                baseDef=10+Mathf.RoundToInt(level*.23f)+(role=="Tank"?10:0), baseSpd=10+Mathf.RoundToInt(level*.12f)+(role=="SpeedUtility"?8:0),
                critRate=role=="SpeedUtility"?.17f:.09f, evasion=role=="SpeedUtility"?.13f:.05f,
                hpGrowth=9f+level*.09f, atkGrowth=2f+level*.025f, defGrowth=1.2f+level*.018f, spdGrowth=.35f+level*.004f,
                basicAttack=basic, skills=skills, baseCaptureRate=rare?.18f:.34f,
                expReward=40+level*4, goldReward=20+level*2,
                loreLocked=$"{region.displayName}의 {region.environment} 속에서 오래된 배역을 반복한다.",
                loreUnlocked=$"{region.summary} 그날, {displayName}은 사라지는 이들의 마지막 장면을 대신 기억했다.",
                restored=null, salvation=null, grudge=null
            });
            manifest.entries.Add(new RegionalRosterEntry { shadowId=id, regionId=region.regionId,
                sceneName=region.sceneName, minLevel=region.recommendedLevelMin, maxLevel=region.recommendedLevelMax });
        }

        private static bool Validate(CoreCatalog catalog, out string error)
        {
            if (catalog?.skills == null || catalog.items == null || catalog.shadows == null || catalog.shadows.Length == 0)
            {
                error = "카탈로그 배열이 비어 있습니다.";
                return false;
            }
            var skillIds = new HashSet<string>(catalog.skills.Select(x => x.skillId));
            if (skillIds.Count != catalog.skills.Length)
            {
                error = "중복된 skillId가 있습니다.";
                return false;
            }
            var shadowIds = new HashSet<string>(catalog.shadows.Select(x => x.shadowId));
            if (shadowIds.Count != catalog.shadows.Length)
            {
                error = "중복된 shadowId가 있습니다.";
                return false;
            }
            if (catalog.items.Length != 8)
            {
                error = $"도구 수가 올바르지 않습니다: {catalog.items.Length}/8";
                return false;
            }
            var itemIds = new HashSet<string>(catalog.items.Select(x => x.itemId));
            if (itemIds.Count != catalog.items.Length || catalog.items.Any(x => string.IsNullOrEmpty(x.itemId)))
            {
                error = "비어 있거나 중복된 itemId가 있습니다.";
                return false;
            }
            foreach (var item in catalog.items)
            {
                if (!Enum.TryParse(item.effectType, true, out ItemEffectType _))
                {
                    error = $"{item.itemId}의 효과 타입이 올바르지 않습니다: {item.effectType}";
                    return false;
                }
                if (item.buyPrice <= 0 || item.sellPrice < 0 || item.sellPrice > item.buyPrice)
                {
                    error = $"{item.itemId}의 구매/판매 가격이 올바르지 않습니다.";
                    return false;
                }
                if (item.shopUnlockAct < 1 || item.shopUnlockAct > 8)
                {
                    error = $"{item.itemId}의 상점 해금 막이 올바르지 않습니다: {item.shopUnlockAct}";
                    return false;
                }
            }
            foreach (var shadow in catalog.shadows)
            {
                var references = new List<string> { shadow.basicAttack };
                if (shadow.skills != null) references.AddRange(shadow.skills);
                AddFormSkills(references, shadow.restored);
                AddFormSkills(references, shadow.salvation);
                AddFormSkills(references, shadow.grudge);
                string missing = references.FirstOrDefault(x => !string.IsNullOrEmpty(x) && !skillIds.Contains(x));
                if (missing != null)
                {
                    error = $"{shadow.shadowId}가 존재하지 않는 스킬을 참조합니다: {missing}";
                    return false;
                }
            }
            error = null;
            return true;
        }

        private static CoreCatalog Merge(IEnumerable<CoreCatalog> catalogs)
        {
            var valid = catalogs.Where(x => x != null).ToList();
            return new CoreCatalog
            {
                skills = valid.SelectMany(x => x.skills ?? Array.Empty<SkillSpec>()).ToArray(),
                items = valid.SelectMany(x => x.items ?? Array.Empty<ItemSpec>()).ToArray(),
                shadows = valid.SelectMany(x => x.shadows ?? Array.Empty<ShadowSpec>()).ToArray()
            };
        }

        private static void AddFormSkills(List<string> target, FormSpec form)
        {
            if (form?.bonusSkills != null) target.AddRange(form.bonusSkills);
        }

        private static Dictionary<string, SkillData> GenerateSkills(IEnumerable<SkillSpec> specs)
        {
            var result = new Dictionary<string, SkillData>();
            foreach (var spec in specs)
            {
                string path = $"{SkillFolder}/{spec.skillId}.asset";
                var skill = LoadOrCreate<SkillData>(path);
                skill.name = skill.skillId = spec.skillId;
                skill.displayName = spec.displayName;
                skill.description = spec.description;
                skill.requiredLevel = UnlockLevel(spec.requiredLevel, spec.fpCost, spec.isUltimate);
                skill.fpCost = spec.fpCost;
                skill.fpGain = spec.fpGain;
                skill.damageType = Parse(spec.damageType, DamageType.None);
                skill.element = Parse(spec.element, ShadowElement.None);
                skill.damageMultiplier = spec.damageMultiplier;
                skill.accuracy = spec.accuracy;
                skill.bonusCritRate = spec.bonusCritRate;
                skill.target = Parse(spec.target, SkillTarget.Enemy);
                skill.healRatio = spec.healRatio;
                skill.statusEffect = Parse(spec.statusEffect, StatusEffectType.None);
                skill.statusChance = spec.statusChance;
                skill.statusDuration = spec.statusDuration;
                skill.isUltimate = spec.isUltimate;
                skill.ultimateFxStyle = Parse(spec.ultimateFxStyle, UltimateFxStyle.None);
                skill.primaryFxColor = ParseColor(spec.primaryFxColor, Color.white);
                skill.secondaryFxColor = ParseColor(spec.secondaryFxColor, skill.primaryFxColor);
                skill.ultimateBurstCount = spec.ultimateBurstCount > 0 ? spec.ultimateBurstCount : 18;
                skill.sfxVolume = spec.sfxVolume > 0f ? spec.sfxVolume : (spec.isUltimate ? 0.9f : 0.65f);
                skill.sfxPitch = spec.sfxPitch > 0f ? spec.sfxPitch : 1f;
                skill.hitStopDuration = spec.hitStopDuration > 0f ? spec.hitStopDuration : (spec.isUltimate ? 0.08f : 0.025f);
                skill.cameraShake = spec.cameraShake;
                EditorUtility.SetDirty(skill);
                result[skill.skillId] = skill;
            }
            return result;
        }

        private static List<ItemData> GenerateItems(IEnumerable<ItemSpec> specs)
        {
            var result = new List<ItemData>();
            foreach (var spec in specs)
            {
                var item = LoadOrCreate<ItemData>($"{ItemFolder}/{spec.itemId}.asset");
                item.name = item.itemId = spec.itemId;
                item.displayName = spec.displayName;
                item.description = spec.description;
                item.icon = GenerateItemIcon(spec);
                item.effectType = Parse(spec.effectType, ItemEffectType.HealRatio);
                item.value = spec.value;
                item.usableInBattle = spec.usableInBattle;
                item.usableInField = spec.usableInField;
                item.buyPrice = Mathf.Max(0, spec.buyPrice);
                item.sellPrice = Mathf.Clamp(spec.sellPrice, 0, item.buyPrice);
                item.shopUnlockAct = Mathf.Clamp(spec.shopUnlockAct > 0 ? spec.shopUnlockAct : 1, 1, 8);
                EditorUtility.SetDirty(item);
                result.Add(item);
            }
            return result;
        }

        private static Sprite GenerateItemIcon(ItemSpec spec)
        {
            string path = $"{ArtFolder}/item_{spec.itemId}.png";
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(Enumerable.Repeat(Color.clear, size * size).ToArray());
            Color outline = new Color(.08f,.06f,.13f,1f);
            ItemEffectType effect = Parse(spec.effectType, ItemEffectType.HealRatio);
            Color liquid = effect == ItemEffectType.HealFlat || effect == ItemEffectType.HealRatio ? new Color(.22f,.92f,.58f,1f)
                : effect == ItemEffectType.GainFP ? new Color(.22f,.78f,1f,1f)
                : effect == ItemEffectType.CaptureBoost ? new Color(.70f,.30f,1f,1f)
                : new Color(1f,.76f,.30f,1f);
            Ellipse(texture,32,25,20,22,outline); Rect(texture,25,42,39,55,outline); Rect(texture,23,53,41,59,outline);
            Ellipse(texture,32,23,15,16,liquid); Rect(texture,29,39,35,48,liquid);
            Rect(texture,27,55,37,60,new Color(.78f,.58f,.28f,1f));
            if (spec.itemId.Contains("greater") || spec.itemId.Contains("silver"))
            { Circle(texture,20,42,3,Color.white); Circle(texture,45,34,2,Color.white); }
            texture.Apply(false,false); File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer!=null){importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=64f;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static List<ShadowData> GenerateShadows(IEnumerable<ShadowSpec> specs,
                                                        IReadOnlyDictionary<string, SkillData> skills)
        {
            var result = new List<ShadowData>();
            foreach (var spec in specs)
            {
                var shadow = LoadOrCreate<ShadowData>($"{ShadowFolder}/{spec.shadowId}.asset");
                shadow.name = shadow.shadowId = spec.shadowId;
                shadow.displayName = spec.displayName;
                shadow.title = spec.title;
                shadow.growthTier = Parse(spec.growthTier, GrowthTier.Standard);
                shadow.element = Parse(spec.element, ShadowElement.None);
                shadow.role = Parse(spec.role, ShadowRole.PhysicalDealer);
                shadow.silhouetteSprite = LoadFinalPortrait(spec.shadowId) ?? GeneratePlaceholderSprite(spec.shadowId, spec.shape);
                shadow.accentColor = ParseColor(spec.accentColor, Color.white);
                shadow.baseHp = spec.baseHp;
                shadow.baseAtk = spec.baseAtk;
                shadow.baseDef = spec.baseDef;
                shadow.baseSpd = spec.baseSpd;
                shadow.critRate = spec.critRate;
                shadow.evasion = spec.evasion;
                shadow.hpGrowth = spec.hpGrowth;
                shadow.atkGrowth = spec.atkGrowth;
                shadow.defGrowth = spec.defGrowth;
                shadow.spdGrowth = spec.spdGrowth;
                shadow.basicAttack = SkillFor(skills, spec.basicAttack);
                shadow.skills = SkillsFor(skills, spec.skills);
                shadow.baseCaptureRate = spec.baseCaptureRate;
                shadow.expReward = spec.expReward;
                shadow.goldReward = spec.goldReward;
                shadow.dropItemId = !string.IsNullOrEmpty(spec.dropItemId) ? spec.dropItemId : DefaultDropItem(spec);
                shadow.dropChance = spec.dropChance > 0f ? Mathf.Clamp01(spec.dropChance)
                    : (shadow.growthTier == GrowthTier.Rare ? .16f : .24f);
                shadow.dropMinCount = Mathf.Max(1, spec.dropMinCount);
                shadow.dropMaxCount = Mathf.Max(shadow.dropMinCount, spec.dropMaxCount);
                shadow.loreLocked = spec.loreLocked;
                shadow.loreUnlocked = spec.loreUnlocked;
                shadow.restoredForm ??= new MemoryFormData();
                shadow.salvationForm ??= new MemoryFormData();
                shadow.grudgeForm ??= new MemoryFormData();
                ApplyForm(shadow.restoredForm, spec.restored, skills);
                ApplyForm(shadow.salvationForm, spec.salvation, skills);
                ApplyForm(shadow.grudgeForm, spec.grudge, skills);
                EditorUtility.SetDirty(shadow);
                result.Add(shadow);
            }
            return result;
        }

        private static string DefaultDropItem(ShadowSpec spec)
        {
            if (spec.growthTier == "Rare") return "ink";
            if (spec.role == "MagicNuker" || spec.role == "SpeedUtility") return "tonic";
            if (spec.role == "Support") return "salve";
            return "potion";
        }

        private static void ApplyForm(MemoryFormData target, FormSpec spec,
                                      IReadOnlyDictionary<string, SkillData> skills)
        {
            target.enabled = spec != null;
            target.silhouetteSprite = null; // 전용 아트가 없으면 기본 실루엣과 포인트 컬러를 사용한다.
            target.bonusSkills = new List<SkillData>();
            if (spec == null) return;
            target.formName = spec.formName;
            target.accentColor = ParseColor(spec.accentColor, Color.white);
            target.requiredLevel = spec.requiredLevel;
            target.requiredFlag = spec.requiredFlag;
            target.hpMultiplier = spec.hpMultiplier;
            target.atkMultiplier = spec.atkMultiplier;
            target.defMultiplier = spec.defMultiplier;
            target.spdMultiplier = spec.spdMultiplier;
            target.bonusCritRate = spec.bonusCritRate;
            target.bonusEvasion = spec.bonusEvasion;
            target.bonusSkills = SkillsFor(skills, spec.bonusSkills);
            target.loreAppend = spec.loreAppend;
        }

        private static Sprite GeneratePlaceholderSprite(string id, string shape)
        {
            string path = $"{ArtFolder}/{id}_placeholder.png";
            if (!File.Exists(path))
            {
                const int size = 192;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = Enumerable.Repeat(Color.clear, size * size).ToArray();
                texture.SetPixels(pixels);
                DrawSilhouette(texture, shape);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != 96f))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 96f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite LoadFinalPortrait(string id)
        {
            string path = $"{FinalPortraitFolder}/{id}.png";
            if (!File.Exists(path)) return null;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 256f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void DrawSilhouette(Texture2D texture, string shape)
        {
            Color ink = new Color(0.96f, 0.96f, 0.98f, 1f); // UI에서 검게 틴트된다.
            switch (shape)
            {
                case "Knight":
                    Circle(texture, 86, 137, 20, ink); Triangle(texture, 48, 31, 123, 31, 91, 127, ink);
                    Rect(texture, 117, 55, 128, 145, ink); Triangle(texture, 122, 145, 135, 145, 128, 174, ink); break;
                case "Mage":
                    Circle(texture, 96, 127, 17, ink); Triangle(texture, 45, 28, 147, 28, 96, 125, ink);
                    Triangle(texture, 61, 141, 133, 141, 101, 180, ink); Rect(texture, 126, 78, 151, 105, ink); break;
                case "Beast":
                    Circle(texture, 129, 104, 24, ink); Rect(texture, 54, 72, 132, 108, ink);
                    Triangle(texture, 124, 125, 134, 125, 129, 151, ink); Rect(texture, 64, 31, 77, 77, ink);
                    Rect(texture, 111, 31, 124, 77, ink); Line(texture, 55, 95, 26, 126, 13, ink); break;
                case "Puppet":
                    Circle(texture, 96, 139, 17, ink); Rect(texture, 76, 79, 116, 132, ink);
                    Line(texture, 78, 117, 47, 79, 10, ink); Line(texture, 114, 117, 145, 79, 10, ink);
                    Line(texture, 85, 80, 68, 30, 11, ink); Line(texture, 107, 80, 124, 30, 11, ink); break;
                case "Crow":
                    Circle(texture, 96, 103, 22, ink); Triangle(texture, 15, 77, 83, 118, 65, 55, ink);
                    Triangle(texture, 177, 77, 109, 118, 127, 55, ink); Triangle(texture, 88, 86, 104, 86, 96, 28, ink);
                    Triangle(texture, 114, 106, 149, 112, 118, 97, ink); break;
                case "Mask":
                    Ellipse(texture, 96, 103, 46, 67, ink); Triangle(texture, 50, 92, 69, 109, 53, 126, Color.clear);
                    Triangle(texture, 142, 92, 123, 109, 139, 126, Color.clear); break;
                default:
                    Circle(texture, 96, 139, 18, ink); Triangle(texture, 49, 28, 143, 28, 96, 127, ink);
                    Rect(texture, 38, 73, 55, 151, ink); Rect(texture, 137, 73, 154, 151, ink); break;
            }
        }

        private static void Circle(Texture2D t, int cx, int cy, int radius, Color color) =>
            Ellipse(t, cx, cy, radius, radius, color);

        private static void Ellipse(Texture2D t, int cx, int cy, int rx, int ry, Color color)
        {
            for (int y = -ry; y <= ry; y++)
                for (int x = -rx; x <= rx; x++)
                    if ((x * x) / (float)(rx * rx) + (y * y) / (float)(ry * ry) <= 1f) Set(t, cx + x, cy + y, color);
        }

        private static void Rect(Texture2D t, int x0, int y0, int x1, int y1, Color color)
        {
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(t, x, y, color);
        }

        private static void Triangle(Texture2D t, int ax, int ay, int bx, int by, int cx, int cy, Color color)
        {
            int minX = Mathf.Min(ax, bx, cx), maxX = Mathf.Max(ax, bx, cx);
            int minY = Mathf.Min(ay, by, cy), maxY = Mathf.Max(ay, by, cy);
            float area = Edge(ax, ay, bx, by, cx, cy);
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float w0 = Edge(bx, by, cx, cy, x, y), w1 = Edge(cx, cy, ax, ay, x, y), w2 = Edge(ax, ay, bx, by, x, y);
                if (area >= 0 ? w0 >= 0 && w1 >= 0 && w2 >= 0 : w0 <= 0 && w1 <= 0 && w2 <= 0) Set(t, x, y, color);
            }
        }

        private static float Edge(int ax, int ay, int bx, int by, int px, int py) =>
            (px - ax) * (by - ay) - (py - ay) * (bx - ax);

        private static void Line(Texture2D t, int x0, int y0, int x1, int y1, int width, Color color)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                float p = steps == 0 ? 0 : i / (float)steps;
                Circle(t, Mathf.RoundToInt(Mathf.Lerp(x0, x1, p)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, p)), width / 2, color);
            }
        }

        private static void Set(Texture2D t, int x, int y, Color color)
        {
            if (x >= 0 && x < t.width && y >= 0 && y < t.height) t.SetPixel(x, y, color);
        }

        private static void UpdateDatabase(IEnumerable<SkillData> skills, IEnumerable<ItemData> items,
                                           IEnumerable<ShadowData> shadows)
        {
            var database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<ShadowDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }
            foreach (var skill in skills) AddOrReplace(database.skills, skill, x => x.skillId);
            foreach (var item in items) AddOrReplace(database.items, item, x => x.itemId);
            foreach (var shadow in shadows) AddOrReplace(database.shadows, shadow, x => x.shadowId);
            EditorUtility.SetDirty(database);
        }

        private static void AddOrReplace<T>(List<T> list, T value, Func<T, string> id) where T : UnityEngine.Object
        {
            int index = list.FindIndex(x => x != null && id(x) == id(value));
            if (index >= 0) list[index] = value; else list.Add(value);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static SkillData SkillFor(IReadOnlyDictionary<string, SkillData> skills, string id) =>
            !string.IsNullOrEmpty(id) && skills.TryGetValue(id, out var skill) ? skill : null;

        private static List<SkillData> SkillsFor(IReadOnlyDictionary<string, SkillData> skills, string[] ids) =>
            ids == null ? new List<SkillData>() : ids.Select(x => SkillFor(skills, x)).Where(x => x != null).ToList();

        private static T Parse<T>(string value, T fallback) where T : struct =>
            Enum.TryParse(value, true, out T result) ? result : fallback;

        private static int UnlockLevel(int configured, int fpCost, bool ultimate) =>
            Mathf.Clamp(configured > 0 ? configured : (ultimate ? 1 : fpCost <= 1 ? 1 : fpCost == 2 ? 5 : fpCost == 3 ? 12 : 20), 1, 100);

        private static Color ParseColor(string html, Color fallback) =>
            !string.IsNullOrEmpty(html) && ColorUtility.TryParseHtmlString(html, out var color) ? color : fallback;

        private static void EnsureFolders()
        {
            Ensure("Assets", "Data"); Ensure("Assets/Data", "Generated");
            Ensure("Assets/Data/Generated", "Skills"); Ensure("Assets/Data/Generated", "Items");
            Ensure("Assets/Data/Generated", "Shadows"); Ensure("Assets", "Art");
            Ensure("Assets/Art", "Generated"); Ensure("Assets/Art/Generated", "Core");
        }

        private static void Ensure(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}")) AssetDatabase.CreateFolder(parent, child);
        }

        [Serializable] private class CoreCatalog { public SkillSpec[] skills; public ItemSpec[] items; public ShadowSpec[] shadows; }
        [Serializable] private class SkillSpec
        {
            public string skillId, displayName, description, damageType, element, target, statusEffect;
            public string ultimateFxStyle, primaryFxColor, secondaryFxColor;
            public int fpCost, fpGain, statusDuration, requiredLevel;
            public int ultimateBurstCount;
            public float damageMultiplier, accuracy, bonusCritRate, healRatio, statusChance, cameraShake;
            public float sfxVolume, sfxPitch, hitStopDuration;
            public bool isUltimate;
        }
        [Serializable] private class ItemSpec
        {
            public string itemId, displayName, description, effectType;
            public float value;
            public int buyPrice, sellPrice, shopUnlockAct = 1;
            public bool usableInBattle, usableInField;
        }
        [Serializable] private class ShadowSpec
        {
            public string shadowId, displayName, title, growthTier, element, role, shape, accentColor, dropItemId;
            public int baseHp, baseAtk, baseDef, baseSpd, expReward, goldReward, dropMinCount = 1, dropMaxCount = 1;
            public float critRate, evasion, hpGrowth, atkGrowth, defGrowth, spdGrowth, baseCaptureRate, dropChance;
            public string basicAttack, loreLocked, loreUnlocked;
            public string[] skills;
            public FormSpec restored, salvation, grudge;
        }
        [Serializable] private class FormSpec
        {
            public string formName, accentColor, requiredFlag, loreAppend;
            public int requiredLevel;
            public float hpMultiplier, atkMultiplier, defMultiplier, spdMultiplier, bonusCritRate, bonusEvasion;
            public string[] bonusSkills;
        }
        [Serializable] private class RegionCatalogSpec { public RegionSpec[] regions; }
        [Serializable] private class RegionSpec
        {
            public string regionId, displayName, sceneName, environment, summary, accentHex;
            public int order, act, recommendedLevelMin, recommendedLevelMax;
            public string[] featuredShadows;
        }
        [Serializable] private class RegionalRosterManifest { public List<RegionalRosterEntry> entries; }
        [Serializable] private class RegionalRosterEntry
        {
            public string shadowId, regionId, sceneName;
            public int minLevel, maxLevel;
        }
    }
}
#endif
