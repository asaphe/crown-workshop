using System;
using System.Linq;
using System.IO;
using System.Web.Script.Serialization;
using CrownWorkshop;
class SessionTests {
    static int n;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);n++;}
    static void Main(string[] args){var s=new WorkshopSession();s.Load(args[0]);byte[] before=File.ReadAllBytes(args[0]);var j=new JavaScriptSerializer();string model=j.Serialize(s.State());Check(model.Contains("\"loaded\":true"),"Model serializes");var p=s.Save.Player;
        var treasury=s.Save.Entities.Single(e=>e.Id==p.Number("StorageId"));var gold=s.Save.Fields.FindIndex(f=>f.Group==treasury.Label&&f.Name=="Gold");var old=s.Save.Fields[gold].Original;
        s.Set(gold,"321",false);Check(s.Dirty,"Dirty state");Check(j.Serialize(s.State()).Contains("\"value\":\"321\""),"Updated model");var rendered=new Save(s.Save.Render());Check(rendered.Entities.Single(e=>e.Id==treasury.Id).Number("Gold")==321,"Rendered edit");s.Undo();Check(!s.Dirty,"Undo");
        bool invalid=false;try{s.Set(gold,"oops",false);}catch{invalid=true;}Check(invalid&&!s.Dirty,"Invalid edit rejected");
        var id=s.Save.Fields.FindIndex(f=>f.Group==p.Label&&f.Name=="CommanderId");invalid=false;try{s.Set(id,"1",false);}catch{invalid=true;}Check(invalid,"Advanced ID protected");
        s.Set(gold,"555",false);s.Revert();Check(!s.Dirty,"Revert");s.Undo();Check(s.Save.Fields[gold].Pending=="555","Revert undo");s.Revert();
        foreach(var e in s.Save.Entities.Where(e=>e.Has("Storage")||e.Has("Commander")||e.Has("Castle")))Check(s.Owner(e)!=null,"Ownership "+e.Id);
        Check(s.Save.Entities.Select(e=>e.Label).Distinct().Count()==s.Save.Entities.Count,"Unique record labels");Check(before.SequenceEqual(File.ReadAllBytes(args[0])),"Live file untouched");Console.WriteLine("PASS: "+n+" desktop session checks; no files modified.");
    }
}
