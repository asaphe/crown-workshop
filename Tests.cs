using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CrownWorkshop;
class Tests {
    static int count;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;}
    static void Reject(Action a,string name){bool failed=false;try{a();}catch{failed=true;}Check(failed,name);}
    static void Main(string[] args){
        byte[] original=File.ReadAllBytes(args[0]);var s=new Save(original);
        Check(s.Render().SequenceEqual(original),"No-op byte preservation");
        Check(s.Entities.Count==52&&s.Player.Id==3&&s.Player.Number("CommanderId")==39,"Known player relationships");
        int mutated=0;
        foreach(var f in s.Fields.Where(x=>x.Editable)) {
            string v;
            switch(f.Kind){case "String":v=f.Original+" test Ω";break;case "Boolean":v=f.Original=="True"?"False":"True";break;case "Guid":v=Guid.NewGuid().ToString();break;case "Char":v="Z";break;default:v=f.Original=="1"?"2":"1";break;}
            f.Pending=v;var bytes=s.Render();Check(!bytes.SequenceEqual(original),"Mutated "+f.Path);var read=new Save(bytes);
            Check(read.Fields[s.Fields.IndexOf(f)].Original==v,"Readback "+f.Path);
            // Every byte outside the replacement span must remain untouched, even for resized strings.
            var encoded=Save.Encode(f,v);Check(bytes.Take(f.Entry.Offset).SequenceEqual(original.Take(f.Entry.Offset)),"Prefix preserved");
            Check(bytes.Skip(f.Entry.Offset+encoded.Length).SequenceEqual(original.Skip(f.Entry.End)),"Suffix preserved");
            f.Pending=f.Original;mutated++;
        }
        var gold=s.Fields.Single(x=>x.Group=="Treasury 45"&&x.Name=="Gold");gold.Pending="9999";
        var name=s.Fields.Single(x=>x.Group==s.Player.Label&&x.Name=="Name");name.Pending="Asaph of the Crown Ω";
        var troops=s.Fields.Single(x=>x.Group=="Army 39"&&x.Name=="Soldiers");troops.Pending="1234";
        var outBytes=s.Render();var edited=new Save(outBytes);Check(edited.Player.Get("Name").Value.ToString()==name.Pending,"Multi-edit string");Check(edited.Entities.Single(x=>x.Id==39).Number("Soldiers")==1234,"Shifted troop field");
        Reject(()=>Save.Encode(gold,"2147483648"),"Overflow rejected");Reject(()=>Save.Encode(gold,"1.5"),"Fractional integer rejected");
        Reject(()=>new Save(original.Take(original.Length-1).ToArray()),"Truncated save rejected");
        var corrupt=(byte[])original.Clone();corrupt[0]=0;Reject(()=>new Save(corrupt),"Unknown tag rejected");
        if(args[1]=="--read-only"){Console.WriteLine("PASS: "+count+" parser and edit checks; "+mutated+" individually edited fields. No filesystem writes requested.");return;}
        string dir=Path.GetFullPath(args[1]);Directory.CreateDirectory(dir);string path=Path.Combine(dir,"ClassicSave.sav");File.WriteAllBytes(path,original);
        string backup=Save.WriteFile(path,outBytes,original);Check(File.ReadAllBytes(backup).SequenceEqual(original),"Backup contains original");Check(File.ReadAllBytes(path).SequenceEqual(outBytes),"Atomic write result");
        Reject(()=>Save.WriteFile(path,original,original),"Stale file rejected");Check(File.ReadAllBytes(path).SequenceEqual(outBytes),"Stale write leaves file intact");
        Save.WriteFile(path,original,outBytes);Check(File.ReadAllBytes(path).SequenceEqual(original),"Restore backup bytes");
        Check(File.ReadAllBytes(args[0]).SequenceEqual(original),"Live save untouched");
        Console.WriteLine("PASS: "+count+" checks; "+mutated+" individually edited fields, resized Unicode strings, combined edits, corrupt input, backups, stale-save protection, restore. Live save unchanged.");
    }
}
