using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using PaoCaixa;
class RemotePolicyTests {
 static int count;static void Assert(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);count++;Console.WriteLine("PASS "+name);}
 static void Denied(Action action,string name){bool denied=false;try{action();}catch{denied=true;}Assert(denied,name);}
 static RemoteControl.SavedState State(RemoteControl remote){return (RemoteControl.SavedState)typeof(RemoteControl).GetField("state",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(remote);}
 static void Main(string[] args){string dir=args[0];Directory.CreateDirectory(dir);string config=Path.Combine(dir,"config.json");File.WriteAllText(config,"{\"url\":\"https://example.invalid/\"}");
  var remote=new RemoteControl(dir,config);var state=State(remote);string id=remote.InstallationId;
  Assert(remote.Enabled&&!remote.Denied,"new installation has existing three-day offline tolerance");
  state.lastAllowedUtc=DateTime.UtcNow.AddDays(-6);state.firstSeenUtc=DateTime.UtcNow.AddMonths(-1);Assert(!remote.Denied,"valid online approval allows seven-day offline tolerance");
  state.lastAllowedUtc=DateTime.UtcNow.AddDays(-8);Assert(remote.Denied,"expired offline approval suspends commercial access");
  state.lastAllowedUtc=DateTime.UtcNow;state.blocked=true;state.reason="Suspenso no teste";Assert(remote.Denied&&remote.DenialReason==state.reason,"explicit block overrides fresh approval");
  File.WriteAllText(Path.Combine(dir,"remote-control-state.json"),J.Write(state));var reopened=new RemoteControl(dir,config);Assert(reopened.InstallationId==id&&reopened.Denied,"cached explicit block and UUID survive reopen even without Windows registry");
  Denied(()=>remote.AssertAllowed(),"offline failure cannot automatically unlock blocked installation");
  state.blocked=false;Assert(!remote.Denied,"valid server unlock restores access without restart");
  using(var db=new Store(Path.Combine(dir,"db"),remote))using(var sql=new Sqlite(db.FilePath)){
   string salt=Passwords.Salt();sql.Exec("UPDATE users SET salt=?,password_hash=? WHERE is_owner=1",salt,Passwords.Hash("Teste-Pdv-2026",salt));db.Login(Store.OwnerEmail,"Teste-Pdv-2026");
   var draft=new Draft{cart=new List<Line>{new Line{productId="test",quantity=1000}},note="Edited during remote request"};state.blocked=true;
   Denied(()=>db.SetDraft(draft),"blocked installation cannot use normal commercial commands");db.PreserveDraftOnRemoteLock(draft);
   Assert(J.Read<Draft>((string)sql.Scalar("SELECT payload FROM user_drafts WHERE user_id=?",Store.OwnerId)).note==draft.note,"last private draft preserved before remote lock screen replaces sale");
   Denied(()=>db.Products(),"saving draft for preservation does not unlock commercial data");
   db.Logout();Denied(()=>db.PreserveDraftOnRemoteLock(draft),"preservation requires the existing authenticated session");
  }
  Assert(new RemoteControl(Path.Combine(dir,"disabled"),Path.Combine(dir,"absent.json")).Enabled==false,"missing remote config keeps existing disabled-mode behaviour");
  Console.WriteLine("PASS "+count+" remote policy checks");
 }
}
