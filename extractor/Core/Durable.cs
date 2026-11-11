using System.Text;
using System.Text.Json;
namespace FlexExtractor;
public static class Durable {
 public static string Read(string path,int max){using var s=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(s.Length>max)throw new InvalidDataException("文件超出读取上限 "+path);using var reader=new StreamReader(s,Encoding.UTF8,true,4096);var chars=new char[max+1];var n=reader.ReadBlock(chars,0,chars.Length);if(n>max)throw new InvalidDataException("文本超限");return new string(chars,0,n);}
 public static void Write(string path,string text,int max){var bytes=Encoding.UTF8.GetBytes(text);if(bytes.Length>max)throw new InvalidDataException("文件超出写入上限");Paths.NoReparse(path);var dir=Path.GetDirectoryName(path)!;Directory.CreateDirectory(dir);var temp=path+".new";Paths.NoReparse(temp);using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){f.Write(bytes);f.Flush(true);}File.Move(temp,path,true);}
 public static string Hash(Stream stream)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
 public static string HashFile(string path){Paths.NoReparse(path);using var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);return Hash(f);}
 public static void SaveConfig(string path,Config c){c.Validate();Write(path,JsonSerializer.Serialize(c,Config.Json),65536);}
 public static Config LoadConfig(string path){var c=Config.Parse(Read(path,65536));c.Validate();return c;}
}
