using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

var root = Path.GetFullPath(".");
var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (Match m in Regex.Matches(File.ReadAllText("tmp/EnergyValidation.rsp"), "-r:\"([^\"]+)\""))
{
    var p = Path.GetFullPath(m.Groups[1].Value);
    if (p.EndsWith(".ref.dll") && File.Exists(p.Replace(".ref.dll", ".dll"))) p = p.Replace(".ref.dll", ".dll");
    if (File.Exists(p)) paths[Path.GetFileNameWithoutExtension(p)] = p;
}
AssemblyLoadContext.Default.Resolving += (_, n) => paths.TryGetValue(n.Name, out var p) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(p) : null;
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root, "tmp/EnergyValidation.dll"));
var controllerType = assembly.GetType("EnergyStationController", true);
var loggerType = assembly.GetType("EventLogger", true);
object Make(Type t) => FormatterServices.GetUninitializedObject(t);
void Set(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
object Get(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, args);
void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }

var controller = Make(controllerType);
Set(controller, "acceptedInteractionIds", new HashSet<string>());
Set(controller, "interactionsToComplete", 2);
Check((bool)Call(controller, "TryRegisterInteraction", "energy_support_doubts", ""), "first choice accepted");
Check(!(bool)Call(controller, "TryRegisterInteraction", "energy_support_doubts", ""), "same choice rejected twice");
Check((int)Get(controller, "interactionCount") == 1, "duplicate does not advance progress");
Check((bool)Call(controller, "TryRegisterInteraction", "energy_support_materials", ""), "second distinct choice completes round");
Check(Get(controller, "sessionState").ToString() == "ReadyToComplete", "completion available after exactly two choices");
Check(!(bool)Call(controller, "TryRegisterInteraction", "energy_support_priorities", ""), "third choice blocked after completion");
Call(controller, "ResetChoices");
Check((int)Get(controller, "interactionCount") == 0 && Get(controller, "sessionState").ToString() == "Playing", "reset reopens round and clears progress");
Check((bool)Call(controller, "TryRegisterInteraction", "energy_support_doubts", ""), "previously consumed choice available after reset");

var logger = Make(loggerType);
Set(logger, "sessionId", "test");
Set(logger, "interactionIds", new List<string> { "energy_hydration", "energy_break" });
Set(logger, "eventTypes", new List<string> { "SESSION_STARTED", "energy_question:pause:v1", "USER_ACTION", "ACTIVITY_COMPLETED_EARLY", "COMPLETE_BUTTON_AVAILABLE" });
Set(logger, "interactionCount", 2);
Set(logger, "activityEnded", true);
Set(logger, "completedEarly", true);
Call(logger, "ResetChoices");
Check(((List<string>)Get(logger, "interactionIds")).Count == 0 && (int)Get(logger, "interactionCount") == 0, "discarded choices removed from recorded data");
var events = (List<string>)Get(logger, "eventTypes");
Check(events.Contains("energy_question:pause:v1") && events.Contains("CHOICES_RESET") && !events.Contains("ACTIVITY_COMPLETED_EARLY"), "question context preserved and stale completion removed");
Console.WriteLine("Behavior checks passed. These checks do not simulate Unity drag input or rendering.");
