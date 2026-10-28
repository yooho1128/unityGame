#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.Field;
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
            // Append AFTER the original expansion so all 97 generated legacy IDs remain stable.
            catalogs.Add(JsonUtility.FromJson<CoreCatalog>(File.ReadAllText("Assets/Resources/Data/ElementExpansionCatalog.json")));
            catalogs.Add(JsonUtility.FromJson<CoreCatalog>(File.ReadAllText("Assets/Resources/Data/ItemExpansionCatalog.json")));
            var catalog = Merge(catalogs);
            int generatedGrowthForms = ApplyTierGrowthPolicy(catalog);
            if (catalog.shadows == null || catalog.shadows.Length != TargetCoreShadowCount + 11)
            {
                Debug.LogError($"[CoreContent] 목표 로스터 불일치: {catalog.shadows?.Length ?? 0}/{TargetCoreShadowCount + 11}");
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
            var emptyForm = new MemoryFormData { enabled = true };
            ApplyForm(emptyForm, new FormSpec(), new Dictionary<string, SkillData>());
            if (emptyForm.enabled) errors.Add("성장 생성: JSON null에서 복원된 빈 형태가 활성화됨");
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
            var signatureSkills = new List<SkillSpec>();
            var manifest = new RegionalRosterManifest { entries = new List<RegionalRosterEntry>() };
            var slots = regionCatalog.regions.ToDictionary(x => x.regionId, _ => 1);

            // 월드맵에 선언됐지만 실제 데이터가 없던 대표 그림자를 최우선으로 채운다.
            foreach (RegionSpec region in regionCatalog.regions)
            foreach (string featured in region.featuredShadows ?? Array.Empty<string>())
            {
                if (specs.Count >= needed) break;
                if (string.IsNullOrEmpty(featured) || existingNames.Contains(featured)) continue;
                AddRegionalShadow(specs, signatureSkills, manifest, region, slots[region.regionId]++, featured, true);
                existingNames.Add(featured);
            }

            RegionSpec[] encounterRegions = regionCatalog.regions
                .Where(x => !x.isSettlement && !x.isBossStage).ToArray();
            if (encounterRegions.Length == 0)
                throw new InvalidOperationException("랜덤 조우가 가능한 지역이 없습니다.");
            var regionalSlots = encounterRegions.ToDictionary(x => x.regionId, _ => 1);
            int pass = 0;
            while (specs.Count < needed)
            {
                // 기존 저장이 참조하는 exp_{region}_{slot} ID 순서는 유지하고 출현 지역만 재배치한다.
                RegionSpec identityRegion = regionCatalog.regions[pass % regionCatalog.regions.Length];
                int identitySlot = slots[identityRegion.regionId]++;
                RegionSpec region = encounterRegions[pass % encounterRegions.Length];
                int regionalSlot = regionalSlots[region.regionId]++;
                string[] suffixes = RegionalSuffixes(region.act);
                string name = $"{region.displayName}의 {suffixes[(regionalSlot + region.order) % suffixes.Length]}";
                string stableId = $"exp_{identityRegion.regionId}_{identitySlot:00}";
                if (existingNames.Add(name))
                    AddRegionalShadow(specs, signatureSkills, manifest, region, regionalSlot, name, false, stableId);
                pass++;
            }

            File.WriteAllText(RosterManifestPath, JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(RosterManifestPath, ImportAssetOptions.ForceSynchronousImport);
            return new CoreCatalog { skills = signatureSkills.ToArray(), items = Array.Empty<ItemSpec>(), shadows = specs.ToArray() };
        }

        private static void AddRegionalShadow(List<ShadowSpec> output, List<SkillSpec> signatureSkills,
            RegionalRosterManifest manifest,
            RegionSpec region, int slot, string displayName, bool featured, string stableId = null)
        {
            int act = Mathf.Max(1, region.act);
            string element = act == 2 ? "Flame"
                : act == 3 || act == 7 ? "Frost"
                : act == 4 || act == 6 || act == 8 ? "Shade"
                : "None";
            string[] roles = { "PhysicalDealer", "MagicNuker", "SpeedUtility", "Tank", "Support" };
            string[] shapes = { "Knight", "Mage", "Crow", "Beast", "Mask" };
            string role = roles[(region.order + slot) % roles.Length];
            string shape = shapes[(region.order * 2 + slot) % shapes.Length];
            bool rare = featured || (region.order + slot) % 7 == 0;
            int level = Mathf.RoundToInt((region.recommendedLevelMin + region.recommendedLevelMax) * .5f);
            string id = string.IsNullOrEmpty(stableId) ? $"exp_{region.regionId}_{slot:00}" : stableId;
            string basic = role == "MagicNuker" || role == "Support" ? "inkshot" : shape == "Crow" ? "peck" : shape == "Beast" ? "claw" : "slash";
            string[] skills = element == "Flame" ? new[] { "ember_strike", "cinder_peck" }
                : element == "Frost" ? new[] { "frost_sigil", "frost_breath" }
                : element == "Shade" ? new[] { "night_leap", "black_ink" }
                : new[] { "cut_string", "stitch" };
            if (featured)
            {
                string signatureId = id + "_signature";
                signatureSkills.Add(RegionalSignatureSkill(signatureId, displayName, role, element,
                    region.recommendedLevelMin, region.act, region.accentHex));
                skills = new[] { skills[0], skills[1], signatureId };
            }
            output.Add(new ShadowSpec
            {
                shadowId=id, displayName=displayName, title=$"{region.environment}에 남은 {(featured ? "대표 배역" : "떠도는 잔영")}",
                growthTier=rare ? "Rare" : "Standard", element=element, role=role, shape=shape,
                accentColor=string.IsNullOrEmpty(region.accentHex) ? "#8F7CC9" : "#" + region.accentHex,
                baseHp=Mathf.RoundToInt((85+level*2+(role=="Tank"?35:0)) * (act == 1 && region.recommendedLevelMin <= 5 ? .82f : 1f)),
                baseAtk=Mathf.RoundToInt((18+Mathf.RoundToInt(level*.38f)) * (act == 1 && region.recommendedLevelMin <= 5 ? .76f : 1f)),
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
                sceneName=region.sceneName, minLevel=region.recommendedLevelMin,
                maxLevel=region.recommendedLevelMax, featured=featured });
        }

        private static SkillSpec RegionalSignatureSkill(string id, string ownerName, string role, string element,
            int requiredLevel, int act, string accentHex)
        {
            bool magical = role == "MagicNuker" || role == "Support";
            string status = role == "SpeedUtility" ? "SpeedDown"
                : element == "Flame" ? "Burn" : element == "Frost" ? "Freeze"
                : element == "Shade" ? "DefenseDown" : "Bleed";
            float statusChance = status == "Freeze" ? .22f : role == "Support" ? .55f : .38f;
            string[] motifs = { "", "장막 독백", "잿빛 행진", "봉인 문장", "월광 추적",
                "실끊기 춤", "먹칠 판결", "파도 호명", "별빛 퇴장" };
            string motif = motifs[Mathf.Clamp(act, 1, 8)];
            return new SkillSpec
            {
                skillId=id, displayName=$"{ownerName}의 {motif}",
                description="이 지역에서 되풀이한 고유 배역을 펼쳐 상처와 기억의 흔적을 함께 남긴다.",
                requiredLevel=Mathf.Max(1, requiredLevel), fpCost=2,
                damageType=magical ? "Magical" : "Physical", element=element,
                damageMultiplier=role == "PhysicalDealer" ? 1.7f : role == "MagicNuker" ? 1.62f : 1.38f,
                accuracy=role == "SpeedUtility" ? 1f : .95f,
                bonusCritRate=role == "SpeedUtility" ? .12f : .04f,
                target="Enemy", statusEffect=status, statusChance=statusChance, statusDuration=2,
                primaryFxColor=string.IsNullOrEmpty(accentHex) ? "#8F7CC9" : "#" + accentHex,
                secondaryFxColor="#F4E8FF", ultimateBurstCount=16,
                cameraShake=.32f, hitStopDuration=.035f, sfxVolume=.72f,
                sfxPitch=role == "SpeedUtility" ? 1.12f : .98f
            };
        }

        private static string[] RegionalSuffixes(int act)
        {
            switch (act)
            {
                case 1: return new[] { "장막쥐", "수레까마귀", "웃음나비", "종이배우", "천막여우", "종소리망령" };
                case 2: return new[] { "잿불사냥개", "그을음망령", "부서진창병", "재나비", "불씨까마귀", "왕관벌레" };
                case 3: return new[] { "등불새", "문장벌레", "잉크여우", "페이지나비", "서리올빼미", "유리망령" };
                case 4: return new[] { "가면사슴", "안개늑대", "달빛여우", "절벽박쥐", "수렁뱀", "보랏빛망령" };
                case 5: return new[] { "태엽쥐", "실타래고양이", "가위새", "황동광대", "봉합망령", "톱니벌레" };
                case 6: return new[] { "먹칠까마귀", "봉인벌레", "번호망령", "검열사냥개", "백지나비", "잉크가면" };
                case 7: return new[] { "물결여우", "유리갈매기", "별모래게", "액자해파리", "기억고래", "등대나비" };
                default: return new[] { "무대밖망령", "별빛가면", "침묵관객", "대사잃은새", "장막여우", "공허나비" };
            }
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
            if (catalog.items.Length != 21)
            {
                error = $"도구 수가 올바르지 않습니다: {catalog.items.Length}/21";
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
                skill.forceNeutralElement = spec.forceNeutralElement;
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
                item.isKeyItem = spec.isKeyItem;
                item.isEventOnly = spec.isEventOnly;
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
                : effect == ItemEffectType.GainExperience ? new Color(1f,.70f,.24f,1f)
                : new Color(1f,.76f,.30f,1f);
            if (!string.IsNullOrEmpty(spec.iconStyle))
            {
                if (ColorUtility.TryParseHtmlString(spec.iconColor, out var accent)) liquid = accent;
                DrawExpandedItemIcon(texture, spec.iconStyle, outline, liquid);
            }
            else if (effect == ItemEffectType.TraversalTool)
            {
                if (spec.itemId == TraversalAbilityService.FrostwingCompassItemId)
                {
                    Circle(texture,32,32,22,outline); Circle(texture,32,32,17,liquid);
                    Line(texture,23,43,39,19,6,outline); Line(texture,25,41,37,22,3,Color.white);
                    Line(texture,18,32,46,32,2,outline);
                }
                else if (spec.itemId == TraversalAbilityService.SpiritProcessionLanternItemId)
                {
                    Rect(texture,18,17,46,49,outline); Rect(texture,22,21,42,45,liquid);
                    Line(texture,22,17,27,10,4,outline); Line(texture,42,17,37,10,4,outline);
                    Rect(texture,26,8,38,13,outline); Circle(texture,32,33,8,Color.white);
                }
                else if (spec.itemId == TraversalAbilityService.HeartWindingKeyItemId)
                {
                    Circle(texture,22,36,12,outline); Circle(texture,22,36,6,liquid);
                    Line(texture,31,31,52,18,8,outline); Line(texture,32,31,51,20,4,liquid);
                    Rect(texture,45,12,54,22,outline); Rect(texture,39,17,47,27,outline);
                }
                else
                {
                    // 접힌 고무보트가 아니라 가방에서 막 펼친 작은 보트로 읽히는 전용 픽셀 아이콘.
                    Ellipse(texture,32,28,27,17,outline); Ellipse(texture,32,30,22,12,liquid);
                    Rect(texture,10,27,54,35,outline); Rect(texture,15,29,49,34,liquid);
                    Rect(texture,19,35,45,40,outline); Rect(texture,22,36,42,39,new Color(1f,.9f,.5f,1f));
                    Line(texture,18,19,48,45,4,outline); Line(texture,19,20,49,46,2,Color.white);
                }
            }
            else if(effect==ItemEffectType.GainExperience)
            {
                Rect(texture,13,10,51,55,outline);Rect(texture,17,14,47,51,liquid);
                Line(texture,32,14,32,51,3,outline);Line(texture,21,42,28,42,2,Color.white);
                Line(texture,36,42,43,42,2,Color.white);Circle(texture,32,29,6,Color.white);
            }
            else
            {
                Ellipse(texture,32,25,20,22,outline); Rect(texture,25,42,39,55,outline); Rect(texture,23,53,41,59,outline);
                Ellipse(texture,32,23,15,16,liquid); Rect(texture,29,39,35,48,liquid);
                Rect(texture,27,55,37,60,new Color(.78f,.58f,.28f,1f));
                if (spec.itemId.Contains("greater") || spec.itemId.Contains("silver"))
                { Circle(texture,20,42,3,Color.white); Circle(texture,45,34,2,Color.white); }
            }
            texture.Apply(false,false); File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer!=null){importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=64f;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void DrawExpandedItemIcon(Texture2D texture, string style, Color ink, Color fill)
        {
            switch (style)
            {
                case "biscuit":
                    Rect(texture,10,13,54,51,ink); Rect(texture,14,17,50,47,fill);
                    Line(texture,15,32,49,32,2,ink);
                    foreach (int x in new[] { 22, 41 }) foreach (int y in new[] { 24, 40 })
                        Circle(texture,x,y,3,ink);
                    break;
                case "flask":
                    Rect(texture,19,8,45,46,ink); Rect(texture,23,12,41,42,fill);
                    Rect(texture,24,45,40,56,ink); Rect(texture,27,54,37,60,fill);
                    Line(texture,27,17,27,36,2,Color.white);
                    break;
                case "candy":
                    Triangle(texture,7,18,7,46,24,32,ink); Triangle(texture,57,18,57,46,40,32,ink);
                    Triangle(texture,10,23,10,41,22,32,fill); Triangle(texture,54,23,54,41,42,32,fill);
                    Circle(texture,32,32,17,ink); Circle(texture,32,32,12,fill);
                    Line(texture,26,38,38,26,3,Color.white);
                    break;
                case "ink":
                    Rect(texture,13,9,43,37,ink); Rect(texture,17,13,39,33,fill);
                    Rect(texture,18,37,38,45,ink); Rect(texture,22,20,34,27,Color.white);
                    Line(texture,36,19,52,54,4,ink); Triangle(texture,42,44,52,59,57,44,fill);
                    break;
                case "cup":
                    Circle(texture,46,30,12,ink); Circle(texture,46,30,7,Color.clear);
                    Rect(texture,10,16,43,43,ink); Rect(texture,14,20,39,39,fill);
                    Line(texture,9,12,47,12,4,ink); Line(texture,21,46,19,56,3,fill);
                    Line(texture,31,47,34,59,3,fill); break;
                case "elixir":
                    Circle(texture,32,26,21,ink); Circle(texture,32,26,16,fill);
                    Rect(texture,25,43,39,55,ink); Rect(texture,23,54,41,60,fill);
                    Line(texture,25,26,39,26,4,Color.white); Line(texture,32,19,32,33,4,Color.white);
                    break;
                case "notes":
                    Rect(texture,11,10,49,55,ink); Rect(texture,15,14,45,51,fill);
                    for (int y=23;y<=44;y+=7) Line(texture,21,y,38,y,2,ink);
                    Line(texture,42,13,55,45,5,ink); Line(texture,43,15,54,43,2,Color.white);
                    break;
                case "script":
                    Rect(texture,10,10,54,54,ink); Rect(texture,14,14,50,50,fill);
                    Line(texture,31,15,31,49,3,ink); Rect(texture,38,10,44,43,new Color(.75f,.25f,.32f));
                    Circle(texture,22,33,5,Color.white); break;
                default: throw new InvalidOperationException("Unknown item icon style: " + style);
            }
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
                shadow.silhouetteSprite = LoadFinalPortrait(spec.shadowId) ?? GenerateProceduralPortrait(spec);
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
                var restoredArt = LoadFinalPortrait(spec.shadowId + "_restored");
                if (restoredArt != null) shadow.restoredForm.silhouetteSprite = restoredArt;
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
            target.enabled = spec != null && !string.IsNullOrWhiteSpace(spec.formName);
            target.silhouetteSprite = null; // 전용 아트가 없으면 기본 실루엣과 포인트 컬러를 사용한다.
            target.bonusSkills = new List<SkillData>();
            if (!target.enabled) return;
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

        private static Sprite GenerateProceduralPortrait(ShadowSpec spec)
        {
            string path = $"{ArtFolder}/{spec.shadowId}_procedural.png";
            if (!File.Exists(path))
            {
                const int size = 192;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = Enumerable.Repeat(Color.clear, size * size).ToArray();
                texture.SetPixels(pixels);
                Color accent = ParseColor(spec.accentColor, new Color(.62f, .35f, 1f, 1f));
                DrawSilhouette(texture, spec.shape, new Color(.035f, .025f, .075f, 1f));
                DecorateProceduralPortrait(texture, spec, accent);
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
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
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

        private static void DrawSilhouette(Texture2D texture, string shape, Color ink)
        {
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

        private static void DecorateProceduralPortrait(Texture2D texture, ShadowSpec spec, Color accent)
        {
            Color glow = Color.Lerp(accent, Color.white, .55f);
            Color shade = Color.Lerp(accent, Color.black, .48f);
            int leftEye = 90, rightEye = 102, eyeY = 141;
            switch (spec.shape)
            {
                case "Knight": leftEye = 80; rightEye = 92; eyeY = 139; break;
                case "Mage": leftEye = 91; rightEye = 101; eyeY = 129; break;
                case "Beast": leftEye = 123; rightEye = 135; eyeY = 108; break;
                case "Puppet": leftEye = 90; rightEye = 102; eyeY = 142; break;
                case "Crow": leftEye = 90; rightEye = 101; eyeY = 107; break;
                case "Mask": leftEye = 72; rightEye = 120; eyeY = 111; break;
            }

            Circle(texture, leftEye, eyeY, 5, shade);
            Circle(texture, rightEye, eyeY, 5, shade);
            Rect(texture, leftEye - 2, eyeY - 1, leftEye + 2, eyeY + 1, glow);
            Rect(texture, rightEye - 2, eyeY - 1, rightEye + 2, eyeY + 1, glow);

            int seed = StablePortraitHash(spec.shadowId);
            for (int i = 0; i < 5; i++)
            {
                int x = 70 + ((seed >> (i * 3)) & 7) * 7;
                int y = 51 + i * 11;
                Rect(texture, x, y, x + 3 + (i & 1), y + 3, i % 2 == 0 ? accent : shade);
            }

            ShadowRole role = Parse(spec.role, ShadowRole.PhysicalDealer);
            if (role == ShadowRole.PhysicalDealer)
            {
                Line(texture, 132, 45, 158, 126, 4, accent);
                Triangle(texture, 151, 126, 166, 139, 156, 112, glow);
            }
            else if (role == ShadowRole.MagicNuker || role == ShadowRole.Support)
            {
                Circle(texture, 151, 88, 11, shade);
                Circle(texture, 151, 88, 5, glow);
                Line(texture, 151, 45, 151, 78, 4, accent);
            }
            else if (role == ShadowRole.SpeedUtility)
            {
                Triangle(texture, 39, 72, 68, 86, 46, 101, accent);
                Triangle(texture, 153, 72, 124, 86, 146, 101, accent);
            }
            else if (role == ShadowRole.Tank)
            {
                Rect(texture, 37, 67, 52, 125, shade);
                Rect(texture, 40, 71, 48, 121, accent);
            }

            GrowthTier tier = Parse(spec.growthTier, GrowthTier.Standard);
            if (tier == GrowthTier.RegionalBoss || tier == GrowthTier.Legendary)
            {
                Triangle(texture, 72, 159, 84, 159, 78, 181, accent);
                Triangle(texture, 88, 163, 100, 163, 94, 187, glow);
                Triangle(texture, 104, 159, 116, 159, 110, 181, accent);
            }
        }

        private static int StablePortraitHash(string value)
        {
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
                return hash & int.MaxValue;
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
            var currentShadows = shadows.ToList();
            var currentIds = new HashSet<string>(currentShadows.Select(x => x.shadowId), StringComparer.Ordinal);
            if (File.Exists(LegendaryCatalogPath))
            {
                var legendary = JsonUtility.FromJson<CoreCatalog>(File.ReadAllText(LegendaryCatalogPath));
                foreach (ShadowSpec shadow in legendary?.shadows ?? Array.Empty<ShadowSpec>())
                    currentIds.Add(shadow.shadowId);
            }
            // 이전 지역 로스터의 생성 에셋은 보존하되 현재 데이터베이스에서는 제외한다.
            database.shadows.RemoveAll(shadow => shadow == null ||
                (!currentIds.Contains(shadow.shadowId) && AssetDatabase.GetAssetPath(shadow)
                    .StartsWith(ShadowFolder + "/", StringComparison.Ordinal)));
            foreach (var skill in skills) AddOrReplace(database.skills, skill, x => x.skillId);
            foreach (var item in items) AddOrReplace(database.items, item, x => x.itemId);
            foreach (var shadow in currentShadows) AddOrReplace(database.shadows, shadow, x => x.shadowId);
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
            Mathf.Clamp(configured > 0 ? configured : (ultimate ? 1 : fpCost <= 1 ? 1 : fpCost == 2 ? 5 : fpCost == 3 ? 12 : 20), 1, ShadowInstance.MaxLevel);

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
            public bool isUltimate, forceNeutralElement;
        }
        [Serializable] private class ItemSpec
        {
            public string itemId, displayName, description, effectType, iconStyle, iconColor;
            public float value;
            public int buyPrice, sellPrice, shopUnlockAct = 1;
            public bool usableInBattle, usableInField, isKeyItem, isEventOnly;
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
            public bool isSettlement, isBossStage;
            public string[] featuredShadows;
        }
        [Serializable] private class RegionalRosterManifest { public List<RegionalRosterEntry> entries; }
        [Serializable] private class RegionalRosterEntry
        {
            public string shadowId, regionId, sceneName;
            public int minLevel, maxLevel;
            public bool featured;
        }
    }
}
#endif
