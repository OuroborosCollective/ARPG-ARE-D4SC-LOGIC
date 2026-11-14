// Run through Unity CLI script eval in the exact authoring project, never print the token.
var assembly = System.Reflection.Assembly.Load("Fadenwerk.Editor");
var settings = assembly.GetType("Fadenwerk.EditorTools.FadenwerkSettings", true);
settings.GetProperty("BindAll").SetValue(null, false);
settings.GetProperty("Port").SetValue(null, 8095);
settings.GetProperty("AutoStart").SetValue(null, true);

var folder = "/var/lib/unityarpg/.local/share/are-arpg/fadenwerk-web";
System.IO.File.WriteAllText(folder + "/editor-token", (string)settings.GetProperty("Token").GetValue(null));
assembly.GetType("Fadenwerk.EditorTools.FadenwerkServer", true).GetMethod("Restart").Invoke(null, null);
var bridge=System.Type.GetType("AshenFadenwerkEditor, Assembly-CSharp-Editor", true);
bridge.GetMethod("Install").Invoke(null,null);
bridge.GetMethod("Publish").Invoke(null,null);
"Fadenwerk owner browser started; canonical text published."
