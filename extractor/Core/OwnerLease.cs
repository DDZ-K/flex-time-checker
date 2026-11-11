using System.Security.AccessControl;
using System.Security.Principal;
namespace FlexExtractor;
public static class MachineState {
 #pragma warning disable CS0649
 internal static string? TestRoot;
 #pragma warning restore CS0649
 public static string Root=>TestRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"FlexExtractor.Sol.v2");
}
public sealed class OwnerLease:IDisposable {
 internal FileStream Handle {get;}
 public OwnerLease(){var root=MachineState.Root;Paths.NoReparse(root);
  if(!Directory.Exists(root)){var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);var user=WindowsIdentity.GetCurrent().User??throw new IOException("无法确定本机身份");foreach(var sid in new[]{user,new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null)})acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));new DirectoryInfo(root).Create(acl);}
  Paths.NoReparse(root);var security=new DirectoryInfo(root).GetAccessControl();var forbidden=new[]{WellKnownSidType.WorldSid,WellKnownSidType.BuiltinUsersSid,WellKnownSidType.AuthenticatedUserSid,WellKnownSidType.AnonymousSid};var write=FileSystemRights.Write|FileSystemRights.Modify|FileSystemRights.Delete|FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership;
  foreach(FileSystemAccessRule rule in security.GetAccessRules(true,true,typeof(SecurityIdentifier))){if(rule.AccessControlType==AccessControlType.Allow&&(rule.FileSystemRights&write)!=0&&forbidden.Any(s=>((SecurityIdentifier)rule.IdentityReference).IsWellKnown(s)))throw new InvalidDataException("独占状态目录允许公共写入；请管理员配置专用账户权限");}
  var path=Path.Combine(root,"engine.lease");Paths.NoReparse(path);Handle=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
 }
 static OwnerLease? poisoned;
 bool unsafeToRelease;
 internal void Poison(){unsafeToRelease=true;poisoned=this;}
 public void Dispose(){if(unsafeToRelease)throw new IOException("无法确认工作进程停止，独占锁保留直到进程退出");Handle.Dispose();GC.KeepAlive(poisoned);}
}
