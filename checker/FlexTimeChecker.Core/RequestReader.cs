using System.Globalization;
using System.Xml;
namespace FlexTimeChecker.Core;
public sealed record RequestRecord(string Housing, DateTimeOffset Stamp, bool MatchesMaterial);
public static class RequestReader
{
    static readonly HashSet<string> Schema = new(["name","dataType","typeNo","typeVar","batch","class","vendor","qty"],StringComparer.Ordinal);
    public static RequestRecord Read(string path,string code)
    {
        var before=new FileInfo(path); var length=before.Length; var modified=before.LastWriteTimeUtc;
        if(length>2097152) throw new InvalidDataException("XML 超过 2 MiB");
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2097152,MaxCharactersFromEntities=1024,IgnoreComments=true,IgnoreWhitespace=true});
        string housing=""; DateTimeOffset stamp=default; bool matched=false,headerSeen=false,housingSeen=false;
        while(reader.Read())
        {
            if(reader.NodeType!=XmlNodeType.Element) continue;
            // Match the legacy qualified Name: default namespaces accepted, prefixed elements ignored.
            if(reader.Name=="header")
            {
                if(headerSeen) throw new InvalidDataException("重复 header，无法确定点胶时间");
                headerSeen=true;
                if(!DateTimeOffset.TryParse(reader.GetAttribute("timeStamp"),CultureInfo.InvariantCulture,DateTimeStyles.AssumeLocal,out stamp)) throw new InvalidDataException("时间戳无效");
            }
            else if(reader.Name=="partProcessed")
            {
                if(housingSeen) throw new InvalidDataException("重复 partProcessed，无法确定 Housing");
                housingSeen=true; housing=reader.GetAttribute("identifier")?.Trim()??"";
            }
            else if(reader.Name=="item")
            {
                while(reader.MoveToNextAttribute())
                    if(!Schema.Contains(reader.Name) && reader.NamespaceURI!="http://www.w3.org/2000/xmlns/" && code.Length>0 && reader.Value.Trim().Equals(code,StringComparison.OrdinalIgnoreCase)) matched=true;
                reader.MoveToElement();
            }
        }
        if(housing.Length==0 || housing.Length>512 || stamp==default) throw new InvalidDataException("缺失/超长 Housing 或时间");
        before.Refresh();
        if(!before.Exists || before.Length!=length || before.LastWriteTimeUtc!=modified) throw new IOException("读取期间文件变化，请重试");
        return new(housing,stamp,matched);
    }
}
