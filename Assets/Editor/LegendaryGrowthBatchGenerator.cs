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
    /// <summary>
    /// LegendaryGrowthCatalog.json과 4열 성장 시트를 실제 SkillData/ShadowData로 변환한다.
    /// 같은 ID의 에셋은 덮어써서 데이터 조정 후 안전하게 다시 실행할 수 있다.
    /// </summary>
    public static class LegendaryGrowthBatchGenerator
    {
        private const string CatalogPath = "Assets/Resources/Data/LegendaryGrowthCatalog.json";
        private const string SkillFolder = "Assets/Data/Generated/Skills";
        private const string ShadowFolder = "Assets/Data/Generated/Shadows";
        private const string DatabasePath = "Assets/Resources/ShadowDatabase.asset";
        private static readonly string[] FormKeys = { "echo", "restored", "salvation", "grudge" };

        [MenuItem("Tools/Shadow Theater/Generate Legendary Growth Batch")]
        public static void Generate()
        {
            if (!File.Exists(CatalogPath))
            {
                Debug.LogError($"[GrowthBatch] 카탈로그가 없습니다: {CatalogPath}");
                return;
            }

            var catalog = JsonUtility.FromJson<GrowthCatalog>(File.ReadAllText(CatalogPath));
            if (catalog?.skills == null || catalog.shadows == null || catalog.shadows.Length == 0)
            {
                Debug.LogError("[GrowthBatch] 성장 카탈로그 형식이 올바르지 않습니다.");
                return;
            }

            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Generated");
            EnsureFolder("Assets/Data/Generated", "Skills");
            EnsureFolder("Assets/Data/Generated", "Shadows");

            var skills = GenerateSkills(catalog.skills);
            var shadows = new List<ShadowData>();
            foreach (var spec in catalog.shadows)
            {
                SliceGrowthSheet(spec);
                var shadow = GenerateShadow(spec, skills);
                if (shadow != null) shadows.Add(shadow);
            }

            UpdateDatabase(skills.Values, shadows);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<ShadowData>($"{ShadowFolder}/{catalog.shadows[0].shadowId}.asset");
            Debug.Log($"[GrowthBatch] 성장 데이터 생성 완료: 그림자 {shadows.Count}종, 스킬 {skills.Count}개");
        }

        private static Dictionary<string, SkillData> GenerateSkills(IEnumerable<SkillSpec> specs)
        {
            var result = new Dictionary<string, SkillData>();
            foreach (var spec in specs)
            {
                if (string.IsNullOrWhiteSpace(spec.skillId)) continue;
                string path = $"{SkillFolder}/{spec.skillId}.asset";
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                if (skill == null)
                {
                    skill = ScriptableObject.CreateInstance<SkillData>();
                    AssetDatabase.CreateAsset(skill, path);
                }

                skill.name = spec.skillId;
                skill.skillId = spec.skillId;
                skill.displayName = spec.displayName;
                skill.description = spec.description;
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

        private static ShadowData GenerateShadow(ShadowSpec spec, IReadOnlyDictionary<string, SkillData> skills)
        {
            if (string.IsNullOrWhiteSpace(spec.shadowId)) return null;
            string path = $"{ShadowFolder}/{spec.shadowId}.asset";
            var shadow = AssetDatabase.LoadAssetAtPath<ShadowData>(path);
            if (shadow == null)
            {
                shadow = ScriptableObject.CreateInstance<ShadowData>();
                AssetDatabase.CreateAsset(shadow, path);
            }

            shadow.name = spec.shadowId;
            var sprites = AssetDatabase.LoadAllAssetsAtPath(spec.sheetPath).OfType<Sprite>()
                .ToDictionary(x => x.name, x => x);
            shadow.shadowId = spec.shadowId;
            shadow.displayName = spec.displayName;
            shadow.title = spec.title;
            shadow.growthTier = Parse(spec.growthTier, GrowthTier.Standard);
            shadow.element = Parse(spec.element, ShadowElement.None);
            shadow.role = Parse(spec.role, ShadowRole.PhysicalDealer);
            shadow.silhouetteSprite = SpriteFor(sprites, spec.shadowId, "echo");
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
            shadow.loreLocked = spec.loreLocked;
            shadow.loreUnlocked = spec.loreUnlocked;
            ApplyForm(shadow.restoredForm, spec.restored, SpriteFor(sprites, spec.shadowId, "restored"), skills);
            ApplyForm(shadow.salvationForm, spec.salvation, SpriteFor(sprites, spec.shadowId, "salvation"), skills);
            ApplyForm(shadow.grudgeForm, spec.grudge, SpriteFor(sprites, spec.shadowId, "grudge"), skills);
            EditorUtility.SetDirty(shadow);
            return shadow;
        }

        private static void ApplyForm(MemoryFormData target, FormSpec spec, Sprite sprite,
                                      IReadOnlyDictionary<string, SkillData> skills)
        {
            target.enabled = spec != null;
            if (spec == null) return;
            target.formName = spec.formName;
            target.silhouetteSprite = sprite;
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

        private static void SliceGrowthSheet(ShadowSpec spec)
        {
            var importer = AssetImporter.GetAtPath(spec.sheetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[GrowthBatch] 이미지가 없습니다: {spec.sheetPath}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = 256f;
            importer.npotScale = TextureImporterNPOTScale.None;

            // Unity 2022 validates legacy SpriteMetaData against the currently imported
            // texture size. That size can be lower than the PNG source size because of a
            // platform/max-size setting, so use the imported texture as the authority.
            var importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.sheetPath);
            if (importedTexture == null)
            {
                Debug.LogError($"[GrowthBatch] 이미지를 불러올 수 없습니다: {spec.sheetPath}");
                return;
            }

            int importedWidth = importedTexture.width;
            int importedHeight = importedTexture.height;
            if (importedWidth <= FormKeys.Length || importedHeight < 1)
            {
                Debug.LogError($"[GrowthBatch] 이미지 크기가 올바르지 않습니다: {spec.sheetPath} ({importedWidth}x{importedHeight})");
                return;
            }

            var metadata = new SpriteMetaData[4];
            for (int i = 0; i < metadata.Length; i++)
            {
                // Integer boundaries also support sheets whose width is not divisible by four.
                int left = importedWidth * i / metadata.Length;
                int right = importedWidth * (i + 1) / metadata.Length;
                // Some Unity 2022 patch releases reject a final rect whose right edge is
                // exactly equal to texture.width. One transparent edge pixel is expendable.
                if (i == metadata.Length - 1) right = Mathf.Min(right, importedWidth - 1);
                metadata[i] = new SpriteMetaData
                {
                    name = $"{spec.shadowId}_{FormKeys[i]}",
                    alignment = (int)SpriteAlignment.BottomCenter,
                    pivot = new Vector2(0.5f, 0f),
                    rect = new Rect(left, 0, right - left, importedHeight)
                };
            }
#pragma warning disable 0618
            importer.spritesheet = metadata;
#pragma warning restore 0618
            importer.SaveAndReimport();

            var generatedNames = new HashSet<string>(
                AssetDatabase.LoadAllAssetsAtPath(spec.sheetPath).OfType<Sprite>().Select(sprite => sprite.name));
            var missingForms = FormKeys.Where(form => !generatedNames.Contains($"{spec.shadowId}_{form}")).ToArray();
            if (missingForms.Length > 0)
            {
                Debug.LogError($"[GrowthBatch] 스프라이트 분할 실패: {spec.sheetPath} / {string.Join(", ", missingForms)}");
            }
        }

        private static void UpdateDatabase(IEnumerable<SkillData> skills, IEnumerable<ShadowData> shadows)
        {
            var database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<ShadowDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }
            foreach (var skill in skills)
            {
                int index = database.skills.FindIndex(x => x != null && x.skillId == skill.skillId);
                if (index >= 0) database.skills[index] = skill; else database.skills.Add(skill);
            }
            foreach (var shadow in shadows)
            {
                int index = database.shadows.FindIndex(x => x != null && x.shadowId == shadow.shadowId);
                if (index >= 0) database.shadows[index] = shadow; else database.shadows.Add(shadow);
            }
            EditorUtility.SetDirty(database);
        }

        private static Sprite SpriteFor(IReadOnlyDictionary<string, Sprite> sprites, string id, string form) =>
            sprites.TryGetValue($"{id}_{form}", out var sprite) ? sprite : null;

        private static SkillData SkillFor(IReadOnlyDictionary<string, SkillData> skills, string id) =>
            !string.IsNullOrEmpty(id) && skills.TryGetValue(id, out var skill) ? skill : null;

        private static List<SkillData> SkillsFor(IReadOnlyDictionary<string, SkillData> skills, string[] ids) =>
            ids == null ? new List<SkillData>() : ids.Select(x => SkillFor(skills, x)).Where(x => x != null).ToList();

        private static T Parse<T>(string value, T fallback) where T : struct =>
            Enum.TryParse(value, true, out T result) ? result : fallback;

        private static Color ParseColor(string html, Color fallback) =>
            !string.IsNullOrEmpty(html) && ColorUtility.TryParseHtmlString(html, out var color) ? color : fallback;

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}")) AssetDatabase.CreateFolder(parent, child);
        }

        [Serializable] private class GrowthCatalog { public SkillSpec[] skills; public ShadowSpec[] shadows; }
        [Serializable] private class SkillSpec
        {
            public string skillId, displayName, description, damageType, element, target, statusEffect;
            public string ultimateFxStyle, primaryFxColor, secondaryFxColor;
            public int fpCost, fpGain, statusDuration;
            public int ultimateBurstCount;
            public float damageMultiplier, accuracy, bonusCritRate, healRatio, statusChance, cameraShake;
            public float sfxVolume, sfxPitch, hitStopDuration;
            public bool isUltimate;
        }
        [Serializable] private class ShadowSpec
        {
            public string shadowId, displayName, title, growthTier, element, role, sheetPath, accentColor;
            public int baseHp, baseAtk, baseDef, baseSpd, expReward, goldReward;
            public float critRate, evasion, hpGrowth, atkGrowth, defGrowth, spdGrowth, baseCaptureRate;
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
    }
}
#endif
