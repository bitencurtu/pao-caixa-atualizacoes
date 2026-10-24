using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
namespace PaoCaixa {
 public class Feed {public string version,path,sha512;public long size;}
 public sealed class Updates {
  public const string Version="2.0.7";
  public string Status="Pronto para procurar atualizações.",Downloaded,LastError;
  public Feed Available;public bool CheckSucceeded;
  public static Feed Parse(string yaml){
   if(yaml==null||yaml.Length>65536)throw new InvalidDataException("Manifesto de atualização vazio ou muito grande.");
   yaml=yaml.TrimStart(new char[]{'\uFEFF'}).Replace("\r\n","\n").Replace('\r','\n');
   Func<string,string> field=k=>{var matches=Regex.Matches(yaml,"(?m)^"+k+@":[ \t]*([^\n]+)$");if(matches.Count!=1)throw new InvalidDataException("Manifesto inválido: campo "+k+" ausente ou repetido.");return matches[0].Groups[1].Value.Trim().Trim(new char[]{'"','\''});};
   var f=new Feed{version=field("version"),path=field("path"),sha512=field("sha512")};
   var size=Regex.Matches(yaml,@"(?m)^[ \t]+size:[ \t]*(\d+)[ \t]*$");
   if(size.Count!=1||!long.TryParse(size[0].Groups[1].Value,out f.size))throw new InvalidDataException("Manifesto inválido: tamanho ausente ou repetido.");
   System.Version version;if(!System.Version.TryParse(f.version,out version)||!Regex.IsMatch(f.path??"", @"^PaoCaixa-Instalador-[0-9.]+-(x86|x64)\.exe$")||f.size<1024||f.size>250*1024*1024)throw new InvalidDataException("Manifesto inválido: confira versão, nome e tamanho do instalador.");
   byte[] hash;try{hash=Convert.FromBase64String(f.sha512);}catch{throw new InvalidDataException("Manifesto inválido: SHA-512 incorreto.");}if(hash.Length!=64)throw new InvalidDataException("Manifesto inválido: SHA-512 precisa ter 64 bytes.");
   if(!f.path.StartsWith("PaoCaixa-Instalador-"+f.version+"-",StringComparison.Ordinal))throw new InvalidDataException("Manifesto inválido: versão e instalador não correspondem.");return f;
  }
  sealed class Response:IDisposable {
   public HttpWebResponse Http;public CancellationTokenRegistration Cancellation;
   public void Dispose(){if(Http!=null)Http.Close();Cancellation.Dispose();}
  }
  static Response Open(Uri uri,CancellationToken token){
   ServicePointManager.SecurityProtocol=(SecurityProtocolType)3072;
   for(int redirects=0;redirects<8;redirects++){
    token.ThrowIfCancellationRequested();if(uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo))throw new InvalidDataException("O canal e seus redirecionamentos precisam usar HTTPS sem credenciais.");
    var request=(HttpWebRequest)WebRequest.Create(uri);request.AllowAutoRedirect=false;request.Timeout=15000;request.ReadWriteTimeout=30000;request.UserAgent="PaoCaixa/"+Version;request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
    var result=new Response{Cancellation=token.Register(()=>request.Abort())};
    try{result.Http=(HttpWebResponse)request.GetResponse();token.ThrowIfCancellationRequested();int code=(int)result.Http.StatusCode;
     if(code>=300&&code<400){string location=result.Http.Headers["Location"];if(string.IsNullOrEmpty(location))throw new InvalidDataException("O servidor redirecionou sem informar um destino.");uri=new Uri(uri,location);result.Dispose();continue;}
     if(code!=200)throw new IOException("Servidor respondeu HTTP "+code+".");return result;
    }catch{result.Dispose();token.ThrowIfCancellationRequested();throw;}
   }throw new InvalidDataException("O canal redirecionou mais de oito vezes.");
  }
  public static string Describe(Exception ex){
   var web=ex as WebException;if(web!=null){var reply=web.Response as HttpWebResponse;int code=reply==null?0:(int)reply.StatusCode;
    if(code==404)return "Canal não encontrado (HTTP 404). Confira o latest.yml da release marcada como Latest.";
    if(code!=0)return "Servidor de atualização respondeu HTTP "+code+" ("+web.Status+").";
    if(web.Status==WebExceptionStatus.TrustFailure||web.Status==WebExceptionStatus.SecureChannelFailure)return "Falha de TLS/certificado ("+web.Status+"). Confira data do Windows, certificados e suporte a TLS 1.2.";
    if(web.Status==WebExceptionStatus.NameResolutionFailure||web.Status==WebExceptionStatus.ProxyNameResolutionFailure)return "Não foi possível localizar o canal ("+web.Status+"). Confira internet, DNS e proxy.";
    if(web.Status==WebExceptionStatus.Timeout)return "Tempo de conexão esgotado. Tente novamente quando a internet estiver disponível.";
    return "Falha de conexão com o canal ("+web.Status+").";
   }
   if(ex is InvalidDataException)return ex.Message;
   if(ex is MissingMethodException)return "Compatibilidade .NET: "+ex.Message;
   if(ex is OperationCanceledException)return "Consulta interrompida ou tempo limite excedido.";
   return ex.GetType().Name+": "+ex.Message;
  }
  public void Check(string channel){Check(channel,CancellationToken.None);}
  public void Check(string channel,CancellationToken token){
   Status="Procurando atualização…";Available=null;Downloaded=null;LastError=null;CheckSucceeded=false;
   try{token.ThrowIfCancellationRequested();
    using(var r=Open(new Uri(Preferences.ValidateUrl(channel)+"latest.yml"),token))using(var input=r.Http.GetResponseStream())using(var mem=new MemoryStream()){
     var buffer=new byte[4096];int n;while((n=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();if(mem.Length+n>65536)throw new InvalidDataException("Manifesto maior que 64 KB.");mem.Write(buffer,0,n);}token.ThrowIfCancellationRequested();var feed=Parse(Encoding.UTF8.GetString(mem.ToArray()));
     CheckSucceeded=true;if(new System.Version(feed.version)<=new System.Version(Version))Status="Atualizado: versão instalada "+Version+"; versão do canal "+feed.version+".";
     else{Available=feed;Status="Nova versão "+feed.version+" disponível. Versão instalada: "+Version+".";}
    }
   }catch(Exception ex){Available=null;LastError=Describe(ex);Status=LastError+" O caixa continua offline conforme a autorização da instalação.";token.ThrowIfCancellationRequested();}
  }
  public void Download(string channel,string dir,Action<int> progress){Download(channel,dir,progress,CancellationToken.None);}
  public void Download(string channel,string dir,Action<int> progress,CancellationToken token){
   token.ThrowIfCancellationRequested();if(Available==null)throw new InvalidOperationException("Procure uma nova versão antes de baixar.");var feed=Available;string folder=Path.Combine(dir,"updates-native");Directory.CreateDirectory(folder);string path=Path.Combine(folder,feed.path),tmp=path+".part";Downloaded=null;Status="Baixando atualização…";LastError=null;
   try{
    if(File.Exists(path)){bool valid=false;try{Verify(path,feed);valid=true;}catch{}token.ThrowIfCancellationRequested();if(valid){Downloaded=path;Status="Atualização verificada e pronta para instalar.";if(progress!=null)progress(100);return;}}
    using(var r=Open(new Uri(Preferences.ValidateUrl(channel)+feed.path),token))using(var input=r.Http.GetResponseStream())using(var output=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None)){
     byte[] b=new byte[65536];int n,last=-1;long size=0;while((n=input.Read(b,0,b.Length))>0){token.ThrowIfCancellationRequested();size+=n;if(size>feed.size)throw new InvalidDataException("Download maior que o tamanho publicado.");output.Write(b,0,n);int percent=(int)(size*100/feed.size);if(percent!=last){last=percent;if(progress!=null)progress(percent);}}if(size!=feed.size)throw new InvalidDataException("Download incompleto.");
    }
    token.ThrowIfCancellationRequested();Verify(tmp,feed);token.ThrowIfCancellationRequested();if(File.Exists(path))File.Delete(path);File.Move(tmp,path);Downloaded=path;Status="Atualização pronta. Feche o caixa e conclua os pedidos antes de instalar.";
   }catch(Exception ex){if(File.Exists(tmp))File.Delete(tmp);LastError=Describe(ex);Status="Download não concluído: "+LastError+" Os dados foram preservados.";token.ThrowIfCancellationRequested();throw;}
  }
  public static void Verify(string path,Feed feed){using(var input=File.OpenRead(path)){
   if(input.Length!=feed.size||input.ReadByte()!=77||input.ReadByte()!=90)throw new InvalidDataException("Instalador inválido: tamanho ou cabeçalho incorreto.");input.Position=0;
   using(var hash=SHA512.Create()){var actual=hash.ComputeHash(input);var expected=Convert.FromBase64String(feed.sha512);if(expected.Length!=64)throw new InvalidDataException("SHA-512 inválido.");int diff=0;for(int i=0;i<64;i++)diff|=actual[i]^expected[i];if(diff!=0)throw new InvalidDataException("O instalador não corresponde ao SHA-512 publicado.");}
  }}
 }
}
