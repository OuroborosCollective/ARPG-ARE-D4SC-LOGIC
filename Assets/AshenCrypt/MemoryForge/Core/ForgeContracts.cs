using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AshenCrypt.MemoryForge
{
    public enum ForgeAction { None=-1, Wait=0, Travel, Rest, SeekFood, Eat, Flee, Attack, Follow, Observe, Treat }
    public enum Feature { Hunger, Fatigue, Injury, Danger, Food, Trust, Fear, Gratitude, Hostility, Quest, TravelCost, ResourceCost, AllyDistance, TargetInjury, Count }
    [Flags] public enum Capability { None=0, Movement=1, Needs=2, Social=4, Combat=8, Companion=16, Treatment=32, Hearing=64, Smell=128 }

    public static class ActionSet
    {
        public const ulong Known=(1UL<<10)-1;
        public static ulong Of(params ForgeAction[] actions)
        {
            ulong mask=0;foreach(var action in actions)
            {if(action<ForgeAction.Wait||action>ForgeAction.Treat)throw new ArgumentOutOfRangeException(nameof(actions));mask|=1UL<<(int)action;}
            return mask;
        }
        public static bool Contains(ulong mask,ForgeAction action) => action>=ForgeAction.Wait&&action<=ForgeAction.Treat&&(mask&(1UL<<(int)action))!=0;
        internal static Capability Requires(ForgeAction action)
        {
            switch(action)
            {
                case ForgeAction.Travel:case ForgeAction.Flee:return Capability.Movement;
                case ForgeAction.Rest:case ForgeAction.Eat:case ForgeAction.SeekFood:return Capability.Needs;
                case ForgeAction.Attack:return Capability.Combat;
                case ForgeAction.Follow:return Capability.Movement|Capability.Companion;
                case ForgeAction.Observe:return Capability.Social;
                case ForgeAction.Treat:return Capability.Treatment;
                default:return Capability.None;
            }
        }
    }
    public static class FixedPoint
    {
        public const int Scale=10000;
        /// <summary>Signed multiplication, nearest integer with exact halves away from zero; overflow throws.</summary>
        public static int Multiply(int a,int b)
        {
            long product=checked((long)a*b);
            return checked((int)(product>=0?(product+Scale/2)/Scale:-((-product+Scale/2)/Scale)));
        }
        public static int Clamp(int value) => Math.Max(0,Math.Min(Scale,value));
    }
    public readonly struct Observation
    {
        public Feature Feature { get; }
        public int Value { get; }
        public Observation(Feature feature,int value)
        {if(feature<0||feature>=Feature.Count||value<0||value>FixedPoint.Scale)throw new ArgumentOutOfRangeException(nameof(value));Feature=feature;Value=value;}
    }
    /// <summary>The host supplies observations and the hard legal-action mask. No world queries or clock live here.</summary>
    public sealed class ObservationFrame
    {
        private readonly int[] _values=new int[(int)Feature.Count];
        public ulong LegalActions { get; }
        public ulong PresentFeatures { get; }
        public int this[Feature feature] => _values[(int)feature];
        public ObservationFrame(ulong legalActions,IEnumerable<Observation> observations)
        {
            if((legalActions&~ActionSet.Known)!=0)throw new ArgumentOutOfRangeException(nameof(legalActions));
            LegalActions=legalActions;ulong present=0;
            foreach(var observation in observations??throw new ArgumentNullException(nameof(observations)))
            {ulong bit=1UL<<(int)observation.Feature;if((present&bit)!=0)throw new ArgumentException("Duplicate observation");present|=bit;_values[(int)observation.Feature]=observation.Value;}
            PresentFeatures=present;
        }
    }
    internal static class ContractText
    {
        public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
        public static string Number(ulong value) => value.ToString(CultureInfo.InvariantCulture);
        public static string Id(string value)
        {
            if(string.IsNullOrEmpty(value)||value.Length>128)throw new ArgumentException("Invalid stable identifier");
            foreach(char c in value)if(!((c>='a'&&c<='z')||(c>='0'&&c<='9')||c=='.'||c=='_'||c=='-'||c==':'))throw new ArgumentException("Invalid stable identifier");
            return value;
        }
        public static string Hash(string value)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
    }
}
