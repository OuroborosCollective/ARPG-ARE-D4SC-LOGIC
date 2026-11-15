using System;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Fadenwerk.Tests
{
    public class FadenwerkRevisionTests
    {
        static Type EditorType(string name) { foreach(var a in AppDomain.CurrentDomain.GetAssemblies()) { var t=a.GetType("Fadenwerk.EditorTools."+name); if(t!=null)return t; } throw new Exception(name); }
        static object Request(string method,string path,string body="")
        {
            var type=EditorType("FwRequest"); var r=Activator.CreateInstance(type);
            type.GetField("method").SetValue(r,method); type.GetField("path").SetValue(r,path); type.GetField("body").SetValue(r,body);
            return EditorType("FadenwerkApi").GetMethod("Handle").Invoke(null,new[]{r});
        }
        static int Status(object r) { return (int)r.GetType().GetField("status").GetValue(r); }
        [Serializable] class State { public string rev; }

        [Test]
        public void StaleAndMissingRevisionCannotOverwriteNewerSeed()
        {
            var data=EditorType("FadenwerkEditorData"); var settings=EditorType("FadenwerkSettings");
            var original=data.GetProperty("Database").GetValue(null); var auto=settings.GetProperty("AutoSave"); var saved=auto.GetValue(null);
            var db=ScriptableObject.CreateInstance<FadenwerkDatabase>(); db.worldSeed="first";
            try
            {
                auto.SetValue(null,false); data.GetMethod("SetDatabase").Invoke(null,new object[]{db});
                var state=Request("GET","/api/state");
                string revision=JsonUtility.FromJson<State>(Encoding.UTF8.GetString((byte[])state.GetType().GetField("body").GetValue(state))).rev;
                db.worldSeed="newer-editor-value";
                var result=Request("POST","/api/seed/save","{\"worldSeed\":\"stale-browser\",\"expectedRevision\":\""+revision+"\"}");
                Assert.AreEqual(409,Status(result)); Assert.AreEqual("newer-editor-value",db.worldSeed);
                Assert.AreEqual(409,Status(Request("POST","/api/seed/save","{\"worldSeed\":\"without-revision\"}")));
                Assert.AreEqual("newer-editor-value",db.worldSeed);
            }
            finally { data.GetMethod("SetDatabase").Invoke(null,new[]{original}); auto.SetValue(null,saved); UnityEngine.Object.DestroyImmediate(db); }
        }
    }
}
