using System;
using System.IO;
using System.Text;
using System.Threading;
namespace PaoCaixa {
 internal sealed class StartupResult {public bool Installing;public string Message;}
 internal sealed class StartupAttempt {public string version,sha512;}
 internal sealed class StartupUpdate {
  readonly Updates updates;readonly string channel,directory;readonly Func<string> blocked;readonly Action prepare;readonly Action<string> launch;
  internal Action<CancellationToken> Check;internal Action<Action<int>,CancellationToken> Download;
  internal int CheckTimeout=8000,DownloadTimeout=180000;
  string AttemptPath{get{return Path.Combine(directory,"updates-native","startup-attempt.json");}}
  internal StartupUpdate(Updates updater,string url,string dataDirectory,Func<string> blockReason,Action prepareInstall,Action<string> startInstaller){
   updates=updater;channel=url;directory=dataDirectory;blocked=blockReason;prepare=prepareInstall;launch=startInstaller;
   Check=token=>updates.Check(channel,token);Download=(progress,token)=>updates.Download(channel,directory,progress,token);
  }
  StartupResult Done(string message,bool installing=false){updates.Status=message;return new StartupResult{Message=message,Installing=installing};}
  StartupAttempt ReadAttempt(){if(!File.Exists(AttemptPath))return null;return J.Read<StartupAttempt>(File.ReadAllText(AttemptPath));}
  void SaveAttempt(Feed feed){Directory.CreateDirectory(Path.GetDirectoryName(AttemptPath));string tmp=AttemptPath+".tmp";File.WriteAllText(tmp,J.Write(new StartupAttempt{version=feed.version,sha512=feed.sha512}),Encoding.UTF8);if(File.Exists(AttemptPath))File.Replace(tmp,AttemptPath,null);else File.Move(tmp,AttemptPath);}
  internal StartupResult Run(Action<string,int> progress,CancellationToken token){
   try{
    token.ThrowIfCancellationRequested();var attempt=ReadAttempt();System.Version previous;
    if(attempt!=null&&System.Version.TryParse(attempt.version,out previous)&&previous<=new System.Version(Updates.Version)){File.Delete(AttemptPath);attempt=null;}
    string reason=blocked();if(reason!=null)return Done("Atualização automática adiada. "+reason+" Seus dados foram mantidos.");
    progress("Verificando atualizações…",-1);
    using(var check=CancellationTokenSource.CreateLinkedTokenSource(token)){check.CancelAfter(CheckTimeout);Check(check.Token);}
    token.ThrowIfCancellationRequested();var feed=updates.Available;if(feed==null)return Done(updates.Status);
    if(new System.Version(feed.version)<=new System.Version(Updates.Version))return Done("Você está usando a versão mais recente.");
    if(attempt!=null&&attempt.version==feed.version&&attempt.sha512==feed.sha512)return Done("A instalação anterior não foi concluída. Entre e use Configurações para tentar novamente.");
    progress("Baixando versão "+feed.version+"…",0);
    using(var download=CancellationTokenSource.CreateLinkedTokenSource(token)){download.CancelAfter(DownloadTimeout);Download(percent=>progress("Baixando versão "+feed.version+": "+percent+"%",percent),download.Token);}
    token.ThrowIfCancellationRequested();Updates.Verify(updates.Downloaded,feed);progress("Protegendo os dados antes de atualizar…",100);
    prepare(); // Rechecks every register and draft, then makes a consistent backup.
    token.ThrowIfCancellationRequested();Updates.Verify(updates.Downloaded,feed);progress("Instalando a atualização. O programa vai reabrir…",100);
    SaveAttempt(feed); // If installation fails, reopening the old app must not create a restart loop.
    try{launch(updates.Downloaded);}catch{File.Delete(AttemptPath);throw;}
    return Done("Instalando a atualização…",true);
   }catch(OperationCanceledException){return Done(token.IsCancellationRequested?"Atualização interrompida. Entre para continuar conforme a autorização da instalação.":"A atualização demorou além do limite. Entre para continuar conforme a autorização da instalação.");}
   catch(Exception ex){return Done("Atualização automática não concluída: "+Updates.Describe(ex)+" Entre para continuar conforme a autorização da instalação. Os dados foram preservados.");}
  }
  internal static string InstallerArguments(string installDirectory){return "/S --updated /D="+installDirectory.TrimEnd(new char[]{Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar});}
 }
}
