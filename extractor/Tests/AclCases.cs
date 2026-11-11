using FlexExtractor;
using System.Security.AccessControl;
using System.Security.Principal;
static partial class Cases {
 public static void TestOwnerDirectoryRejectsBroadWriteAcl(){var root=Fresh();var state=typeof(MachineState).GetField("TestRoot",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;var previous=state.GetValue(null);state.SetValue(null,root);try{var acl=new DirectoryInfo(root).GetAccessControl();acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid,null),FileSystemRights.Modify,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));new DirectoryInfo(root).SetAccessControl(acl);Rejected(()=>{using var owner=new OwnerLease();});}finally{state.SetValue(null,previous);}}
}
