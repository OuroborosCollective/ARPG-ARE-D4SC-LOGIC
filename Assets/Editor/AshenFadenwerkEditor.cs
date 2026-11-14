using System;
using System.Collections.Generic;
using System.Linq;
using AreArpg.Core;
using AreArpg.UnityClient;
using Fadenwerk;
using Fadenwerk.EditorTools;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AshenFadenwerkEditor
{
    public const string DatabasePath="Assets/FadenwerkData/AshenCryptDatabase.asset";
    public const string PublishedPath="Assets/Resources/AshenFadenwerkText.asset";
    static readonly string[] Objectives={
        "Ilyan verbirgt etwas. Sprich mit ihm in der Aschengrenze.",
        "Folge dem Nordweg. Untersuche das Wegzeichen auf der Aschestrasse.",
        "Die Spur fuehrt zur alten Muehle. Untersuche das Verbandszeug westlich des Weges.",
        "Kehre ins Dorf zurueck: Mara informieren, Ilyan schuetzen oder Maras Fehlbestand aus eigenem Proviant ersetzen.",
        "Kehre zur Muehle zurueck. Untersuche die verlassenen Lagerstaetten nordwestlich des Weges."};
    static readonly string[] Labels={"BEI ILYAN: Nach seinem Geheimnis fragen","AM WEGZEICHEN: Verband und Spuren untersuchen",
        "IM LAGER: Verbandszeug und Essensreste untersuchen","BEI MARA: Hilfe offenlegen – Mara erfährt das Geheimnis",
        "BEI ILYAN: Schweigen zusagen – Fehlbestand bleibt","BEI MARA: Geheimnis bewahren, Lager auffüllen",
        "AN DEN BETTEN: Das Verschwinden untersuchen"};
    static readonly string[] Responses={
        "Ilyan: Ich bringe Verwundeten heimlich Essen. Folge der Aschestrasse zur alten Muehle.",
        "Ein Verband haengt am Wegzeichen. Fussspuren fuehren nach Norden zur Muehle.",
        "Verbandszeug und Essensreste belegen Ilyans Hilfe. Kehre zurueck: offenlegen, schuetzen oder Vorrat ersetzen.",
        "Mara erfährt von Ilyans Versorgung.","Ilyans Geheimnis bleibt bewahrt.","Der Core prüft und verbraucht die tatsächlich fehlenden Rationen.",
        "Im Verletztenlager fehlt ein Patient."};

    static AshenFadenwerkEditor()
    {
        FadenwerkAuthoringBridge.ReadState=State;
        FadenwerkAuthoringBridge.Command=Command;
    }
    [Serializable] class BridgeState
    {
        public string contract=AuthoredHungerBinding.Contract, questId=AuthoredHungerBinding.QuestId, publishedHash="", questStatus="editor";
        public bool installed, published, playing;
    }
    public static string State()
    {
        var p=AssetDatabase.LoadAssetAtPath<AshenFadenwerkText>(PublishedPath);
        var s=new BridgeState { installed=FadenwerkAuthoringBridge.Database?.FindQuest(AuthoredHungerBinding.QuestId)!=null,
            published=p!=null&&p.Valid,publishedHash=p!=null?p.sourceHash:"",playing=EditorApplication.isPlaying };
        if(s.playing){var r=UnityEngine.Object.FindFirstObjectByType<ArenaRuntime>();if(r!=null)s.questStatus=AuthoredHungerBinding.Status(r.Simulation?.Progression);}
        return JsonUtility.ToJson(s);
    }
    static string Command(string command)
    {
        if(command=="setup") Install();
        else if(command=="publish") Publish();
        else throw new InvalidOperationException("Unbekannter Spielbefehl.");
        return "{\"ok\":true}";
    }
    [MenuItem("Tools/Ashen Crypt/Fadenwerk/Autorenanbindung öffnen")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play-Modus zuerst beenden.");
        var db=AssetDatabase.LoadAssetAtPath<FadenwerkDatabase>(DatabasePath);
        if(db==null)
        {
            if(System.IO.File.Exists(DatabasePath))throw new InvalidOperationException("Datenbankpfad bereits belegt.");
            if(!AssetDatabase.IsValidFolder("Assets/FadenwerkData"))AssetDatabase.CreateFolder("Assets","FadenwerkData");
            db=ScriptableObject.CreateInstance<FadenwerkDatabase>();db.worldSeed="ashen-authoring-v1";
            var q=new QuestDef { id=AuthoredHungerBinding.QuestId,title="HUNGER HINTER DEN MAUERN",
                description="Ilyans geheime Versorgung, die alte Mühle und die verschwundenen Verletzten. Die bestehenden Spielregeln bleiben verbindlich.",requiredLevel=1,giverNpcId="ilyan" };
            for(int i=0;i<Objectives.Length;i++)q.objectives.Add(new ObjectiveDef {type="custom",targetId="ashen.hunger.stage."+i,count=1,text=Objectives[i]});
            db.quests.Add(q);
            var d=new DialogueDef {id=AuthoredHungerBinding.DialogueId,title="Ilyan / Mühle – gebundene Handlungen",startNodeId="hear_ilyan"};
            for(int i=0;i<7;i++)d.nodes.Add(new DialogueNode {id=AuthoredHungerBinding.ActionId((HungerAction)i),text=Responses[i],
                choices=new List<DialogueChoice>{new DialogueChoice {text=Labels[i],action="end"}}});
            db.dialogues.Add(d);AssetDatabase.CreateAsset(db,DatabasePath);AssetDatabase.SaveAssetIfDirty(db);
        }
        FadenwerkAuthoringBridge.UseDatabase(db);
    }
    static string Text(string s,string name,int max=2000)
    { if(string.IsNullOrWhiteSpace(s)||s.Length>max)throw new InvalidOperationException(name+": Text fehlt oder ist zu lang.");return s; }
    public static AshenFadenwerkText Compile(FadenwerkDatabase db)
    {
        if(db==null)throw new InvalidOperationException("Ashen-Datenbank zuerst anbinden.");
        var qs=db.quests.Where(q=>q!=null&&q.id==AuthoredHungerBinding.QuestId).ToArray();
        var ds=db.dialogues.Where(d=>d!=null&&d.id==AuthoredHungerBinding.DialogueId).ToArray();
        if(qs.Length!=1||ds.Length!=1)throw new InvalidOperationException("Genau eine gebundene Quest und ein Handlungsdialog sind erforderlich.");
        var q=qs[0];var d=ds[0];
        if(q.giverNpcId!="ilyan"||!string.IsNullOrEmpty(q.turnInNpcId)||q.repeatable||q.requiredLevel!=1||!string.IsNullOrEmpty(q.rewardKitId)||q.prerequisiteQuestIds==null||q.prerequisiteQuestIds.Count!=0||q.objectives==null||q.objectives.Count!=5)
            throw new InvalidOperationException("Belohnungen, Wiederholung, Voraussetzungen und Queststruktur bleiben im Core; diese Änderung braucht eine eigene Anbindung.");
        if(d.nodes==null||d.nodes.Count!=7||d.startNodeId!="hear_ilyan")throw new InvalidOperationException("Die sieben gebundenen Handlungen müssen erhalten bleiben.");
        var objectives=new string[5];var labels=new string[7];var responses=new string[7];
        for(int i=0;i<5;i++)
        {
            var o=q.objectives[i];if(o==null||o.type!="custom"||o.targetId!="ashen.hunger.stage."+i||o.count!=1)throw new InvalidOperationException("Ziel-ID, Reihenfolge, Typ und Anzahl sind an den Core gebunden.");
            objectives[i]=Text(o.text,"Questziel");
        }
        for(int i=0;i<7;i++)
        {
            var matches=d.nodes.Where(n=>n!=null&&n.id==AuthoredHungerBinding.ActionId((HungerAction)i)).ToArray();
            if(matches.Length!=1)throw new InvalidOperationException("Fehlende oder doppelte Handlungs-ID.");
            var n=matches[0];var c=n.choices!=null&&n.choices.Count==1?n.choices[0]:null;
            if(c==null||c.action!="end"||!string.IsNullOrEmpty(c.actionParam)||!string.IsNullOrEmpty(c.nextNodeId)||!string.IsNullOrEmpty(c.requiresFlag)||!string.IsNullOrEmpty(c.conditionQuestId)||!string.IsNullOrEmpty(c.conditionState))
                throw new InvalidOperationException("Keine freien Kit-, Flag- oder Questaktionen: Bedingungen und Folgen prüft der Core.");
            labels[i]=Text(c.text,"Handlung",180);responses[i]=Text(n.text,"Rückmeldung");
        }
        string title=Text(q.title,"Titel",100);
        var result=ScriptableObject.CreateInstance<AshenFadenwerkText>();
        result.name=AshenFadenwerkText.ResourceName;result.title=title;result.objectives=objectives;result.labels=labels;result.responses=responses;result.sourceHash=result.ContentHash();return result;
    }
    public static void Publish()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play-Modus zuerst beenden.");
        var compiled=Compile(FadenwerkAuthoringBridge.Database);
        try
        {
            if(!AssetDatabase.IsValidFolder("Assets/Resources"))AssetDatabase.CreateFolder("Assets","Resources");
            var existing=AssetDatabase.LoadAssetAtPath<AshenFadenwerkText>(PublishedPath);
            if(existing==null)
            {
                if(System.IO.File.Exists(PublishedPath))throw new InvalidOperationException("Publikationspfad bereits belegt.");
                AssetDatabase.CreateAsset(compiled,PublishedPath);compiled=null;
            }
            else {Undo.RecordObject(existing,"Fadenwerk-Texte übernehmen");EditorUtility.CopySerialized(compiled,existing);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);}
        }
        finally {if(compiled!=null)UnityEngine.Object.DestroyImmediate(compiled);}
    }
}
