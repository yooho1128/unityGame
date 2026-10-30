// Native Unity resource/serialization/application/save bridges only. Policies and file service are shipping code.
using System;
using System.IO;
using System.Text.Json;
namespace UnityEngine
{
    public enum RuntimeInitializeLoadType{SubsystemRegistration}
    [AttributeUsage(AttributeTargets.Method)]public sealed class RuntimeInitializeOnLoadMethodAttribute:Attribute{public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type){}}
    public sealed class TextAsset{public string text;}
    public static class Resources{public static T Load<T>(string path)where T:class=>new TextAsset{text=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,path+".json"))} as T;}
    public static class JsonUtility
    {
        public static T FromJson<T>(string text)=>JsonSerializer.Deserialize<T>(text,new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true});
        public static string ToJson(object value,bool pretty=false)=>JsonSerializer.Serialize(value,new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true,WriteIndented=pretty});
    }
    public static class Application{public static string persistentDataPath;}
    public static class Debug{public static void LogWarning(string message){}}
}
namespace ShadowTheater.Save
{
    public sealed class SaveData{public string saveGuid;public int gold,hp;}
    public static class SaveManager{public static SaveData Current;}
}
