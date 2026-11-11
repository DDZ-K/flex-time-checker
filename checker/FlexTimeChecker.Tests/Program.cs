if(args.Contains("--worker-hang")) { Thread.Sleep(Timeout.Infinite); return 9; }
if(args.Contains("--worker"))
{
    var dir=args[Array.IndexOf(args,"--worker")+1];
    if(args.Contains("--worker-fault")) { File.WriteAllText(Path.Combine(dir,"error.txt"),"synthetic-worker-fault"); return 2; }
    var type=typeof(FlexTimeChecker.Core.WaitRules).Assembly.GetType("FlexTimeChecker.Core.WorkerHost");
    if(type is null) { Console.WriteLine("WorkerHost behavior missing"); return 8; }
    var result=(int)type.GetMethod("Run")!.Invoke(null,[dir])!;
    if(args.Contains("--worker-forge"))
    {
        var file=Path.Combine(dir,"reply.json");
        var reply=System.Text.Json.JsonSerializer.Deserialize<FlexTimeChecker.Core.WorkerReply>(File.ReadAllText(file),FlexTimeChecker.Core.ConfigStore.Json)!;
        File.WriteAllText(file,System.Text.Json.JsonSerializer.Serialize(reply with {Generation=-999},FlexTimeChecker.Core.ConfigStore.Json));
    }
    return result;
}
return FlexTimeChecker.Tests.SelfTests.Run(args.FirstOrDefault());
