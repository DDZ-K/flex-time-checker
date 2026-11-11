namespace FlexExtractor;
public static class Paths {
 // Never touch a UNC/mapped network source during GUI validation or local purge.
 public static void ValidateLocalSource(string source){if(!source.StartsWith(@"\\")&&new DriveInfo(Path.GetPathRoot(source)!).DriveType!=DriveType.Network)NoReparse(source);}
 public static string Full(string p){if(string.IsNullOrWhiteSpace(p)||!Path.IsPathFullyQualified(p)||p.IndexOf(':',2)>=0||p.StartsWith(@"\\?\")||p.StartsWith(@"\\.\"))throw new InvalidDataException("路径必须为普通绝对路径");return Path.GetFullPath(p).TrimEnd('\\','/');}
 public static bool Overlaps(string a,string b){a=Full(a);b=Full(b);return a.Equals(b,StringComparison.OrdinalIgnoreCase)||a.StartsWith(b+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||b.StartsWith(a+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
 public static void NoReparse(string p) {p=Path.GetFullPath(p); for(string? n=p;n!=null;n=Path.GetDirectoryName(n)){try{if((File.GetAttributes(n)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("拒绝重解析点 "+n);}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}}}
}
