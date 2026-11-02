#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace ShadowTheater.EditorTools
{
    /// <summary>Focused presentation export. Run in an isolated validation project.</summary>
    public static class PresentationPolishPreview
    {
        public static void ExportForCi()
        {
            const string folder="Docs/Generated/SkillParts128";Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);

            var notes=new System.Text.StringBuilder();int checkedSounds=0;
            SkillPartLibrary.Preload();
            if(SkillPartLibrary.SpriteCount!=128)throw new Exception("Expected 128 imported painted sprites");
            SkillPartLibrary.ResetUsage();
            var skills=AssetDatabase.FindAssets("t:SkillData",new[]{"Assets/Data/Generated/Skills"})
                .Select(guid=>AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToDictionary(skill=>skill.skillId);
            foreach(string path in Directory.GetFiles("Assets/Resources/Data","*Catalog.json"))
            {
                var catalog=JsonUtility.FromJson<PreviewCatalog>(File.ReadAllText(path));
                if(catalog.skills==null)continue;
                foreach(var spec in catalog.skills)
                {
                    if(skills.ContainsKey(spec.skillId))continue;
                    var skill=ScriptableObject.CreateInstance<SkillData>();skill.skillId=spec.skillId;skill.displayName=spec.displayName;
                    skill.element=(ShadowElement)Enum.Parse(typeof(ShadowElement),spec.element??"None");
                    skill.damageType=(DamageType)Enum.Parse(typeof(DamageType),spec.damageType??"None");
                    skill.target=(SkillTarget)Enum.Parse(typeof(SkillTarget),spec.target??"Enemy");
                    skill.fpCost=spec.fpCost;skill.healRatio=spec.healRatio;skill.isUltimate=spec.isUltimate;
                    skill.ultimateFxStyle=(UltimateFxStyle)Enum.Parse(typeof(UltimateFxStyle),spec.ultimateFxStyle??"None");
                    if(ColorUtility.TryParseHtmlString(spec.primaryFxColor,out Color primary))skill.primaryFxColor=primary;
                    if(ColorUtility.TryParseHtmlString(spec.secondaryFxColor,out Color secondary))skill.secondaryFxColor=secondary;
                    skills.Add(spec.skillId,skill);
                }
            }
            foreach(var skill in skills.Values)
            {
                var profile=SkillPresentationProfile.For(skill);
                foreach(bool impact in new[]{false,true})
                {
                    float[] data=SkillSoundSynthesis.Samples(profile,impact,skill.target==SkillTarget.Self && skill.healRatio>0);
                    if(data.Any(x=>float.IsNaN(x)||float.IsInfinity(x)||Mathf.Abs(x)>.99f)||data.Max(x=>Mathf.Abs(x))<.01f)
                        throw new Exception("Invalid sound "+skill.skillId);
                    checkedSounds++;
                }
                var art=profile.choreography.art;
                if(!art.IsComplete || new[]{art.charge,art.travel,art.impact,art.debris,art.crest}.Any(id=>SkillPartLibrary.Get(id)==null))
                    throw new Exception("Missing painted art assignment: "+skill.skillId);
                if(!profile.choreography.authored)throw new Exception("No authored direction: "+skill.skillId);
                notes.AppendLine(skill.skillId+": "+profile.VisualKey+"; art="+string.Join(",",new[]{art.charge,art.travel,art.impact,art.debris,art.crest}));
            }
            notes.AppendLine("Validated non-silent bounded sound waveforms="+checkedSounds);
            var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=360;
            camera.transform.position=new Vector3(0,0,-100);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.02f,.045f);
            var rt=new RenderTexture(960,540,24);camera.targetTexture=rt;
            var canvas=new GameObject("Preview",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
            var rect=(RectTransform)canvas.transform;
            var bg=Image(rect,"Backdrop",Vector2.zero,new Vector2(960,540));bg.sprite=Resources.Load<Sprite>("Art/BattleBackgrounds/Theater");
            var left=Panel(rect,new Vector2(-270,-50),"knight");var right=Panel(rect,new Vector2(225,35),"beast");
            string[][] pages={
                new[]{"ember_strike","fallen_crown","guardian_oath","cut_string","puppet_string_reversal","stitch","frost_sigil","zero_prophecy","memory_sanctuary"},
                new[]{"claw","blood_fang","homeward_howl","crow_dawn_beacon","crow_sky_cremation","eclipse_widow","cosmic_final_ovation","outside_the_script","devouring_auditorium"},
                new[]{"bloodmane_stalker_salvation_ultimate","bloodmane_stalker_grudge_ultimate","book_drake_salvation_ultimate","wire_primadonna_grudge_ultimate","outside_script_shadow_grudge_ultimate","exp_clockwork_heart_stage_01_signature","exp_moon_eater_ritual_01_signature","exp_stitch_field_01_signature","exp_widow_moon_palace_02_signature"}
            };
            int roleComparisons=pages.Sum(page=>page.Length);
            float[] phases={.16f,.38f,.65f,.84f};
            // Compare actual rendered frames with a fixed palette; labels and skill names are absent.
            var auditTarget=new RenderTexture(320,180,24);var auditFrame=new Texture2D(320,180,TextureFormat.RGB24,false);
            camera.targetTexture=auditTarget;canvas.scaleFactor=1f/3f;
            var fingerprints=new System.Collections.Generic.Dictionary<string,string>();
            using(var sha=System.Security.Cryptography.SHA256.Create())
            foreach(var entry in skills.Values)
            {
                var copy=UnityEngine.Object.Instantiate(entry);
                copy.element=ShadowElement.None;copy.primaryFxColor=copy.secondaryFxColor=Color.white;
                copy.fpCost=copy.isUltimate?4:copy.target==SkillTarget.Self?3:copy.fpCost==0?0:2;
                var layer=new GameObject("AuditFX",typeof(RectTransform)).GetComponent<RectTransform>();layer.SetParent(rect,false);
                layer.anchorMin=Vector2.zero;layer.anchorMax=Vector2.one;layer.offsetMin=layer.offsetMax=Vector2.zero;
                Canvas.ForceUpdateCanvases();var fingerprint=new System.Text.StringBuilder();int impacts=0;
                using(var sequence=new BattleAttackPresentation(layer,left,right,copy,false,()=>impacts++))
                {
                    foreach(float progress in new[]{.32f,.66f,.84f})
                    {
                        sequence.Sample(progress);Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=auditTarget;
                        auditFrame.ReadPixels(new Rect(0,0,320,180),0,0);auditFrame.Apply();
                        fingerprint.Append(Convert.ToBase64String(sha.ComputeHash(auditFrame.GetRawTextureData())));
                    }
                    sequence.Sample(1);sequence.Sample(1);
                    if(impacts!=1)throw new Exception("Contact not delivered once: "+entry.skillId);
                }
                // Exercise all artwork stages without battle simulation or changing game rules.
                var artLayer=new GameObject("ArtStages",typeof(RectTransform)).GetComponent<RectTransform>();artLayer.SetParent(rect,false);
                artLayer.anchorMin=Vector2.zero;artLayer.anchorMax=Vector2.one;artLayer.offsetMin=artLayer.offsetMax=Vector2.zero;
                var artProfile=SkillPresentationProfile.For(copy);
                using(var effects=new SkillSignatureFx(artLayer,artProfile,new Vector2(-270,-50),
                    copy.target==SkillTarget.Self?new Vector2(-270,-50):new Vector2(225,35),false,copy.target==SkillTarget.Self))
                {
                    if(artProfile.IsUltimate)effects.SamplePrelude(.5f);
                    foreach(float progress in new[]{.12f,.25f,.42f,.58f,.72f,.9f})effects.Sample(progress);
                }
                UnityEngine.Object.DestroyImmediate(artLayer.gameObject);
                string key=fingerprint.ToString();
                if(fingerprints.TryGetValue(key,out string other))throw new Exception("Identical fixed-palette rendered sequence: "+other+" / "+entry.skillId);
                fingerprints.Add(key,entry.skillId);UnityEngine.Object.DestroyImmediate(copy);
            }
            notes.AppendLine("Distinct fixed-palette rendered sequences="+fingerprints.Count+" (3 frames per skill, labels absent)");
            RenderTexture.active=null;camera.targetTexture=rt;canvas.scaleFactor=1;
            auditTarget.Release();UnityEngine.Object.DestroyImmediate(auditTarget);UnityEngine.Object.DestroyImmediate(auditFrame);
            Canvas.ForceUpdateCanvases();
            string[] unused=SkillPartLibrary.Definitions.Select(part=>part.id).Except(SkillPartLibrary.UsedIds).ToArray();
            if(unused.Length>0)throw new Exception("Painted sprites never drawn by any skill: "+string.Join(",",unused));
            notes.AppendLine("Painted sprites imported=128; assigned=128; exercised by enabled draw calls="+SkillPartLibrary.UsedIds.Count);
            int pageNumber=0;
            foreach(string[] ids in pages)
            {
            var sheet=new Texture2D(960*phases.Length,540*ids.Length,TextureFormat.RGB24,false);
            var frame=new Texture2D(960,540,TextureFormat.RGB24,false);
            for(int row=0;row<ids.Length;row++)
            {
                var skill=skills.TryGetValue(ids[row],out SkillData found)?found:null;
                if(skill==null)throw new Exception("Missing preview skill "+ids[row]);
                var title=new GameObject("Label",typeof(RectTransform),typeof(Text)).GetComponent<Text>();title.transform.SetParent(rect,false);
                title.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");title.fontSize=23;title.alignment=TextAnchor.MiddleCenter;
                title.rectTransform.anchoredPosition=new Vector2(0,220);title.rectTransform.sizeDelta=new Vector2(900,50);
                title.text=ids[row]+" / "+SkillPresentationProfile.For(skill).kind;
                var layer=new GameObject("FX",typeof(RectTransform)).GetComponent<RectTransform>();layer.SetParent(rect,false);
                layer.anchorMin=Vector2.zero;layer.anchorMax=Vector2.one;layer.offsetMin=layer.offsetMax=Vector2.zero;
                Canvas.ForceUpdateCanvases();int impacts=0;Vector3 home=left.MotionRoot.localPosition;
                using(var sequence=new BattleAttackPresentation(layer,left,right,skill,false,()=>impacts++))
                {
                    for(int col=0;col<phases.Length;col++)
                    {
                        sequence.Sample(phases[col]);Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                        frame.ReadPixels(new Rect(0,0,960,540),0,0);frame.Apply();
                        sheet.SetPixels(col*960,(ids.Length-1-row)*540,960,540,frame.GetPixels());
                    }
                    sequence.Sample(1);sequence.Sample(1);
                    if(impacts!=1)throw new Exception("Impact callback count "+impacts);
                }
                if(Vector3.Distance(home,left.MotionRoot.localPosition)>.01f)throw new Exception("Actor did not restore");
                UnityEngine.Object.DestroyImmediate(title.gameObject);
            }
            sheet.Apply();File.WriteAllBytes(folder+"/skill-sequences-"+(++pageNumber)+".png",sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(frame);UnityEngine.Object.DestroyImmediate(sheet);
            }
            notes.AppendLine(roleComparisons+" role comparison sequences; skipped-frame impact delivered once; actor transforms restored.");
            File.WriteAllText(folder+"/checks.txt",notes.ToString());
            RenderTexture.active=null;camera.targetTexture=null;rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log("[PresentationPolish] PASS: "+checkedSounds+" sounds; "+skills.Count+" distinct sequences; 128 painted sprites; "+roleComparisons+" role comparisons");
        }
        [Serializable] private class PreviewCatalog { public PreviewSkill[] skills; }
        [Serializable] private class PreviewSkill
        {
            public string skillId,displayName,element,damageType,target,ultimateFxStyle,primaryFxColor,secondaryFxColor;
            public int fpCost;public float healRatio;public bool isUltimate;
        }
        private static Image Image(Transform parent,string name,Vector2 pos,Vector2 size)
        {
            var image=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();image.transform.SetParent(parent,false);
            image.rectTransform.anchoredPosition=pos;image.rectTransform.sizeDelta=size;image.raycastTarget=false;return image;
        }
        private static BattleUnitPanel Panel(Transform parent,Vector2 pos,string portrait)
        {
            var root=new GameObject("Unit",typeof(RectTransform)).GetComponent<RectTransform>();root.SetParent(parent,false);
            root.anchoredPosition=pos;root.sizeDelta=new Vector2(200,230);
            var image=Image(root,"Portrait",Vector2.zero,new Vector2(200,230));image.preserveAspect=true;
            image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Final/Portraits/"+portrait+".png");
            var panel=root.gameObject.AddComponent<BattleUnitPanel>();var data=new SerializedObject(panel);
            data.FindProperty("portrait").objectReferenceValue=image;data.ApplyModifiedPropertiesWithoutUndo();return panel;
        }
    }
}
#endif
