// Only native scene/resource/save bridges are simulated. The tracking, guidance,
// repositories and route/knowledge code are the actual shipping sources.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)] public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){} }
    public class Object
    {
        public static List<object> Scene = new List<object>();
        public static T[] FindObjectsOfType<T>() => Scene.OfType<T>().ToArray();
    }
    public class Transform { public Vector3 position; }
    public class TextAsset { public string text; }
    public static class Resources { public static T Load<T>(string path) where T:class => new TextAsset { text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,path+".json")) } as T; }
    public static class JsonUtility { public static T FromJson<T>(string s)=>JsonSerializer.Deserialize<T>(s,new JsonSerializerOptions{IncludeFields=true}); }
    public static class Debug { public static void LogError(string s)=>throw new Exception(s); public static void LogWarning(string s)=>throw new Exception(s); }
    public struct Vector3
    {
        public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
    }
    public struct Vector2
    {
        public float x,y;public float sqrMagnitude=>x*x+y*y;
        public static implicit operator Vector2(Vector3 v)=>new Vector2{x=v.x,y=v.y};
    }
    public struct Vector2Int {public static Vector2Int zero=>new Vector2Int(0,0);public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}}
    public struct Color {public Color(float r,float g,float b,float a=1){}}
    public static class ColorUtility {public static bool TryParseHtmlString(string s,out Color c){c=new Color();return false;}}
    public static class Mathf {public static float Abs(float f)=>Math.Abs(f);}
}
namespace ShadowTheater.Save
{
    public class QuestProgressData {public string questId;public bool completed;public List<QuestObjectiveProgressData> objectives=new List<QuestObjectiveProgressData>();}
    public class QuestObjectiveProgressData {public string objectiveId;public int current;}
    public class SaveData {public string mapId,trackedQuestId;public List<QuestProgressData> quests=new List<QuestProgressData>();public List<string> visitedRegionIds=new List<string>(),unlockedRegionIds=new List<string>();}
    public static class SaveManager
    {
        public static SaveData Current;public static HashSet<string> Flags=new HashSet<string>();
        public static bool HasFlag(string s)=>s!=null&&Flags.Contains(s);public static void SetFlag(string s)=>Flags.Add(s);
    }
}
namespace ShadowTheater.Field
{
    public class FieldGrid {public static FieldGrid Current;public UnityEngine.Vector3 CellToWorld(UnityEngine.Vector2Int c)=>new UnityEngine.Vector3(c.x+.5f,c.y+.5f,0);}
    public class PlayerController {public static PlayerController Instance=new PlayerController();public UnityEngine.Transform transform=new UnityEngine.Transform();}
    public class MapPortal {public string DestinationScene;public bool CanGuideTravel;public UnityEngine.Transform transform=new UnityEngine.Transform();}
}
namespace ShadowTheater.Story
{
    public static class TownServicePolicy {public static bool ShowRumor;public static bool CanShowHiddenHint(RegionData r)=>ShowRumor;}
    public class StoryNpc {public string NpcId;public UnityEngine.Transform transform=new UnityEngine.Transform();}
    public class BossEncounterTrigger {public string EncounterId;public UnityEngine.Transform transform=new UnityEngine.Transform();}
}
namespace ShadowTheater.UI { public static class L10n {public static string Text(string s)=>s;}}

namespace UnityEngine
{
 public class ScriptableObject : Object {}
 public class Sprite {}
 [System.AttributeUsage(System.AttributeTargets.Class)] public class CreateAssetMenuAttribute:System.Attribute {public string fileName,menuName;public int order;}
 [System.AttributeUsage(System.AttributeTargets.Field)] public class TextAreaAttribute:System.Attribute {}
 [System.AttributeUsage(System.AttributeTargets.Field)] public class TooltipAttribute:System.Attribute {public TooltipAttribute(string value){}}
 [System.AttributeUsage(System.AttributeTargets.Field)] public class MinAttribute:System.Attribute {public MinAttribute(float value){}}
 [System.AttributeUsage(System.AttributeTargets.Field)] public class RangeAttribute:System.Attribute {public RangeAttribute(float min,float max){}}
}
