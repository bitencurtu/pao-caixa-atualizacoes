using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using PaoCaixa;
class IntegrationTests {
 static void Main(string[] args){
  string text=File.ReadAllText(args[0]);var feed=Updates.Parse(text);Updates.Verify(args[1],feed);Console.WriteLine("PASS generated manifest and installer SHA-512");
  var old=PaoCaixaLegacy.Updates.Parse(text);if(new Version(old.version)<=new Version("2.0.3"))throw new Exception("2.0.3 cannot discover newer version");
  Updates.Verify(args[1],new Feed{version=old.version,path=old.path,size=old.size,sha512=old.sha512});Console.WriteLine("PASS unchanged 2.0.3 parser discovers and verifies next release");
  if(args.Length>2&&args[2]=="windows"){
   var updates=new Updates();for(int i=0;i<3;i++){updates.Check(Preferences.DefaultChannel);if(updates.CheckSucceeded)break;Thread.Sleep(1000);}if(!updates.CheckSucceeded)throw new Exception("Live GitHub channel: "+updates.Status);Console.WriteLine("PASS live HTTPS GitHub redirects and published manifest: "+updates.Status);
   string target=Path.Combine(Path.GetTempPath(),"PaoCaixa-CI-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(target);
   bool first;Process installer;
   using(var mutex=new Mutex(true,"Local\\PaoCaixaNative",out first)){
    if(!first)throw new Exception("Test app mutex is already in use");
    installer=Process.Start(new ProcessStartInfo(Path.GetFullPath(args[1]),"/S /NOLAUNCH /D="+target){UseShellExecute=false});
    Thread.Sleep(750);if(installer.HasExited)throw new Exception("Installer did not wait for the running app");
   }
   if(!installer.WaitForExit(60000)){installer.Kill();throw new Exception("Silent installation did not finish");}if(installer.ExitCode!=0)throw new Exception("Silent installer exit code "+installer.ExitCode);installer.Dispose();
   foreach(var name in new[]{"PaoCaixa.exe","PaoCaixa.exe.config","sqlite3.dll","remote-control.json","Uninstall PaoCaixa.exe"})if(!File.Exists(Path.Combine(target,name)))throw new Exception("Installed file missing: "+name);
   if(FileVersionInfo.GetVersionInfo(Path.Combine(target,"PaoCaixa.exe")).FileVersion!=Updates.Version+".0")throw new Exception("Installed file version mismatch");
   if(File.Exists(Path.Combine(target,"padaria.sqlite")))throw new Exception("Installer must not ship a customer database");
   Console.WriteLine("PASS real Windows silent installer, app mutex handoff, version, dependencies and data separation");
  }
 }
}
