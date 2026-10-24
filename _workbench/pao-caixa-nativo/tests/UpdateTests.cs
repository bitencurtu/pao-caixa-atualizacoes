using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using PaoCaixa;
class UpdateTests {
 static int count;static string root;
 static void Assert(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);count++;Console.WriteLine("PASS "+name);}
 static void Denied(Action work,string name){bool denied=false;try{work();}catch{denied=true;}Assert(denied,name);}
 static string Dir(string name){string path=Path.Combine(root,name);Directory.CreateDirectory(path);return path;}
 static byte[] Installer(){var bytes=new byte[2048];bytes[0]=77;bytes[1]=90;bytes[2047]=71;return bytes;}
 static Feed Release(string version=null){if(version==null){var current=new Version(Updates.Version);version=current.Major+"."+current.Minor+"."+(current.Build+1);}using(var hash=SHA512.Create())return new Feed{version=version,path="PaoCaixa-Instalador-"+version+"-x86.exe",size=2048,sha512=Convert.ToBase64String(hash.ComputeHash(Installer()))};}
 static string Yaml(Feed feed){return "version: "+feed.version+"\nfiles:\n  - url: "+feed.path+"\n    sha512: "+feed.sha512+"\n    size: "+feed.size+"\npath: "+feed.path+"\nsha512: "+feed.sha512+"\n";}
 static StartupUpdate Job(string name,Updates updater,Action prepare,Action<string> launch,Func<string> blocked=null){
  var job=new StartupUpdate(updater,Preferences.DefaultChannel,Dir(name),blocked??(()=>null),prepare,launch);
  job.Check=token=>{token.ThrowIfCancellationRequested();updater.Available=Release();};
  job.Download=(progress,token)=>{token.ThrowIfCancellationRequested();updater.Downloaded=Path.Combine(Dir(name),"verified.exe");File.WriteAllBytes(updater.Downloaded,Installer());progress(100);};return job;
 }
 static void Main(string[] args){root=args[0];Directory.CreateDirectory(root);string fixture=File.ReadAllText(args[1]);
  Denied(()=>PaoCaixaLegacy.Updates.Parse(fixture),"published 2.0.3 CRLF manifest reproduces actual old parser failure");
  Assert(PaoCaixaLegacy.Updates.Parse(fixture.Replace("\r\n","\n")).version=="2.0.3","same published manifest works in old app after LF normalization");
  Assert(Updates.Parse(fixture).version=="2.0.3","new parser accepts actual published CRLF manifest");
  string yaml=Yaml(Release("2.0.4"));Assert(PaoCaixaLegacy.Updates.Parse(yaml).version=="2.0.4","2.0.3 app can parse next release generated with LF");
  Assert(Updates.Parse(yaml).version=="2.0.4"&&Updates.Parse("\uFEFF"+yaml.Replace("\n","\r\n")).version=="2.0.4","new parser accepts LF CRLF and UTF-8 BOM");
  Denied(()=>Updates.Parse(yaml+"version: 9.9.9\n"),"duplicate version rejected");Denied(()=>Updates.Parse(yaml.Replace("PaoCaixa-Instalador-2.0.4-x86.exe","../../evil.exe")),"unsafe package path rejected");
  Denied(()=>Updates.Parse(yaml.Replace("path: PaoCaixa-Instalador-2.0.4","path: PaoCaixa-Instalador-2.0.5")),"version/package mismatch rejected");
  Assert(Updates.Describe(new InvalidDataException("Manifesto inválido")).Contains("Manifesto"),"manifest errors distinguished from connection errors");
  Assert(Updates.Describe(new WebException("tls",null,WebExceptionStatus.SecureChannelFailure,null)).Contains("TLS"),"TLS failures explained");
  Assert(Updates.Describe(new WebException("dns",null,WebExceptionStatus.NameResolutionFailure,null)).Contains("DNS"),"DNS failures explained");
  var preferences=new Preferences{url="https://example.com/canal/",theme="dark"};preferences.Save(Dir("prefs"));Assert(Preferences.Load(Dir("prefs")).url==preferences.url,"channel saved and reloaded");
  File.WriteAllText(Path.Combine(Dir("prefs"),"update-settings.json"),"{\"url\":\"https://example.com/antigo/\"}");Assert(Preferences.Load(Dir("prefs")).url==preferences.url,"old legacy settings cannot override current channel");
  File.WriteAllText(Path.Combine(Dir("legacy-prefs"),"update-settings.json"),"{\"url\":\"https://example.com/antigo/\"}");Assert(Preferences.Load(Dir("legacy-prefs")).url=="https://example.com/antigo/","old custom channel still migrates when current settings absent");
  Denied(()=>Preferences.ValidateUrl("http://example.com/"),"HTTP channel refused");Denied(()=>Preferences.ValidateUrl("https://user:pass@example.com/"),"credential-bearing channel refused");
  Action<string,int> progress=(text,p)=>{};bool prepared=false,installed=false;var updater=new Updates();var job=Job("success",updater,()=>prepared=true,path=>{Assert(prepared,"backup precedes installer handoff");Updates.Verify(path,updater.Available);installed=true;});var result=job.Run(progress,CancellationToken.None);Assert(result.Installing&&installed,"new release automatically verified and installed");
  prepared=false;installed=false;job=Job("success",new Updates(),()=>prepared=true,path=>installed=true);result=job.Run(progress,CancellationToken.None);Assert(!result.Installing&&!prepared&&!installed&&result.Message.Contains("anterior"),"failed handoff cannot create restart loop");
  job=Job("offline",new Updates(),()=>prepared=true,path=>installed=true);job.Check=token=>{throw new WebException("offline");};prepared=false;installed=false;result=job.Run(progress,CancellationToken.None);Assert(!result.Installing&&!prepared&&!installed,"offline permits normal login subject to licence");
  job=Job("current",new Updates(),()=>prepared=true,path=>installed=true);job.Check=token=>{};Assert(!job.Run(progress,CancellationToken.None).Installing,"no release opens current app");
  var old=new Updates();job=Job("downgrade",old,()=>prepared=true,path=>installed=true);job.Check=token=>old.Available=Release("2.0.3");prepared=false;installed=false;Assert(!job.Run(progress,CancellationToken.None).Installing&&!prepared&&!installed,"downgrade rejected");
  foreach(string reason in new[]{"Caixa aberto","Pedido pendente"}){bool checkedNetwork=false;job=Job("blocked"+count,new Updates(),()=>{},path=>{},()=>reason);job.Check=token=>checkedNetwork=true;result=job.Run(progress,CancellationToken.None);Assert(!result.Installing&&!checkedNetwork&&result.Message.Contains(reason),"busy data postpones update: "+reason);}
  job=Job("backup-error",new Updates(),()=>{throw new IOException("disk full");},path=>installed=true);installed=false;Assert(!job.Run(progress,CancellationToken.None).Installing&&!installed,"failed backup prevents installation");
  var bad=new Updates();job=Job("tampered",bad,()=>prepared=true,path=>installed=true);job.Download=(p,t)=>{bad.Downloaded=Path.Combine(Dir("tampered"),"bad.exe");var bytes=Installer();bytes[2047]^=1;File.WriteAllBytes(bad.Downloaded,bytes);};prepared=false;installed=false;Assert(!job.Run(progress,CancellationToken.None).Installing&&!prepared&&!installed,"tampered package rejected before backup and launch");
  job=Job("late-guard",new Updates(),()=>{throw new Exception("Cash state changed");},path=>installed=true);installed=false;Assert(!job.Run(progress,CancellationToken.None).Installing&&!installed,"final guard prevents state changes during download");
  var changed=new Updates();job=Job("changed",changed,()=>{using(var f=new FileStream(changed.Downloaded,FileMode.Open,FileAccess.Write)){f.Position=2047;f.WriteByte(12);}},path=>installed=true);installed=false;Assert(!job.Run(progress,CancellationToken.None).Installing&&!installed,"package reverified after backup");
  job=Job("launch-error",new Updates(),()=>{},path=>{throw new IOException("antivirus");});Assert(!job.Run(progress,CancellationToken.None).Installing&&!File.Exists(Path.Combine(Dir("launch-error"),"updates-native","startup-attempt.json")),"launch failure returns to login without stale attempt");
  using(var cancel=new CancellationTokenSource()){cancel.Cancel();job=Job("cancel",new Updates(),()=>prepared=true,path=>installed=true);prepared=false;installed=false;Assert(!job.Run(progress,cancel.Token).Installing&&!prepared&&!installed,"user cancellation stops safely");}
  job=Job("timeout",new Updates(),()=>{},path=>{});job.CheckTimeout=60;job.Check=token=>{token.WaitHandle.WaitOne();token.ThrowIfCancellationRequested();};var watch=System.Diagnostics.Stopwatch.StartNew();result=job.Run(progress,CancellationToken.None);Assert(!result.Installing&&result.Message.Contains("limite")&&watch.ElapsedMilliseconds<2000,"bounded check timeout releases app");
  job=Job("download-timeout",new Updates(),()=>prepared=true,path=>installed=true);job.DownloadTimeout=60;job.Download=(p,t)=>{t.WaitHandle.WaitOne();t.ThrowIfCancellationRequested();};prepared=false;installed=false;Assert(!job.Run(progress,CancellationToken.None).Installing&&!prepared&&!installed,"bounded download timeout avoids partial install");
  var cache=new Updates{Available=Release()};string dir=Dir("cache"),folder=Path.Combine(dir,"updates-native");Directory.CreateDirectory(folder);string exe=Path.Combine(folder,cache.Available.path);File.WriteAllBytes(exe,Installer());cache.Download("https://invalid.invalid/",dir,null,CancellationToken.None);Assert(cache.Downloaded==exe,"valid cached installer reused after reverification");
  using(var cancel=new CancellationTokenSource()){cancel.Cancel();Denied(()=>cache.Download(Preferences.DefaultChannel,dir,null,cancel.Token),"cancelled download avoids network");Denied(()=>cache.Check(Preferences.DefaultChannel,cancel.Token),"cancelled feed check avoids network");Assert(cache.Available==null&&cache.Downloaded==null,"stale package and feed cleared before checking");}
  Assert(StartupUpdate.InstallerArguments("C:\\Users\\Caixa\\Pao e Caixa")=="/S --updated /D=C:\\Users\\Caixa\\Pao e Caixa","silent installer arguments preserve path with spaces");
  using(var db=new Store(Dir("db")))using(var sql=new Sqlite(db.FilePath)){
   Assert(db.StartupUpdateBlockReason()==null,"startup maintenance allowed on empty register without login");Denied(()=>db.Products(),"maintenance does not log in or expose business access");
   string snapshot=J.Write(sql.Query("SELECT * FROM users"));Assert(File.Exists(db.PrepareStartupInstall())&&snapshot==J.Write(sql.Query("SELECT * FROM users")),"startup backup preserves accounts and hashes");
   sql.Exec("INSERT INTO sessions VALUES('open',0,?)",J.Write(new Register{id="open",status="open",userId=Store.OwnerId}));Denied(()=>db.PrepareStartupInstall(),"real SQLite open register prevents startup install");sql.Exec("DELETE FROM sessions");
   sql.Exec("INSERT INTO users(id,email,name,role,salt,password_hash,created_at) SELECT 'other','other@teste.local','Other','operator',salt,password_hash,created_at FROM users WHERE is_owner=1");sql.Exec("INSERT INTO user_drafts VALUES('other',?)",J.Write(new Draft{cart=new System.Collections.Generic.List<Line>{new Line{productId="test",quantity=1000}}}));Denied(()=>db.PrepareStartupInstall(),"private draft from any account prevents install");sql.Exec("DELETE FROM user_drafts WHERE user_id='other'");
   Denied(()=>db.PreserveDraftOnRemoteLock(new Draft()),"lock preservation cannot bypass normal authentication");
  }
  Console.WriteLine("PASS "+count+" update checks");
 }
}
