using System;
using System.Security.Cryptography;
using System.Text;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    /// <summary>Build-included, explicitly published presentation. Contains no quest progress or rewards.</summary>
    public sealed class AshenFadenwerkText : ScriptableObject
    {
        public const string ResourceName="AshenFadenwerkText";
        public string contract=AuthoredHungerBinding.Contract;
        public string title;
        public string[] objectives;
        public string[] labels;
        public string[] responses;
        public string sourceHash;
        public string ContentHash()
        {
            var b=new StringBuilder();
            Action<string> add=s=>{s=s??"";b.Append(s.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(s);};
            add(contract);add(title);
            foreach(var list in new[]{objectives,labels,responses}) { b.Append((list==null?-1:list.Length).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(';'); if(list!=null)foreach(var s in list)add(s); }
            using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(b.ToString()))).Replace("-","").ToLowerInvariant();
        }
        public bool Valid { get { return contract==AuthoredHungerBinding.Contract && !string.IsNullOrWhiteSpace(title) &&
            ValidTexts(objectives,5) && ValidTexts(labels,7) && ValidTexts(responses,7) && sourceHash==ContentHash(); } }
        static bool ValidTexts(string[] a,int count) { if(a==null||a.Length!=count)return false;foreach(var s in a)if(string.IsNullOrWhiteSpace(s)||s.Length>2000)return false;return true; }
    }
    public sealed partial class ArenaRuntime
    {
        private AshenFadenwerkText _authoredHunger;
        private bool _authoredHungerLoaded;
        private AshenFadenwerkText AuthoredHunger
        {
            get
            {
                if(!_authoredHungerLoaded){_authoredHungerLoaded=true;var p=Resources.Load<AshenFadenwerkText>(AshenFadenwerkText.ResourceName);if(p!=null&&p.Valid)_authoredHunger=p;}
                return _authoredHunger;
            }
        }
        public string AuthoredHungerHash { get { var p=AuthoredHunger; return p!=null?p.sourceHash:""; } }
        private string PresentedHungerObjective()
        { var p=AuthoredHunger; int stage=AuthoredHungerBinding.TextStage(_game.Progression); return p!=null&&stage>=0?p.objectives[stage]:_game.HungerObjective; }
        private string PresentedStoryResult(HungerAction action, SocialResult result)
        {
            var p=AuthoredHunger;
            // Cost-bearing and consequential choice feedback stays canonical and includes actual transaction details.
            return p!=null && result.Accepted && action<=HungerAction.InspectShelter && action>=HungerAction.HearIlyan ? p.responses[(int)action] : result.Text;
        }
    }
}
