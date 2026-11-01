using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CrownWorkshop {
public class Entry {
    public int Tag, Start, Offset, End, Ref = -1;
    public string Name = "", Type = "";
    public object Value;
    public Entry Parent;
    public List<Entry> Children = new List<Entry>();
    public IEnumerable<Entry> Walk() { yield return this; foreach(var c in Children) foreach(var x in c.Walk()) yield return x; }
    public Entry Child(string name) { return Children.FirstOrDefault(x=>x.Name==name); }
}
public class Field {
    public Entry Entry;
    public string Group, Name, Path, Original, Pending, Kind;
    public bool Advanced, Editable;
    public Dictionary<string,string> Options;
    public bool Changed { get { return Pending != Original; } }
}
public class Entity {
    public Entry Node;
    public int Id;
    public string Label;
    public Dictionary<string,Entry> Components = new Dictionary<string,Entry>();
    public Entry Get(string tail) { var c=Components.FirstOrDefault(x=>x.Key.EndsWith("."+tail)).Value; return c==null?null:c.Child("Value"); }
    public int Number(string tail) { var v=Get(tail); return v==null?-1:Convert.ToInt32(v.Value); }
    public bool Has(string tail) { return Components.Keys.Any(x=>x.EndsWith("."+tail)); }
}
public class Save {
    static Dictionary<string,Dictionary<string,string>> enumLabels=LoadEnums();
    static Dictionary<string,Dictionary<string,string>> LoadEnums(){var all=new Dictionary<string,Dictionary<string,string>>();string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"enum-labels.tsv");if(File.Exists(path))foreach(string line in File.ReadAllLines(path)){var a=line.Split('\t');if(a.Length!=3)continue;if(!all.ContainsKey(a[0]))all[a[0]]=new Dictionary<string,string>();all[a[0]][a[1]]=a[2];}return all;}
    public string Mode;
    public byte[] Bytes;
    public Entry Root;
    public List<Field> Fields = new List<Field>();
    public List<Entity> Entities = new List<Entity>();
    public Dictionary<int,Entry> Refs = new Dictionary<int,Entry>();
    Dictionary<int,string> types = new Dictionary<int,string>();
    int pos;
    static CultureInfo CI = CultureInfo.InvariantCulture;
    public Save(byte[] bytes) {
        Bytes=bytes; if(bytes.Length>32*1024*1024) throw new Exception("Save exceeds 32 MB limit.");
        Root=Read(null,0);
        if(pos<bytes.Length && bytes[pos]==49) pos++;
        if(pos!=bytes.Length) throw new Exception("Unexpected trailing save data.");
        if(!Root.Type.StartsWith("Code.Common.Infrastructure.Progress.GameData.SavedGameData,")) throw new Exception("This is not a campaign save. Account progress is not supported.");
        if(Root.Walk().Any(x=>x.Type.StartsWith("Code.Modes.Roguelike.") || (x.Value is string && ((string)x.Value).StartsWith("Code.Modes.Roguelike.")))) throw new Exception("Kingdom saves are not supported yet.");
        BuildFields();
        Mode=Entities.Any(e=>e.Components.Keys.Any(k=>k.StartsWith("Code.Modes.Updated.")))?"Classic":"Retro";
    }
    void Need(int n) { if(n<0 || n>Bytes.Length-pos) throw new Exception("Truncated or invalid save at byte "+pos); }
    byte B() { Need(1); return Bytes[pos++]; }
    int I() { Need(4); int v=BitConverter.ToInt32(Bytes,pos); pos+=4; return v; }
    long L() { Need(8); long v=BitConverter.ToInt64(Bytes,pos); pos+=8; return v; }
    string S() { int wide=B(), n=I(); if(wide>1 || n<0 || n>Bytes.Length) throw new Exception("Invalid string."); int count=checked(n*(wide+1)); Need(count); string s=(wide==1?Encoding.Unicode:Encoding.GetEncoding(28591)).GetString(Bytes,pos,count); pos+=count; return s; }
    string T() { int t=B(); if(t==46)return ""; int id=I(); if(t==47) { string s=S(); types.Add(id,s); return s; } if(t==48 && types.ContainsKey(id)) return types[id]; throw new Exception("Unknown serialized type."); }
    Entry Read(Entry parent,int depth) {
        if(depth>128)throw new Exception("Excessive nesting.");
        var e=new Entry{Start=pos,Parent=parent,Tag=B()}; int t=e.Tag;
        if(t==1||t==3||(t>=9&&t<=46&&t%2==1)||t==50) e.Name=S();
        if(t>=1&&t<=4) {
            e.Type=T(); if(t<=2) {e.Ref=I(); if(e.Ref>=0) Refs.Add(e.Ref,e);}
            while(true) { Need(1); if(Bytes[pos]==5){pos++;break;} e.Children.Add(Read(e,depth+1)); }
        } else if(t==6) {
            long n=L(); if(n<0||n>Bytes.Length)throw new Exception("Invalid collection length.");
            for(long i=0;i<n;i++)e.Children.Add(Read(e,depth+1));
            if(B()!=7)throw new Exception("Collection count mismatch.");
        } else if(t==8) {
            int n=I(), size=I(); if(n<0||size<1||size>16)throw new Exception("Invalid primitive array.");
            int count=checked(n*size); Need(count); e.Offset=pos; e.Value=BitConverter.ToString(Bytes,pos,count); pos+=count;
        } else {
            e.Offset=pos;
            int k=t>=9&&t<=46 ? (t%2==0?t-1:t):t;
            switch(k) {
                case 9: case 11: e.Value=I();break;
                case 13: case 41: Need(16); var g=new byte[16]; Buffer.BlockCopy(Bytes,pos,g,0,16); e.Value=new Guid(g);pos+=16;break;
                case 15:e.Value=unchecked((sbyte)B());break;
                case 17:e.Value=B();break;
                case 19:Need(2);e.Value=BitConverter.ToInt16(Bytes,pos);pos+=2;break;
                case 21:Need(2);e.Value=BitConverter.ToUInt16(Bytes,pos);pos+=2;break;
                case 23:e.Value=I();break;
                case 25:Need(4);e.Value=BitConverter.ToUInt32(Bytes,pos);pos+=4;break;
                case 27:e.Value=L();break;
                case 29:Need(8);e.Value=BitConverter.ToUInt64(Bytes,pos);pos+=8;break;
                case 31:Need(4);e.Value=BitConverter.ToSingle(Bytes,pos);pos+=4;break;
                case 33:Need(8);e.Value=BitConverter.ToDouble(Bytes,pos);pos+=8;break;
                case 35:e.Value=new decimal(new int[]{I(),I(),I(),I()});break;
                case 37:Need(2);e.Value=(char)BitConverter.ToUInt16(Bytes,pos);pos+=2;break;
                case 39:case 50:case 51:e.Value=S();break;
                case 43:int bv=B();if(bv>1)throw new Exception("Invalid boolean.");e.Value=bv==1;break;
                case 45:e.Value=null;break;
                default:throw new Exception("Unknown entry "+t+" at "+e.Start);
            }
        }
        e.End=pos;return e;
    }
    public Entry Resolve(Entry e) { if(e!=null&&(e.Tag==9||e.Tag==10)) { Entry r; return Refs.TryGetValue((int)e.Value,out r)?r:null; } return e; }
    public static string Short(string t) { return t.Split(',')[0]; }
    public static string Display(object v) { if(v==null)return "null"; if(v is float)return ((float)v).ToString("R",CI); if(v is double)return ((double)v).ToString("R",CI); return Convert.ToString(v,CI); }
    string PathOf(Entry e,Entry stop) { if(e==stop||e==null)return ""; string part=e.Name; if(part=="")part="["+e.Parent.Children.IndexOf(e)+"]"; string p=PathOf(e.Parent,stop); return p==""?part:p+" / "+part; }
    public Entity Player { get { return Entities.FirstOrDefault(x=>x.Has("Player")); } }
    void BuildFields() {
        foreach(var n in Root.Walk().Where(x=>Short(x.Type)=="Code.Common.Infrastructure.EntitySerialization.SavedEntity")) {
            var entity=new Entity{Node=n}; var components=n.Child("Components"); if(components==null)continue;
            var array=components.Children.FirstOrDefault(x=>x.Tag==6); if(array==null)continue;
            foreach(var c in array.Children) {
                var ct=Resolve(c.Child("ComponentType")); if(ct==null)continue;
                var v=ct.Children.FirstOrDefault(); if(v!=null&&v.Value is string) entity.Components[Short((string)v.Value)]=c;
            }
            entity.Id=entity.Number("Id"); var name=entity.Get("NameWithTitle")??entity.Get("Name");
            string category=entity.Has("Lord")?"Lord":entity.Has("Commander")?"Army":entity.Has("Castle")?"Castle":entity.Has("Storage")?"Treasury":entity.Has("Region")?"Region":"Entity";
            entity.Label=(name!=null?Display(name.Value):category+" "+entity.Id)+(entity.Has("Player")?" (you)":entity.Has("OpponentComponent")?" (tournament)":""); Entities.Add(entity);
        }
        foreach(var entity in Entities) foreach(var pair in entity.Components) {
            var c=pair.Value; string component=pair.Key.Substring(pair.Key.LastIndexOf('.')+1);
            var values=c.Children.Where(x=>x.Name!="ComponentType").SelectMany(x=>x.Walk()).Where(x=>x.Children.Count==0&&x.Tag!=6&&x.Tag!=8).ToList();
            if(values.Count==0) Fields.Add(new Field{Entry=c,Group=entity.Label,Name=component,Path=pair.Key,Original="Present",Pending="Present",Kind="Component flag",Editable=false});
            foreach(var v in values) AddField(v,entity.Label,component+(v.Name=="Value"?"":" / "+PathOf(v,c)),pair.Key+" / "+PathOf(v,c),component);
        }
        var entityNodes=new HashSet<Entry>(Entities.SelectMany(x=>x.Node.Walk()));
        foreach(var e in Root.Walk().Where(x=>!entityNodes.Contains(x)&&x.Children.Count==0&&x.Tag!=6&&x.Tag!=8)) AddField(e,"Campaign",PathOf(e,Root),PathOf(e,Root),e.Name);
        Fields=Fields.OrderBy(x=>x.Entry.Start).ToList();
        if(Player==null)throw new Exception("Player entity not found.");
    }
    void AddField(Entry e,string group,string name,string path,string component) {
        bool editable=e.Tag>=15&&e.Tag<=44;
        bool advanced=component=="Id"||component.EndsWith("Id")||component.Contains("IdComponent")||component=="ViewPath"||component=="LastGameEntityId"||e.Name=="$k";
        var f=new Field{Entry=e,Group=group,Name=name,Path=path,Original=Display(e.Value),Pending=Display(e.Value),Kind=e.Value==null?"Null":e.Value.GetType().Name,Editable=editable,Advanced=advanced};
        if(e.Value is ulong && e.Name!="$v")for(var p=e.Parent;p!=null;p=p.Parent){var match=Regex.Match(p.Type,@"\[\[([^,\]]+),");Dictionary<string,string> opts;if(match.Success&&enumLabels.TryGetValue(match.Groups[1].Value,out opts)){f.Options=opts;break;}}
        if(e.Name!="$k"&&e.Parent!=null&&e.Parent.Type=="System.DateTime, mscorlib")f.Advanced=true;
        Fields.Add(f);
    }
    public static byte[] Encode(Field f,string text) {
        var e=f.Entry; int k=e.Tag%2==0?e.Tag-1:e.Tag;
        using(var ms=new MemoryStream()) using(var w=new BinaryWriter(ms)) {
            switch(k) {
                case 15:w.Write(sbyte.Parse(text,CI));break;case 17:w.Write(byte.Parse(text,CI));break;
                case 19:w.Write(short.Parse(text,CI));break;case 21:w.Write(ushort.Parse(text,CI));break;
                case 23:w.Write(int.Parse(text,CI));break;case 25:w.Write(uint.Parse(text,CI));break;
                case 27:w.Write(long.Parse(text,CI));break;case 29:w.Write(ulong.Parse(text,CI));break;
                case 31:float fl=float.Parse(text,CI);if(float.IsInfinity(fl)||float.IsNaN(fl))throw new Exception("Use a finite number.");w.Write(fl);break;
                case 33:double d=double.Parse(text,CI);if(double.IsInfinity(d)||double.IsNaN(d))throw new Exception("Use a finite number.");w.Write(d);break;
                case 35:foreach(int i in decimal.GetBits(decimal.Parse(text,CI)))w.Write(i);break;
                case 37:if(text.Length!=1)throw new Exception("Enter one character.");w.Write((ushort)text[0]);break;
                case 39:if(text.Length>100000)throw new Exception("Text is too long.");w.Write((byte)1);w.Write(text.Length);w.Write(Encoding.Unicode.GetBytes(text));break;
                case 41:w.Write(Guid.Parse(text).ToByteArray());break;
                case 43:w.Write(bool.Parse(text));break;
                default:throw new Exception("Structural entry is read only.");
            } return ms.ToArray();
        }
    }
    public byte[] Render() {
        using(var ms=new MemoryStream()) {
            int last=0;
            foreach(var f in Fields.Where(x=>x.Changed).OrderBy(x=>x.Entry.Offset)) {
                if(!f.Editable)throw new Exception("Cannot edit structural entries.");
                var e=f.Entry; if(e.Offset<last)throw new Exception("Overlapping edits.");
                ms.Write(Bytes,last,e.Offset-last);var b=Encode(f,f.Pending);ms.Write(b,0,b.Length);last=e.End;
            }
            ms.Write(Bytes,last,Bytes.Length-last);var result=ms.ToArray();var check=new Save(result);
            if(check.Fields.Count!=Fields.Count)throw new Exception("Validation changed field count.");
            for(int i=0;i<Fields.Count;i++) {
                var a=Fields[i];var b=check.Fields[i];
                if(a.Path!=b.Path||a.Kind!=b.Kind)throw new Exception("Validation changed schema.");
                if(a.Changed) { if(!Encode(a,a.Pending).SequenceEqual(Encode(b,b.Original)))throw new Exception("Edit verification failed."); }
                else if(a.Original!=b.Original)throw new Exception("Unrelated value changed.");
            }
            return result;
        }
    }
    public static bool GameRunning() { return Process.GetProcessesByName("Defender of the Crown Collection").Length>0; }
    public static string WriteFile(string path,byte[] data,byte[] expected) {
        if(GameRunning())throw new Exception("Close Defender of the Crown before saving.");
        new Save(data);
        bool exists=File.Exists(path); if(expected!=null&&(!exists||!File.ReadAllBytes(path).SequenceEqual(expected)))throw new Exception("The save changed on disk. Reopen it before saving.");
        string backup=null;
        if(exists) { string dir=Path.Combine(Path.GetDirectoryName(path),"CrownWorkshopBackups");Directory.CreateDirectory(dir); backup=Path.Combine(dir,Path.GetFileName(path)+"."+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"."+Guid.NewGuid().ToString("N").Substring(0,6)+".bak");File.Copy(path,backup); }
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try { using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {stream.Write(data,0,data.Length);stream.Flush(true);} if(exists)File.Replace(temp,path,null);else File.Move(temp,path); } finally {if(File.Exists(temp))File.Delete(temp);}
        return backup;
    }
}
}
