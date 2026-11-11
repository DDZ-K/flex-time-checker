"""Exercise the reviewed single-file EXE; all results come from real child processes."""
from pathlib import Path
import json, os, subprocess, sys, time
ROOT=Path(__file__).resolve().parents[1]
EXE=(ROOT/'publish-reviewed/FlexTimeChecker.exe').resolve()
LOG=ROOT/'test-logs/reviewed'
LOG.mkdir(parents=True,exist_ok=True)
env=os.environ.copy()
env['DOTNET_ROOT']=str(LOG/'no-installed-runtime')
env['DOTNET_MULTILEVEL_LOOKUP']='0'
env['DOTNET_BUNDLE_EXTRACT_BASE_DIR']=str(LOG/'bundle-cache')
results=[]
def run(label,args,timeout,report=None):
    start=time.monotonic()
    result=subprocess.run(args,env=env,cwd=ROOT,timeout=timeout,capture_output=True,text=True,encoding='utf-8',errors='replace')
    if report is None:
        (LOG/(label+'.txt')).write_text(result.stdout+result.stderr,encoding='utf-8')
    else:
        assert report.exists(), f'{label} missing actual EXE report'
        print(report.read_text(encoding='utf-8-sig'))
    results.append({'test':label,'exit':result.returncode,'wallSeconds':round(time.monotonic()-start,3)})
    (LOG/'execution.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
    assert result.returncode==0, f'{label} returned {result.returncode}: {result.stdout} {result.stderr}'
for flag in ['--self-test','--ui-test','--soak-seconds']:
    report=LOG/(flag[2:]+'.txt')
    args=[str(EXE),flag]+(['120'] if flag=='--soak-seconds' else [])+['--report',str(report)]
    run(flag[2:],args,200,report)
for script in ['verify_startup','verify_missing_config','verify_instance_lock']:
    run(script,[sys.executable,str(ROOT/'scripts'/f'{script}.py'),str(EXE)],60)
print(json.dumps(results,indent=2))
