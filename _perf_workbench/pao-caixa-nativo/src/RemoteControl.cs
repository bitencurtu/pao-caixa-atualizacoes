using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Win32;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace PaoCaixa {
 // O servidor e a unica autoridade para bloquear/desbloquear. A chave do painel
 // administrativo nunca e distribuida ao aplicativo.
 public sealed class RemoteControl {
  public sealed class Configuration {public string url;}
  public sealed class SavedState {public string id,reason;public bool blocked;public DateTime firstSeenUtc,lastAllowedUtc;}
  public sealed class ServerState {public string id,status,reason;}
  readonly object sync=new object();
  readonly string statePath;
  readonly string address;
  SavedState state;
  DateTime persistedAllowedUtc;
  int checking;
  volatile string lastCheckStatus="Aguardando a primeira consulta à Cloudflare.";
  public string LastCheckStatus { get { return lastCheckStatus; } }
  public string InstallationId {get {lock(sync)return state.id;}}
  public string PanelUrl {get{return address;}}
  public bool Enabled {get{return address!=null;}}

  public RemoteControl(string directory):this(directory,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"remote-control.json")){}
  internal RemoteControl(string directory,string configurationPath){
   Directory.CreateDirectory(directory);
   statePath=Path.Combine(directory,"remote-control-state.json");
   string config=configurationPath;
   try{var c=J.Read<Configuration>(File.ReadAllText(config));
    if(!string.IsNullOrWhiteSpace(c.url)){
     Uri u;if(!Uri.TryCreate(c.url.Trim(),UriKind.Absolute,out u)||u.Scheme!="https"||u.UserInfo!=""||u.Query!=""||u.Fragment!="")throw new Exception("Servidor de controle remoto inválido.");
     address=u.AbsoluteUri.TrimEnd(new char[] {'/'})+"/";
    }
   }catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
   try{state=J.Read<SavedState>(File.ReadAllText(statePath));}catch{state=null;}
   Guid id;
   // ID derivado do Windows e do usuario local, para que apagar o cache
   // nao produza um novo computador no servidor de bloqueio.
   string stableId=StableInstallationId();
   if(stableId==null)stableId=state!=null&&Guid.TryParse(state.id,out id)?state.id:Guid.NewGuid().ToString("D");
   if(state==null||!Guid.TryParse(state.id,out id)||state.firstSeenUtc==default(DateTime)||state.id!=stableId){
    state=new SavedState{id=stableId,firstSeenUtc=DateTime.UtcNow,reason="",blocked=false};
    Save(state);
   }
   persistedAllowedUtc=state.lastAllowedUtc;
  }
  static string StableInstallationId(){
   try{
    string machine=null;
    using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,Environment.Is64BitOperatingSystem?RegistryView.Registry64:RegistryView.Registry32))
    using(var key=root.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
     if(key!=null)machine=Convert.ToString(key.GetValue("MachineGuid"));
    var user=WindowsIdentity.GetCurrent();
    var sid=user==null||user.User==null?null:user.User.Value;
    if(!string.IsNullOrWhiteSpace(machine)&&!string.IsNullOrWhiteSpace(sid)){
     using(var hash=SHA256.Create()){
      byte[] digest=hash.ComputeHash(Encoding.UTF8.GetBytes("PaoCaixa-Remote-v1|"+machine+"|"+sid));
      byte[] first=new byte[16];Array.Copy(digest,first,16);
      first[7]=(byte)((first[7]&0x0f)|0x50);
      first[8]=(byte)((first[8]&0x3f)|0x80);
      return new Guid(first).ToString("D");
     }
    }
   }catch{}
   // Maquinas sem MachineGuid: o fallback funciona, mas pode mudar se
   // a pasta de dados for apagada. Nao e um DRM a prova de adulteracao.
   return null; // Reuse a valid cached ID when Windows cannot supply MachineGuid/SID.
  }
  void Save(SavedState value){
   string tmp=statePath+".tmp";
   File.WriteAllText(tmp,J.Write(value));
   if(File.Exists(statePath))File.Replace(tmp,statePath,null);else File.Move(tmp,statePath);
  }
  public bool Denied {get{lock(sync){if(!Enabled)return false;
   // Em Windows configurado para pt-BR, o JavaScriptSerializer pode
   // desserializar DateTime.MinValue como ano 0001 com um deslocamento
   // de fuso horario. Comparar apenas com default(DateTime) causava
   // suspensao imediata na primeira abertura, mesmo sem bloqueio remoto.
   bool validOnlineApproval=state.lastAllowedUtc.Year>=2000;
   DateTime expires=(validOnlineApproval?state.lastAllowedUtc.AddDays(7):state.firstSeenUtc.AddDays(3));
   return state.blocked||DateTime.UtcNow>expires;
  }}}
  public string DenialReason {get{lock(sync){if(state.blocked)return string.IsNullOrWhiteSpace(state.reason)?"Instalação suspensa pelo responsável.":state.reason;
   return "Sem validação online dentro do prazo. Conecte este computador à internet e tente novamente.";
  }}}
  public const string AccessBlockedMessage="Acesso bloqueado. Entre em contato com o responsável pelo sistema.";
  public void AssertAllowed(){if(Denied)throw new Exception(AccessBlockedMessage);}

  // Nunca chamar da thread de interface. Em falhas de rede, mantem a ultima decisao.
  void Diagnostic(string detail){
   lastCheckStatus=detail;
   try{
    string log=Path.Combine(Path.GetDirectoryName(statePath),"remote-control-diagnostico.txt");
    File.WriteAllText(log,"Data UTC: "+DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")+"\r\n"+
      "Servidor: "+address+"\r\n"+detail+"\r\n",Encoding.UTF8);
   }catch{} // Problemas ao gravar log nao impedem consulta remota.
  }
  public void Check(){
   if(!Enabled){Diagnostic("Controle remoto desativado: remote-control.json nao tem URL.");return;}
   if(Interlocked.Exchange(ref checking,1)==1)return;
   try{
    Diagnostic("Consultando Cloudflare...");
    ServicePointManager.SecurityProtocol=(SecurityProtocolType)3072; // TLS 1.2, .NET Framework
    string id=InstallationId;
    string payload=J.Write(new {id=id,computer=Environment.MachineName,version=Updates.Version});
    byte[] bytes=Encoding.UTF8.GetBytes(payload);
    var request=(HttpWebRequest)WebRequest.Create(address+"api/device");
    request.Method="POST";request.ContentType="application/json; charset=utf-8";
    request.ContentLength=bytes.Length;request.Timeout=6000;request.ReadWriteTimeout=6000;
    request.AllowAutoRedirect=false; // Usa as configuracoes normais de proxy do Windows
    using(var output=request.GetRequestStream())output.Write(bytes,0,bytes.Length);
    using(var response=(HttpWebResponse)request.GetResponse()){
     if(response.StatusCode!=HttpStatusCode.OK)throw new IOException("Servidor indisponível.");
     using(var input=response.GetResponseStream())using(var mem=new MemoryStream()){
      byte[] b=new byte[2048];int n;
      while((n=input.Read(b,0,b.Length))>0){if(mem.Length+n>8192)throw new IOException("Resposta excessiva.");mem.Write(b,0,n);}
      var received=J.Read<ServerState>(Encoding.UTF8.GetString(mem.ToArray()));
      if(received==null||received.id!=id||(received.status!="allowed"&&received.status!="blocked"))throw new IOException("Resposta de controle inválida.");
      lock(sync){
       bool oldBlocked=state.blocked;
       string oldReason=state.reason??"";
       state.blocked=received.status=="blocked";
       state.reason=(received.reason??"").Length>200?received.reason.Substring(0,200):received.reason??"";
       if(!state.blocked)state.lastAllowedUtc=DateTime.UtcNow;
       // Evita gravar em disco a cada 15 segundos: salva imediatamente ao
       // bloquear/desbloquear e renova a ultima autorizacao em ate 5 minutos.
       bool shouldPersist=oldBlocked!=state.blocked||oldReason!=state.reason||
        (!state.blocked&&(persistedAllowedUtc.Year<2000||
        DateTime.UtcNow-persistedAllowedUtc>TimeSpan.FromMinutes(5)));
       if(shouldPersist){try{Save(state);persistedAllowedUtc=state.lastAllowedUtc;}catch{}}

      }
      Diagnostic(received.status=="blocked"?"Conectado ao servidor: bloqueio remoto recebido.":
       "Conectado ao servidor: instalacao liberada e registrada na Cloudflare.");
     }
    }
   }catch(WebException ex){
    var reply=ex.Response as HttpWebResponse;
    Diagnostic("Falha de rede: "+ex.Status+(reply!=null?" (HTTP "+((int)reply.StatusCode)+")":"")+
     (ex.InnerException!=null?". "+ex.InnerException.Message:""));
   }catch(IOException ex){Diagnostic("Falha ao ler resposta: "+ex.Message);}
   catch(Exception ex){Diagnostic("Falha de validacao: "+ex.GetType().Name+" - "+ex.Message);}
   finally{Interlocked.Exchange(ref checking,0);}
  }
 }
}
