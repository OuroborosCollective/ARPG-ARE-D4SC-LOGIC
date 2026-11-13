using System;
using System.Linq;
using AshenCrypt.MemoryForge;
using AshenCrypt.MemoryForge.Samples;

internal static class Program
{
    private static int checks;
    private static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    public static int Main()
    {
        try
        {
            var w=new DemoWorld();Check(!w.GiveFood(),"distant-help-is-rejected");w.Approach();for(int i=0;i<20;i++)w.Step();
            Check(w.GiveFood()&&w.Provisions==2&&w.Archive.Get("nara").Known.Count==1,"real-provision-transfer-is-personally-remembered");
            Check(w.Greet().Contains("food"),"later-greeting-refers-to-help");w.Step();var token=w.Save();Check(DemoWorld.TryLoad(token,out var loaded)&&loaded.Save()==token,"host-and-forge-state-roundtrip");
            for(int i=0;i<100;i++){w.Step();loaded.Step();Check(w.Save()==loaded.Save(),"exact-resumed-tick-"+i);}
            foreach(bool visible in new[]{false,true})
            {
                var crime=new DemoWorld();crime.Approach();if(visible)crime.MoveWitnessToCourtyard();for(int i=0;i<20;i++)crime.Step();
                Check(crime.Rob()&&!crime.Rob(),"purse-transfer-is-once-"+visible);
                Check(crime.Archive.Get("watch").Known.Count==(visible?1:0),"actual-wall-and-distance-control-witness-"+visible);
                if(visible)Check(crime.Archive.Get("watch").Known[0].Origin==KnowledgeOrigin.Witnessed,"visible-crime-has-direct-provenance");
            }
            Console.WriteLine("ALL PASS: "+checks+" standalone sample assertions");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
