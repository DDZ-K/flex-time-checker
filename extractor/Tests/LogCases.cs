using FlexExtractor;
static partial class Cases {
 public static void TestLogsBoundedInMemoryAndOnDisk(){var t=typeof(Config).Assembly.GetType("FlexExtractor.BoundedLog");Program.Check(t!=null,"bounded log missing");var root=Fresh();var log=Activator.CreateInstance(t!,root,50)!;for(int i=0;i<5000;i++)Invoke(t!,"Write",log,new string('x',2048));var lines=(string[])Invoke(t!,"Snapshot",log)!;Program.Check(lines.Length==50&&lines.All(x=>x.Length<1100),"memory log unbounded");Program.Check(Directory.GetFiles(root).Length<=3&&Directory.GetFiles(root).All(x=>new FileInfo(x).Length<=1048576),"disk log unbounded");}
}
