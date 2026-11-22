using System;
using System.Reflection;
using AreArpg.UnityClient;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AreArpg.EditMode.Tests
{
    public class FadenwerkBindingTests
    {
        const string DbPath="Assets/FadenwerkData/AshenCryptDatabase.asset";
        [Test] public void PublishedTextIsBuildIncludedAndIntact()
        {var p=Resources.Load<AshenFadenwerkText>(AshenFadenwerkText.ResourceName);Assert.IsNotNull(p);Assert.IsTrue(p.Valid);}
        [Test] public void ValidDraftCompilesToPublishedHash()
        {
            var type=Type.GetType("AshenFadenwerkEditor, Assembly-CSharp-Editor",true);
            var db=AssetDatabase.LoadAssetAtPath<ScriptableObject>(DbPath);
            var compiled=(AshenFadenwerkText)type.GetMethod("Compile").Invoke(null,new object[]{db});
            try { Assert.AreEqual(AshenFadenwerkText.ResourceName,compiled.name); Assert.IsTrue(compiled.Valid); Assert.AreEqual(Resources.Load<AshenFadenwerkText>(AshenFadenwerkText.ResourceName).sourceHash,compiled.sourceHash); }
            finally { UnityEngine.Object.DestroyImmediate(compiled); }
        }
        [Test] public void LivingChronicleKeysArePublishedFromValidFadenwerkDialogues()
        {
            var published = Resources.Load<AshenFadenwerkText>(AshenFadenwerkText.ResourceName);
            Assert.IsNotNull(published);
            Assert.IsTrue(published.Valid);
            Assert.IsTrue(published.ChronicleValid, "Published Chronicle must carry a valid independent content hash");
            var db = AssetDatabase.LoadAssetAtPath<ScriptableObject>(DbPath);
            var editor = Type.GetType("AshenFadenwerkEditor, Assembly-CSharp-Editor", true);
            var compiled = (AshenFadenwerkText)editor.GetMethod("Compile").Invoke(null, new object[] { db });
            try
            {
                Assert.IsTrue(compiled.ChronicleValid);
                CollectionAssert.AreEqual(compiled.chronicleKeys, published.chronicleKeys);
                CollectionAssert.AreEqual(compiled.chronicleTexts, published.chronicleTexts);
                Assert.AreEqual(compiled.chronicleSourceHash, published.chronicleSourceHash);
                foreach (string key in AreArpg.Core.ChronicleFadenwerkBinding.Keys)
                {
                    Assert.IsNotNull(AreArpg.Core.ChronicleFadenwerkBinding.DialogueId(key));
                    Assert.IsNotEmpty(published.ChronicleTextFor(key));
                }
                Assert.IsNull(published.ChronicleTextFor("chronicle.unknown"));
            }
            finally { UnityEngine.Object.DestroyImmediate(compiled); }
        }
        [Test] public void TamperedChronicleFadenwerkTextFailsClosed()
        {
            var published = Resources.Load<AshenFadenwerkText>(AshenFadenwerkText.ResourceName);
            var altered = UnityEngine.Object.Instantiate(published);
            try
            {
                altered.chronicleTexts[0] = "new text without authoring receipt";
                Assert.IsFalse(altered.ChronicleValid);
                Assert.IsFalse(altered.Valid);
                Assert.IsNull(altered.ChronicleTextFor("chronicle.camp.theft"));
            }
            finally { UnityEngine.Object.DestroyImmediate(altered); }
        }

        [Test] public void TamperedPublishedTextIsRejected()
        {
            var p=UnityEngine.Object.Instantiate(Resources.Load<AshenFadenwerkText>(AshenFadenwerkText.ResourceName));
            try{p.responses[0]="changed outside publication";Assert.IsFalse(p.Valid);}finally{UnityEngine.Object.DestroyImmediate(p);}
        }
        [TestCase("reward")][TestCase("objective")][TestCase("action")][TestCase("repeatable")]
        public void UnsupportedGameplayEditsDoNotChangePublishedAsset(string kind)
        {
            var original=AssetDatabase.LoadAssetAtPath<ScriptableObject>(DbPath);Assert.IsNotNull(original);
            var db=UnityEngine.Object.Instantiate(original);
            var published=Resources.Load<AshenFadenwerkText>(AshenFadenwerkText.ResourceName);string before=JsonUtility.ToJson(published);
            try
            {
                string json=JsonUtility.ToJson(db);
                if(kind=="reward")json=json.Replace("\"rewardKitId\":\"\"","\"rewardKitId\":\"free-reward\"");
                if(kind=="objective")json=json.Replace("ashen.hunger.stage.0","unknown-objective");
                if(kind=="action")json=json.Replace("\"action\":\"end\"","\"action\":\"giveKit\"");
                if(kind=="repeatable")json=json.Replace("\"repeatable\":false","\"repeatable\":true");
                JsonUtility.FromJsonOverwrite(json,db);
                var type=Type.GetType("AshenFadenwerkEditor, Assembly-CSharp-Editor",true);
                var e=Assert.Throws<TargetInvocationException>(()=>type.GetMethod("Compile").Invoke(null,new object[]{db}));
                Assert.IsInstanceOf<InvalidOperationException>(e.InnerException);
                Assert.AreEqual(before,JsonUtility.ToJson(published));
            }
            finally {UnityEngine.Object.DestroyImmediate(db);}
        }
    }
}
