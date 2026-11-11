namespace FlexExtractor;
public sealed class ScanResult {
 public string Generation {get;set;}="";
 public int Copied {get;set;}
 public int Deleted {get;set;}
 public int Skipped {get;set;}
 public int Failed {get;set;}
 public string RecentFile {get;set;}="";
 public string RecentStation {get;set;}="";
 public List<string> Messages {get;set;}=[];
 public List<string> Folders {get;set;}=[];
 public void Error(Exception ex){Failed++;if(Messages.Count<32)Messages.Add(ex.GetType().Name+": "+ex.Message[..Math.Min(512,ex.Message.Length)]);}
}
public static class Scanner {
 public static ScanResult Run(Config c,string operation,DateTime baselineUtc,string cursorPath) {
  c.Validate();var r=new ScanResult();
  if(operation=="purge"){foreach(var folder in c.AllItems.Select(x=>x.SaveFolder).Distinct(StringComparer.OrdinalIgnoreCase)){try{Ownership.PurgeChecked(folder,c.KeepDays,DateTime.UtcNow,r,c.Validate);}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException){r.Error(new IOException("清理目标 "+folder+": "+ex.Message,ex));}}return r;}
  foreach(var folder in c.AllItems.Select(x=>x.SaveFolder).Distinct(StringComparer.OrdinalIgnoreCase))Paths.NoReparse(folder);
  Paths.NoReparse(c.WatchRoot);
  if(operation=="discover"){var found=new SortedSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var date in Dates(c))foreach(var station in Directory.EnumerateDirectories(date)){if(!c.AllItems.Any(x=>Matches(Path.GetFileName(station),x.Token)))continue;foreach(var sub in WalkDirectories(station,0)){var rel=Path.GetRelativePath(station,sub).Replace('\\','/');if(rel.Length>256)throw new InvalidDataException("发现子目录长度超过256");found.Add(rel);if(found.Count>128)throw new InvalidDataException("发现子目录超过128，请缩小报文范围");}}r.Folders=found.ToList();return r;}
  if(operation!="scan")throw new InvalidDataException("未知工作操作");
  var cursor=File.Exists(cursorPath)?Durable.Read(cursorPath,32768):"";bool seeking=cursor.Length>0;int examined=0;
  foreach(var date in Dates(c))foreach(var station in Entries(date,false,r)){
   var hits=c.AllItems.Where(x=>Matches(Path.GetFileName(station),x.Token)).ToArray();
   if(hits.Length==0)continue;
   foreach(var file in WalkFiles(station,0,station,hits,r)){
    if(seeking){if(file.Equals(cursor,StringComparison.OrdinalIgnoreCase))seeking=false;continue;}
    Durable.Write(cursorPath,file,32768);
    foreach(var hit in hits){var relative=Path.GetRelativePath(station,Path.GetDirectoryName(file)!).Replace('\\','/');if(hit.SubFolders.Count>0&&!hit.SubFolders.Any(s=>relative.Equals(s.Replace('\\','/'),StringComparison.OrdinalIgnoreCase)||relative.StartsWith(s.Replace('\\','/')+"/",StringComparison.OrdinalIgnoreCase)))continue;try {var info=new FileInfo(file);var newest=info.CreationTimeUtc>info.LastWriteTimeUtc?info.CreationTimeUtc:info.LastWriteTimeUtc;if(newest<DateTime.UtcNow.AddDays(-c.KeepDays)||(c.OnlyNewAfterStart&&newest<baselineUtc.AddSeconds(-c.NewFileGraceSec))){r.Skipped++;continue;}if(Copy(c,file,hit.SaveFolder)){r.Copied++;r.RecentFile=file[..Math.Min(1024,file.Length)];r.RecentStation=hit.StationName;}else r.Skipped++;}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Xml.XmlException or InvalidDataException){r.Error(ex);}}
    if(++examined>=c.MaxFilesPerPass)return r;
   }
  }
  Durable.Write(cursorPath,"",32768);return r;
 }
 static bool Copy(Config c,string source,string folder){
  Paths.NoReparse(source);Paths.NoReparse(folder);Directory.CreateDirectory(folder);var temp=Path.Combine(folder,".flex-copy-v2.tmp");Paths.NoReparse(temp);
  try {
   var before=new FileInfo(source);var size=before.Length;var stamp=before.LastWriteTimeUtc;var created=before.CreationTimeUtc;
   if(size>c.MaxFileBytes||size<c.MinFileBytes)throw new InvalidDataException("文件大小超限");
   for(int i=0;i<c.SizeStableChecks;i++){Thread.Sleep(c.SizeStableIntervalMs);before.Refresh();if(!before.Exists||before.Length!=size||before.LastWriteTimeUtc!=stamp||before.CreationTimeUtc!=created)throw new IOException("源仍在写入，请稍后重试");}
   using var src=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read);
   if(src.Length!=size)throw new IOException("源大小在打开时变化");
   using(var dst=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){src.CopyTo(dst,65536);dst.Flush(true);}
   src.Position=0;var sourceHash=Durable.Hash(src);
   if(Durable.HashFile(temp)!=sourceHash)throw new IOException("源复制前后内容变化");
   before.Refresh();if(before.Length!=size||before.LastWriteTimeUtc!=stamp||before.CreationTimeUtc!=created)throw new IOException("源复制前后元数据变化");
   using(var xml=System.Xml.XmlReader.Create(temp,new(){DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=c.MaxFileBytes})){while(xml.Read()){} }
   var pathHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(source).ToUpperInvariant())));
   var dataHash=Durable.HashFile(temp);
   var stem=Path.GetFileNameWithoutExtension(source);stem=stem[..Math.Min(72,stem.Length)];
   var dest=Path.Combine(folder,stem+"__"+pathHash[..16]+"_"+dataHash[..16]+".xml");
   Paths.NoReparse(dest);File.SetLastWriteTimeUtc(temp,File.GetLastWriteTimeUtc(source));
   if(File.Exists(dest)){using var existing=File.OpenRead(dest);if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(existing))!=dataHash)throw new IOException("目标冲突，拒绝覆盖 "+dest);return false;}
   else {
    var manifest=Ownership.Load(folder);if(manifest.Files.Any(x=>x.Name.Equals(Path.GetFileName(dest),StringComparison.OrdinalIgnoreCase)))throw new IOException("所有权目标记录冲突");
    var sourceInfo=new FileInfo(source);File.SetCreationTimeUtc(temp,sourceInfo.CreationTimeUtc);
    var record=new OwnedFile{Name=Path.GetFileName(dest),Hash=dataHash,CreationUtc=File.GetCreationTimeUtc(temp),WriteUtc=File.GetLastWriteTimeUtc(temp),RetainUtc=sourceInfo.CreationTimeUtc>sourceInfo.LastWriteTimeUtc?sourceInfo.CreationTimeUtc:sourceInfo.LastWriteTimeUtc};
    manifest.Files.Add(record);manifest.Save(folder);File.Move(temp,dest,false);record.Committed=true;manifest.Save(folder);return true;
   }
  } finally {if(File.Exists(temp)){c.Validate();Paths.NoReparse(temp);File.Delete(temp);}}
 }
 static IEnumerable<string> WalkDirectories(string root,int depth){if(depth>64)throw new InvalidDataException("目录深度超过64");Paths.NoReparse(root);foreach(var d in Directory.EnumerateDirectories(root)){Paths.NoReparse(d);yield return d;foreach(var sub in WalkDirectories(d,depth+1))yield return sub;}}
 static bool Selected(string relative,Station[] hits)=>hits.Any(h=>h.SubFolders.Count==0||h.SubFolders.Any(s=>relative.Equals(s.Replace('\\','/'),StringComparison.OrdinalIgnoreCase)||relative.StartsWith(s.Replace('\\','/')+"/",StringComparison.OrdinalIgnoreCase)));
 static bool Relevant(string relative,Station[] hits)=>relative=="."||Selected(relative,hits)||hits.Any(h=>h.SubFolders.Any(s=>s.Replace('\\','/').StartsWith(relative+"/",StringComparison.OrdinalIgnoreCase)));
 // Catch both enumerator creation and lazy MoveNext failures, only for this directory.
 // Never materialize a whole directory or advance the file cursor to hide directory faults.
 static IEnumerator<string>? OpenEntries(string root,bool files,ScanResult r){try{Paths.NoReparse(root);return (files?Directory.EnumerateFiles(root,"Request*.xml"):Directory.EnumerateDirectories(root)).GetEnumerator();}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidDataException){r.Error(ex);return null;}}
 static IEnumerable<string> Entries(string root,bool files,ScanResult r){using var e=OpenEntries(root,files,r);if(e==null)yield break;while(true){string? entry=null;try{if(e.MoveNext())entry=e.Current;}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidDataException){r.Error(ex);}if(entry==null)yield break;yield return entry;}}
 static IEnumerable<string> WalkFiles(string root,int depth,string station,Station[] hits,ScanResult r){
  var relative=Path.GetRelativePath(station,root).Replace('\\','/');if(!Relevant(relative,hits))yield break;
  if(depth>64){r.Error(new InvalidDataException("目录深度超过64 "+root));yield break;}
  if(Selected(relative,hits))foreach(var f in Entries(root,true,r))yield return f;
  foreach(var d in Entries(root,false,r))foreach(var f in WalkFiles(d,depth+1,station,hits,r))yield return f;
 }
 public static IEnumerable<string> Dates(Config c){var found=false;for(int i=0;i<Math.Min(14,Math.Max(c.LookbackDays,c.KeepDays+1));i++){var p=Path.Combine(c.WatchRoot,DateTime.Now.AddDays(-i).ToString("yyyy/MM/dd"));if(Directory.Exists(p)){found=true;yield return p;}}if(!found)yield return c.WatchRoot;}
 public static bool Matches(string folder,string token)=>token is "*"||token.Equals("all",StringComparison.OrdinalIgnoreCase)||folder.Equals(token,StringComparison.OrdinalIgnoreCase)||folder.StartsWith(token+"_",StringComparison.OrdinalIgnoreCase)||(int.TryParse(token,out var n)&&(folder.Equals(n.ToString("D4"),StringComparison.OrdinalIgnoreCase)||folder.StartsWith(n.ToString("D4")+"_",StringComparison.OrdinalIgnoreCase)));
}
