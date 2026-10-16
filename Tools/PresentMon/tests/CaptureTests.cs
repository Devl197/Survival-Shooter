using System;
using System.IO;
namespace UnityEngine { public static class JsonUtility { public static string ToJson(object value, bool pretty) { return "{}"; } } }
class CaptureTests
{
 static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
 static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"PresentMonTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
  string input=Path.Combine(root,"raw.csv"), output=Path.Combine(root,"measured.csv");
  string header="Application,ProcessID,SwapChainAddress,CPUStartQPC,FrameTime";
  File.WriteAllLines(input,new[]{header,"game,42,A,95,10","\"game, quoted\",42,A,100,10","game,43,A,110,10","game,42,A,190,10","game,42,A,195,10","game,42,A,200,10"});
  Check(PresentMonCsv.Filter(input,output,42,100,200,1000)==2,"Full containment and PID filtering");
  Check(File.ReadAllLines(output).Length==3,"Header and two rows");
  Check(PresentMonCsv.Split("\"a,\"\"b\"\"\",c")[0]=="a,\"b\"","Quoted CSV");
  File.WriteAllLines(input,new[]{header,"game,42,A,100,10","game,42,B,110,10"});
  bool failed=false; try { PresentMonCsv.Filter(input,output,42,100,200,1000); } catch(InvalidDataException) { failed=true; } Check(failed,"Reject mixed swap chains");
  File.WriteAllText(input,"Wrong,Header\n"); failed=false; try { PresentMonCsv.Filter(input,output,42,100,200,1000); } catch(InvalidDataException){failed=true;} Check(failed,"Reject schema mismatch");
  File.WriteAllLines(input,new[]{header,"game,42,A,9007199254740993,0.01"});
  Check(PresentMonCsv.Filter(input,output,42,9007199254740993,9007199254741003,1000000)==1,"QPC precision beyond double integer range");
  using(var capture=new PresentMonCapture()) { Check(capture.Exited,"Unstarted process safely disposed"); }
  Console.WriteLine("PASS: frame containment, PID, CSV quoting, schema, swap chains, QPC precision and unstarted disposal.");
 }
}
