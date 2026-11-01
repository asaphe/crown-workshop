using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CrownWorkshop {
public class WorkshopSession {
    public Save Save;
    public string FilePath;
    Stack<Dictionary<Field,string>> undo=new Stack<Dictionary<Field,string>>();
    public void Load(string path){var s=new Save(File.ReadAllBytes(path));Save=s;FilePath=Path.GetFullPath(path);undo.Clear();}
    public Entity Owner(Entity e){if(e.Has("Lord"))return e;int id=e.Number("OwnerId");if(id<0)id=e.Number("LordId");return Save.Entities.FirstOrDefault(x=>x.Has("Lord")&&x.Id==id)??Save.Entities.FirstOrDefault(x=>x.Has("Lord")&&(x.Number("CommanderId")==e.Id||x.Number("StorageId")==e.Id||x.Number("HomeCastleId")==e.Id));}
    public string Role(Entity e){return e.Has("Lord")?"Character":e.Has("Commander")?"Army":e.Has("Storage")?"Treasury":e.Has("Castle")?"Castle":e.Has("Region")?"Territory":e.Has("OpponentComponent")?"Tournament":"Campaign";}
    public object State(){
        if(Save==null)return new {loaded=false};
        var entities=Save.Entities.Select(e=>{var owner=Owner(e);return new {id=e.Id,name=e.Label,role=Role(e),owner=owner==null?-1:owner.Id,category=owner==Save.Player?"Player":owner!=null?"Opponents":(e.Has("Region")||e.Has("Castle"))?"Opponents":"Campaign"};}).ToArray();
        var map=Save.Entities.ToDictionary(e=>e.Label,e=>e);
        return new {loaded=true,file=FilePath,name=Save.Player.Label.Replace(" (you)",""),player=Save.Player.Id,canUndo=undo.Count>0,entities=entities,
            fields=Save.Fields.Select((f,i)=>new {id=i,entity=map.ContainsKey(f.Group)?map[f.Group].Id:-1,group=f.Group,name=f.Name,path=f.Path,kind=f.Kind,value=f.Pending,original=f.Original,changed=f.Changed,editable=f.Editable,advanced=f.Advanced,options=f.Options}).ToArray()};
    }
    public void Set(int id,string value,bool advanced){if(Save==null||id<0||id>=Save.Fields.Count)throw new Exception("Field not found.");var f=Save.Fields[id];if(!f.Editable||(f.Advanced&&!advanced))throw new Exception("Enable advanced mode to edit this field.");Save.Encode(f,value);if(f.Pending!=value){undo.Push(new Dictionary<Field,string>{{f,f.Pending}});f.Pending=value;}}
    public void Undo(){if(undo.Count>0)foreach(var kv in undo.Pop())kv.Key.Pending=kv.Value;}
    public void Revert(){var changes=Save.Fields.Where(f=>f.Changed).ToDictionary(f=>f,f=>f.Pending);if(changes.Count>0){undo.Push(changes);foreach(var f in changes.Keys)f.Pending=f.Original;}}
    public bool Dirty {get{return Save!=null&&Save.Fields.Any(x=>x.Changed);}}
}
public class Desktop:Form {
    WebView2 view=new WebView2();WorkshopSession session=new WorkshopSession();JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024};bool allowClose,closePending;
    public Desktop(){Text="Crown Workshop";Width=1400;Height=940;MinimumSize=new Size(1000,720);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(16,17,19);view.Dock=DockStyle.Fill;Controls.Add(view);Shown+=async(s,e)=>{try{var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CrownWorkshop","WebView"));await view.EnsureCoreWebView2Async(env);var c=view.CoreWebView2;c.SetVirtualHostNameToFolderMapping("crown.local",Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"web"),CoreWebView2HostResourceAccessKind.DenyCors);c.Settings.AreDefaultContextMenusEnabled=false;c.Settings.AreDevToolsEnabled=false;c.Settings.IsStatusBarEnabled=false;c.Settings.IsGeneralAutofillEnabled=false;c.Settings.IsPasswordAutosaveEnabled=false;c.NavigationStarting+=(a,b)=>{if(!b.Uri.StartsWith("https://crown.local/",StringComparison.Ordinal))b.Cancel=true;};c.NewWindowRequested+=(a,b)=>b.Handled=true;c.PermissionRequested+=(a,b)=>b.State=CoreWebView2PermissionState.Deny;c.WebMessageReceived+=Message;view.Source=new Uri("https://crown.local/index.html");}catch(Exception ex){MessageBox.Show(this,ex.Message,"Could not start Crown Workshop");allowClose=true;Close();}};
        FormClosing+=(s,e)=>{if(allowClose||view.CoreWebView2==null)return;e.Cancel=true;if(!closePending){closePending=true;view.CoreWebView2.PostWebMessageAsJson("{\"event\":\"closeRequested\"}");}};
    }
    bool Discard(){return !session.Dirty||MessageBox.Show(this,"Discard unsaved edits?","Crown Workshop",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes;}
    string ChooseFile(){using(var d=new OpenFileDialog{Filter="Retro saves and backups|*.sav;*.bak|All files|*.*",Title="Open Retro save"})return d.ShowDialog(this)==DialogResult.OK?d.FileName:null;}
    string Find(){string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),@"AppData\LocalLow\Black Tower Basement\Defender of the Crown Collection");if(!Directory.Exists(root))return null;var files=Directory.GetFiles(root,"ClassicSave.sav",SearchOption.AllDirectories).Where(p=>Path.GetFileName(Path.GetDirectoryName(p))=="Classic").ToArray();return files.Length==1?files[0]:null;}
    void Message(object sender,CoreWebView2WebMessageReceivedEventArgs e){
        if(!e.Source.StartsWith("https://crown.local/",StringComparison.Ordinal))return;
        object rid=null;
        try{var m=json.Deserialize<Dictionary<string,object>>(e.WebMessageAsJson);rid=m["id"];string action=(string)m["action"];object result=null;
            switch(action){
                case "init":if(session.Save==null){string p=Find();if(p!=null)session.Load(p);}result=session.State();break;
                case "find":{
                    string p=Find();
                    if(p!=null&&string.Equals(Path.GetFullPath(p),session.FilePath,StringComparison.OrdinalIgnoreCase)){
                        result=new {message="Found your Retro save. It is already open; your edits have been kept.\n"+p,state=session.State()};break;
                    }
                    if(p==null)MessageBox.Show(this,"Could not identify a single Retro save automatically. Choose the save you want to open.","Find my save",MessageBoxButtons.OK,MessageBoxIcon.Information);
                    if(p==null)p=ChooseFile();
                    if(p==null||!Discard()){result=new {cancelled=true,state=session.State()};break;}
                    session.Load(p);result=new {message="Found and opened your Retro save.\n"+p,state=session.State()};break;
                }
                case "open":{
                    string p=ChooseFile();if(p==null||!Discard()){result=new {cancelled=true,state=session.State()};break;}
                    session.Load(p);result=new {message="Opened "+p,state=session.State()};break;
                }
                case "set":session.Set(Convert.ToInt32(m["field"]),(string)m["value"],Convert.ToBoolean(m["advanced"]));result=session.State();break;
                case "undo":session.Undo();result=session.State();break;
                case "revert":session.Revert();result=session.State();break;
                case "save":case "export":{
                    if(session.Save==null)throw new Exception("Open a save first.");string target=session.FilePath;bool export=action=="export";
                    if(export){using(var d=new SaveFileDialog{Filter="Retro save|*.sav",FileName="ClassicSave.edited.sav",InitialDirectory=Path.GetDirectoryName(target),OverwritePrompt=true}){if(d.ShowDialog(this)!=DialogResult.OK){result=new {cancelled=true,state=session.State()};break;}target=d.FileName;}}
                    if(!export&&!session.Dirty){result=new {message="No changes to save.",state=session.State()};break;}
                    var edits=session.Save.Fields.Where(f=>f.Changed).ToList();string summary=string.Join("\n",edits.Take(10).Select(f=>f.Name+": "+f.Original+" → "+f.Pending));if(edits.Count>10)summary+="\n… and "+(edits.Count-10)+" more.";
                    if(MessageBox.Show(this,(export?"Export":"Save")+" "+edits.Count+" changes?\n\n"+summary+"\n\n"+target+"\n\nAn existing file will be backed up first.","Review changes",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK){result=new {cancelled=true,state=session.State()};break;}
                    byte[] data=session.Save.Render();string backup=Save.WriteFile(target,data,string.Equals(Path.GetFullPath(target),session.FilePath,StringComparison.OrdinalIgnoreCase)?session.Save.Bytes:null);if(!export)session.Load(target);result=new {message=export?"Edited copy exported.":"Saved. A backup of the previous file was created.",backup=backup,state=session.State()};break;}
                case "backups":if(session.FilePath==null)throw new Exception("Open a save first.");string dir=Path.Combine(Path.GetDirectoryName(session.FilePath),"CrownWorkshopBackups");if(Directory.Exists(dir))System.Diagnostics.Process.Start("explorer.exe","\""+dir+"\"");else throw new Exception("No backups yet. Saving creates one beside the save file.");result=session.State();break;
                case "close":closePending=false;if(Discard()){allowClose=true;Close();return;}result=session.State();break;
                case "cancelClose":closePending=false;result=session.State();break;
                default:throw new Exception("Unknown action.");
            }
            view.CoreWebView2.PostWebMessageAsJson(json.Serialize(new {id=rid,ok=true,data=result}));
        }catch(Exception ex){view.CoreWebView2.PostWebMessageAsJson(json.Serialize(new {id=rid,ok=false,error=ex.Message}));}
    }
    [STAThread]public static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Desktop());}
}
}
