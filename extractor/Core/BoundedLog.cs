using System.Text;
namespace FlexExtractor;
public sealed class BoundedLog {
 readonly object gate=new();readonly Queue<string> lines=new();readonly string folder;readonly int max;
 public BoundedLog(string folder,int max){this.folder=folder;this.max=Math.Clamp(max,50,2000);Paths.NoReparse(folder);Directory.CreateDirectory(folder);}
 public void Write(string message){lock(gate){var line=$"{DateTimeOffset.Now:O} "+message[..Math.Min(1024,message.Length)].Replace('\r',' ').Replace('\n',' ');lines.Enqueue(line);while(lines.Count>max)lines.Dequeue();var path=Path.Combine(folder,"engine.log");Paths.NoReparse(path);var bytes=Encoding.UTF8.GetBytes(line+Environment.NewLine);if(File.Exists(path)&&new FileInfo(path).Length+bytes.Length>1048576){var older=Path.Combine(folder,"engine.2.log");var old=Path.Combine(folder,"engine.1.log");Paths.NoReparse(old);Paths.NoReparse(older);if(File.Exists(old))File.Move(old,older,true);File.Move(path,old,true);}using var f=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.Read);f.Write(bytes);}}
 public string[] Snapshot(){lock(gate)return lines.ToArray();}
}
