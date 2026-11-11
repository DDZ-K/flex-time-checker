using FlexExtractor;
static partial class Cases {
 public static void TestEngineStopRestartPreservesBaseline(){var t=typeof(Config).Assembly.GetType("FlexExtractor.Engine");Program.Check(t!=null,"engine lifecycle missing");typeof(MachineState).GetField("TestRoot",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.SetValue(null,TestMachineRoot);var c=Cfg(Fresh());Source(c);var engine=Activator.CreateInstance(t!,c)!;DateTime baseline;
  try{Invoke(t!,"Start",engine);baseline=(DateTime)t!.GetProperty("BaselineUtc")!.GetValue(engine)!;Program.Check(SpinWait.SpinUntil(()=>Directory.Exists(c.DispenseItems[0].SaveFolder)&&Directory.GetFiles(c.DispenseItems[0].SaveFolder,"Request*.xml").Length==1,8000),"engine failed real copy");Rejected(()=>{using var other=new OwnerLease();});}finally{((Task)Invoke(t!,"StopAsync",engine)!).GetAwaiter().GetResult();}
  var next=Activator.CreateInstance(t!,c)!;try{Invoke(t!,"Start",next);Program.Check((DateTime)t!.GetProperty("BaselineUtc")!.GetValue(next)! == baseline,"baseline reset on restart");}finally{((Task)Invoke(t!,"StopAsync",next)!).GetAwaiter().GetResult();}using var after=new OwnerLease();
 }
}
