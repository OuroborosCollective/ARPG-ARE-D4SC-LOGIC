using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Fadenwerk.EditorTools;
using NUnit.Framework;

namespace Fadenwerk.Tests
{
    /// <summary>Project extensions (1.1.0): an extra page at "/?key" and calls below "/api/x/".</summary>
    public class FadenwerkExtensionTests
    {
        static Type EditorType(string name) { foreach(var a in AppDomain.CurrentDomain.GetAssemblies()) { var t=a.GetType("Fadenwerk.EditorTools."+name); if(t!=null)return t; } throw new Exception(name); }
        static object Request(string method, string path, string body, string area)
        {
            var type=EditorType("FwRequest"); var r=Activator.CreateInstance(type);
            type.GetField("method").SetValue(r,method); type.GetField("path").SetValue(r,path); type.GetField("body").SetValue(r,body);
            if(area!=null) ((IDictionary<string,string>)type.GetField("query").GetValue(r))["area"]=area;
            return EditorType("FadenwerkApi").GetMethod("Handle").Invoke(null,new[]{r});
        }
        static int Status(object r) { return (int)r.GetType().GetField("status").GetValue(r); }
        static string Body(object r) { return Encoding.UTF8.GetString((byte[])r.GetType().GetField("body").GetValue(r)); }

        [Test]
        public void ExtensionCallsReachTheProjectHandlerWithPathBodyAndQuery()
        {
            var previous=FadenwerkAuthoringBridge.Extension;
            string seen=null;
            try
            {
                FadenwerkAuthoringBridge.Extension=(method,path,body,query)=>
                {
                    string area; query.TryGetValue("area",out area);
                    seen=method+" "+path+" "+body+" "+area;
                    if(path=="refuse") throw new InvalidOperationException("Nein danke.");
                    return FadenwerkExtensionReply.Json("{\"ok\":true,\"echo\":\""+path+"\"}");
                };
                var reply=Request("POST","/api/x/demo/sub","{\"a\":1}","dorf");
                Assert.AreEqual(200,Status(reply));
                StringAssert.Contains("demo/sub",Body(reply));
                Assert.AreEqual("POST demo/sub {\"a\":1} dorf",seen);
                var refused=Request("POST","/api/x/refuse","{}",null);
                Assert.AreEqual(422,Status(refused));
                StringAssert.Contains("Nein danke.",Body(refused));
                FadenwerkAuthoringBridge.Extension=null;
                Assert.AreEqual(404,Status(Request("GET","/api/x/demo","",null)));
            }
            finally { FadenwerkAuthoringBridge.Extension=previous; }
        }

        [Test]
        public void ExtensionPageAnswersOnlyItsExactKey()
        {
            string key=FadenwerkAuthoringBridge.ExtensionPageKey; Func<byte[]> page=FadenwerkAuthoringBridge.ExtensionPage;
            MethodInfo check=EditorType("FadenwerkServer").GetMethod("IsExtensionPage",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
            Func<string,bool> matches=q=>(bool)check.Invoke(null,new object[]{q});
            try
            {
                FadenwerkAuthoringBridge.ExtensionPageKey="bau"; FadenwerkAuthoringBridge.ExtensionPage=()=>new byte[0];
                Assert.IsTrue(matches("?bau"));
                Assert.IsTrue(matches("?x=1&bau=2"));
                Assert.IsFalse(matches("?bauer"));
                Assert.IsFalse(matches(""));
                Assert.IsFalse(matches(null));
                FadenwerkAuthoringBridge.ExtensionPage=null;
                Assert.IsFalse(matches("?bau"),"no page registered");
            }
            finally { FadenwerkAuthoringBridge.ExtensionPageKey=key; FadenwerkAuthoringBridge.ExtensionPage=page; }
        }
    }
}
