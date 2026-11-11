using System.Text.Json;
namespace FlexTimeChecker.Tests;
public static partial class SelfTests
{
    sealed class Sandbox : IDisposable
    {
        public string Root {get;} = Path.Combine(AppContext.BaseDirectory,"test-data",Guid.NewGuid().ToString("N"));
        public Sandbox() { Directory.CreateDirectory(Root); }
        public string Put(string name,string content) { var p=Path.Combine(Root,name); Directory.CreateDirectory(Path.GetDirectoryName(p)!); File.WriteAllText(p,content); return p; }
        public void Dispose() { Directory.Delete(Root,true); }
    }
    static string Xml(string housing,string stamp="2026-08-26T10:00:00+08:00",string item="") => $"<root><header timeStamp=\"{stamp}\"/><partProcessed identifier=\"{housing}\"/>{item}</root>";
    static JsonElement Tree(object obj) => JsonSerializer.SerializeToElement(obj);
    static void RegisterXml()
    {
        Tests.Add(("XML duplicate business headers or housings fail closed",()=>{
            using var b=new Sandbox();
            foreach(var extra in new[]{"<header timeStamp=\"2000-01-01T00:00:00+08:00\"/>","<partProcessed identifier=\"OTHER\"/>"})
            {
                var p=b.Put("Request_duplicate.xml",Xml("H",item:extra));
                Throws(()=>Call("RequestReader","Read",p,""));
            }
        }));
        Tests.Add(("XML exact qualified element names prevent metadata overriding business fields",()=>{
            using var b=new Sandbox();
            var p=b.Put("Request_spoof.xml","<root xmlns:m=\"urn:metadata\"><header timeStamp=\"2026-08-26T10:00:00+08:00\"/><partProcessed identifier=\"H\"/><m:header timeStamp=\"2000-01-01T00:00:00+08:00\"/><m:partProcessed identifier=\"WRONG\"/><m:item serial=\"M\"/></root>");
            var r=Tree(Call("RequestReader","Read",p,"M"));
            Check(r.GetProperty("Stamp").GetDateTimeOffset()==DateTimeOffset.Parse("2026-08-26T10:00:00+08:00"),"prefixed metadata header must not replace real time");
            Check(r.GetProperty("Housing").GetString()=="H"&&!r.GetProperty("MatchesMaterial").GetBoolean(),"prefixed metadata elements are not business elements");
            var query=FlexTimeChecker.Core.SearchEngine.Run(new(){Folders=[new(){MessageFolder=b.Root}]},"H").Query;
            Check(query.Found&&!FlexTimeChecker.Core.WaitRules.Evaluate(DateTimeOffset.Parse("2026-08-26T10:01:00+08:00")-query.Stamp,30,240).CanClear,"metadata timestamp cannot prematurely authorize clear glue");
            var prefixed=b.Put("Prefixed_only.xml","<m:root xmlns:m=\"urn:other\"><m:header timeStamp=\"2000-01-01T00:00:00+08:00\"/><m:partProcessed identifier=\"H\"/></m:root>");
            Throws(()=>Call("RequestReader","Read",prefixed,""));
            var unprefixed=b.Put("Request_default.xml",Xml("H",item:"<item serial=\"M\"/>").Replace("<root>","<root xmlns=\"urn:default\">"));
            Check(Tree(Call("RequestReader","Read",unprefixed,"M")).GetProperty("MatchesMaterial").GetBoolean(),"legacy reader.Name accepts unprefixed elements with default namespace");
        }));
        Tests.Add(("XML namespaced custom item attributes remain material identifiers",()=>{
            using var b=new Sandbox(); var p=b.Put("Request_ns.xml",Xml("H",item:"<item xmlns:m=\"urn:metadata\" m:serial=\"M-CODE\"/>"));
            Check(Tree(Call("RequestReader","Read",p,"m-code")).GetProperty("MatchesMaterial").GetBoolean(),"non-schema namespaced attributes preserved");
            Check(!Tree(Call("RequestReader","Read",p,"urn:metadata")).GetProperty("MatchesMaterial").GetBoolean(),"namespace declaration is not material");
        }));
        Tests.Add(("XML safety size DTD malformed and identifier limits",()=>{
            using var box=new Sandbox();
            foreach(var content in new[]{ Xml("H",item:"<!--"+new string('x',2097152)+"-->"), Xml(new string('H',513)), "<!DOCTYPE root [<!ENTITY x 'H'>]><root><header timeStamp=\"2026-01-01\"/><partProcessed identifier=\"&x;\"/></root>", "<root>broken" })
            { var p=box.Put("Request_bad.xml",content); try { Call("RequestReader","Read",p,""); } catch(Exception e) when(e.GetBaseException() is InvalidDataException or System.Xml.XmlException) { continue; } throw new Exception("unsafe XML accepted"); }
        }));
        Tests.Add(("XML exact material all non-schema attributes and housing",()=>{
            using var box=new Sandbox();
            var p=box.Put("Request_a.xml",Xml("H-1",item:"<item name=\"SCHEMA\" dataType=\"SCHEMA\" typeNo=\"SCHEMA\" typeVar=\"SCHEMA\" batch=\"SCHEMA\" class=\"SCHEMA\" vendor=\"SCHEMA\" qty=\"SCHEMA\" customSerial=\"CODE\"/>"));
            var read=Tree(Call("RequestReader","Read",p,"code"));
            Check(read.GetProperty("Housing").GetString()=="H-1" && read.GetProperty("MatchesMaterial").GetBoolean(),"custom material exact case-insensitive");
            Check(!Tree(Call("RequestReader","Read",p,"COD")).GetProperty("MatchesMaterial").GetBoolean(),"no substrings");
            Check(!Tree(Call("RequestReader","Read",p,"SCHEMA")).GetProperty("MatchesMaterial").GetBoolean(),"schema excluded");
        }));
    }
}
