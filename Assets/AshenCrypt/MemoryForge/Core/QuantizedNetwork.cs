using System;
using System.Linq;

namespace AshenCrypt.MemoryForge
{
    /// <summary>Immutable 14-input, bounded hidden-layer, 10-action integer network. No training or clock.</summary>
    public sealed class QuantizedNetwork
    {
        private readonly int[] _input,_hiddenBias,_output,_outputBias;
        public int HiddenCount { get; }
        public string Canonical { get; }
        public string Hash { get; }
        public QuantizedNetwork(int hiddenCount,int[] inputWeights,int[] hiddenBias,int[] outputWeights,int[] outputBias)
        {
            if(hiddenCount<1||hiddenCount>16)throw new ArgumentOutOfRangeException(nameof(hiddenCount));
            HiddenCount=hiddenCount;
            _input=Copy(inputWeights,hiddenCount*(int)Feature.Count);_hiddenBias=Copy(hiddenBias,hiddenCount);
            _output=Copy(outputWeights,10*hiddenCount);_outputBias=Copy(outputBias,10);
            Canonical="AshenCrypt.QuantizedNetwork.v1;scale=10000;round=nearest-away;hidden=clipped-relu;output=signed-clamp\n"+ContractText.Number(hiddenCount)+"\n"+
                string.Join("\n",new[]{_input,_hiddenBias,_output,_outputBias}.Select(a=>string.Join(",",a.Select(x=>ContractText.Number(x)))));
            Hash=ContractText.Hash(Canonical);
        }
        private static int[] Copy(int[] values,int count)
        {
            if(values==null||values.Length!=count||values.Any(w=>Math.Abs((long)w)>40000))throw new ArgumentException("Invalid bounded network weights");
            return (int[])values.Clone();
        }
        public int InputWeight(int hidden,Feature feature)=>_input[checked(hidden*(int)Feature.Count+(int)feature)];
        public int OutputWeight(ForgeAction action,int hidden)=>_output[checked((int)action*HiddenCount+hidden)];
        public int HiddenBias(int hidden)=>_hiddenBias[hidden];
        public int OutputBias(ForgeAction action)=>_outputBias[(int)action];
        public int[] HiddenActivations(ObservationFrame frame)
        {
            if(frame==null)throw new ArgumentNullException(nameof(frame));
            var hidden=new int[HiddenCount];
            for(int h=0;h<hidden.Length;h++)
            {
                int sum=_hiddenBias[h];
                for(int f=0;f<(int)Feature.Count;f++)sum=checked(sum+FixedPoint.Multiply(InputWeight(h,(Feature)f),frame[(Feature)f]));
                hidden[h]=FixedPoint.Clamp(sum);
            }
            return hidden;
        }
        internal int[] Evaluate(ObservationFrame frame,ulong legal)
        {
            var hidden=HiddenActivations(frame);var scores=new int[10];
            for(int a=0;a<10;a++)
            {
                if(!ActionSet.Contains(legal,(ForgeAction)a))continue;
                int sum=_outputBias[a];
                for(int h=0;h<hidden.Length;h++)sum=checked(sum+FixedPoint.Multiply(_output[a*HiddenCount+h],hidden[h]));
                scores[a]=Math.Max(-10000,Math.Min(10000,sum));
            }
            return scores;
        }
    }
}
