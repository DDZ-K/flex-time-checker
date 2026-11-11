using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace FlexExtractor;
internal static class Native {
 [StructLayout(LayoutKind.Sequential)]internal struct Basic {public long PerProcessUserTimeLimit,PerJobUserTimeLimit;public uint LimitFlags;public UIntPtr MinimumWorkingSetSize,MaximumWorkingSetSize;public uint ActiveProcessLimit;public UIntPtr Affinity;public uint PriorityClass,SchedulingClass;}
 [StructLayout(LayoutKind.Sequential)]internal struct Io {public ulong ReadOperationCount,WriteOperationCount,OtherOperationCount,ReadTransferCount,WriteTransferCount,OtherTransferCount;}
 [StructLayout(LayoutKind.Sequential)]internal struct Extended {public Basic BasicLimitInformation;public Io IoInfo;public UIntPtr ProcessMemoryLimit,JobMemoryLimit,PeakProcessMemoryUsed,PeakJobMemoryUsed;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateJobObject(IntPtr attributes,string? name);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetInformationJobObject(SafeFileHandle job,int type,ref Extended info,uint length);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool AssignProcessToJobObject(SafeFileHandle job,IntPtr process);
 [DllImport("kernel32.dll",SetLastError=true)]internal static extern bool TerminateJobObject(SafeFileHandle job,uint code);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool DuplicateHandle(IntPtr sourceProcess,SafeFileHandle source,IntPtr targetProcess,out IntPtr target,uint access,bool inherit,uint options);
 [DllImport("kernel32.dll")]static extern IntPtr GetCurrentProcess();
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint GetFinalPathNameByHandle(SafeFileHandle file,System.Text.StringBuilder path,uint chars,uint flags);
 public static SafeFileHandle CreateJob(){var h=new SafeFileHandle(CreateJobObject(IntPtr.Zero,null),true);if(h.IsInvalid)throw new Win32Exception();var info=new Extended{BasicLimitInformation=new Basic{LimitFlags=0x2000|0x8,ActiveProcessLimit=1}};if(!SetInformationJobObject(h,9,ref info,(uint)Marshal.SizeOf<Extended>())){h.Dispose();throw new Win32Exception();}return h;}
 public static long Attach(SafeFileHandle job,System.Diagnostics.Process process,OwnerLease lease){if(!AssignProcessToJobObject(job,process.Handle))throw new Win32Exception();if(!DuplicateHandle(GetCurrentProcess(),lease.Handle.SafeFileHandle,process.Handle,out var copied,0,false,2))throw new Win32Exception();return copied.ToInt64();}
 public static void VerifyLease(long handle,string root){using var h=new SafeFileHandle(new IntPtr(handle),false);var b=new System.Text.StringBuilder(32768);if(handle==0||GetFinalPathNameByHandle(h,b,(uint)b.Capacity,0)==0)throw new InvalidDataException("缺少独占引擎句柄");var path=b.ToString();if(path.StartsWith(@"\\?\"))path=path[4..];if(!path.Equals(Path.Combine(root,"engine.lease"),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("独占句柄路径不符");}
}
