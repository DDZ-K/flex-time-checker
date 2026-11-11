using System.Text.Json;
namespace FlexExtractor;
public sealed class OwnedFile {
 public string Name {get;set;}="";
 public string Hash {get;set;}="";
 public DateTime CreationUtc {get;set;}
 public DateTime WriteUtc {get;set;}
 public DateTime RetainUtc {get;set;}
 public bool Committed {get;set;}
}
public sealed class Ownership {
 public const string Manifest=".flex-ownership-v2.json";
 [System.Text.Json.Serialization.JsonRequired] public List<OwnedFile> Files {get;set;}=[];
 public static Ownership Load(string folder){Paths.NoReparse(folder);var path=Path.Combine(folder,Manifest);if(!File.Exists(path))return new();var m=JsonSerializer.Deserialize<Ownership>(Durable.Read(path,8388608),Config.Json)??throw new InvalidDataException("所有权清单为空");if(m.Files==null||m.Files.Count>20000)throw new InvalidDataException("所有权清单超过20000条");var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var f in m.Files){if(f==null||string.IsNullOrEmpty(f.Name)||f.Hash==null||f.Name!=Path.GetFileName(f.Name)||!f.Name.StartsWith("Request",StringComparison.OrdinalIgnoreCase)||!f.Name.EndsWith(".xml",StringComparison.OrdinalIgnoreCase)||!names.Add(f.Name)||f.Hash.Length!=64)throw new InvalidDataException("所有权清单无效");}return m;}
 public void Save(string folder){if(Files.Count>20000)throw new InvalidDataException("所有权上限20000，停止复制，增加保留空间前请先清理");Durable.Write(Path.Combine(folder,Manifest),JsonSerializer.Serialize(this,Config.Json),8388608);}
 public static void Purge(string folder,int keepDays,DateTime nowUtc,ScanResult result)=>PurgeChecked(folder,keepDays,nowUtc,result,()=>{});
 internal static void PurgeChecked(string folder,int keepDays,DateTime nowUtc,ScanResult result,Action validateDelete){
  Paths.NoReparse(folder);if(!Directory.Exists(folder))return;var m=Load(folder);bool changed=false;
  foreach(var item in m.Files.ToArray()){
   var path=Path.Combine(folder,item.Name);if(!File.Exists(path)){m.Files.Remove(item);changed=true;continue;}
   if(!item.Committed){Paths.NoReparse(path);var pending=new FileInfo(path);if(pending.CreationTimeUtc!=item.CreationUtc||pending.LastWriteTimeUtc!=item.WriteUtc||Durable.HashFile(path)!=item.Hash){result.Error(new IOException("未提交目标指纹不同，保留文件等待人工核对 "+item.Name));continue;}item.Committed=true;changed=true;}
   if(item.RetainUtc>=nowUtc.AddDays(-keepDays))continue;
   try{Paths.NoReparse(path);var info=new FileInfo(path);if(info.CreationTimeUtc!=item.CreationUtc||info.LastWriteTimeUtc!=item.WriteUtc||Durable.HashFile(path)!=item.Hash)throw new IOException("所有权指纹已改变；拒绝删除 "+item.Name);validateDelete();Paths.NoReparse(path);File.Delete(path);m.Files.Remove(item);changed=true;result.Deleted++;}
   catch(Exception e) when(e is IOException or UnauthorizedAccessException or InvalidDataException){result.Error(e);}
  }
  if(changed)m.Save(folder);
 }
}
