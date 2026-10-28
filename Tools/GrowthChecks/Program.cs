// Managed tests execute the shipping grant policy and its actual JSON. Not Unity execution.
using System;
using System.IO;
using System.Text.Json;
using ShadowTheater.Data;
class Program
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static int Required(int level) => 20 + level * level * 5;
    static void Main()
    {
        string[] ids = { "rehearsal_script", "annotated_script", "complete_script" };
        int[] starts = { 1, 20, 50 }, caps = { 20, 50, 80 };
        for (int i = 0; i < ids.Length; i++)
        {
            var rule = TrainingItemPolicy.Find(ids[i]);
            Check(rule != null && rule.minimumLevel == starts[i] && rule.maximumLevel == caps[i], "approved rule " + ids[i]);
            Check(TrainingItemPolicy.TryGrant(rule, starts[i], 0, 200, Required, out int grant, out _) && grant == 200, "within band " + ids[i]);
            Check(!TrainingItemPolicy.TryGrant(rule, caps[i], 0, 2400, Required, out grant, out _) && grant == 0, "reject cap " + ids[i]);
            Check(!TrainingItemPolicy.TryGrant(rule, caps[i] + 1, 0, 2400, Required, out grant, out _), "reject above " + ids[i]);
            Check(!TrainingItemPolicy.TryGrant(rule, starts[i] - 1, 0, 2400, Required, out grant, out _), "reject below " + ids[i]);
            Check(TrainingItemPolicy.TryGrant(rule, caps[i] - 1, Required(caps[i] - 1) - 7, 2400, Required, out grant, out _) && grant == 7, "only seven XP to cap " + ids[i]);
            Check(TrainingItemPolicy.TryGrant(rule, caps[i] - 1, 0, int.MaxValue, Required, out grant, out _) && grant == Required(caps[i] - 1), "no XP banking " + ids[i]);
            Check(!TrainingItemPolicy.TryGrant(rule, starts[i], int.MaxValue, 200, Required, out grant, out _), "bad save fails closed " + ids[i]);
        }
        Check(!TrainingItemPolicy.TryGrant(TrainingItemPolicy.Find("unknown_book"), 5, 0, 200, Required, out _, out _), "unknown XP item rejected");
        Check(!TrainingItemPolicy.TryGrant(TrainingItemPolicy.Find(ids[0]), 5, 0, 0, Required, out _, out _), "zero XP rejected");
        Console.WriteLine("PASS: " + checks + " shipping training-policy checks, caps 20/50/80, no excess XP, unknown/bad state rejected (.NET, not Unity)");
    }
}
namespace ShadowTheater.Data { public class ShadowInstance { public const int MaxLevel = 120; } }
namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)] public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) {} }
    public class TextAsset { public string text; }
    public static class Resources
    {
        public static T Load<T>(string path) where T:class => new TextAsset { text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TrainingItemLimits.json")) } as T;
    }
    public static class JsonUtility { public static T FromJson<T>(string value) => JsonSerializer.Deserialize<T>(value,new JsonSerializerOptions { IncludeFields=true }); }
}
