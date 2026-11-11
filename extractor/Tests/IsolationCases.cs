using FlexExtractor;
static partial class Cases {
 public static void TestOwnerLeaseExclusiveAcrossThreads(){var t=typeof(Config).Assembly.GetType("FlexExtractor.OwnerLease");Program.Check(t!=null,"safe owner lease missing");var root=TestMachineRoot;var state=typeof(Config).Assembly.GetType("FlexExtractor.MachineState");state!.GetField("TestRoot",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.SetValue(null,root);var first=(IDisposable)Activator.CreateInstance(t!)!;try{Rejected(()=>{using var second=(IDisposable)Activator.CreateInstance(t!)!;});}finally{Task.Run(()=>first.Dispose()).GetAwaiter().GetResult();}using var next=(IDisposable)Activator.CreateInstance(t!)!;}
}
