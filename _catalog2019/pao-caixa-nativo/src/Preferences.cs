using System;
using System.Collections.Generic;
using System.IO;
namespace PaoCaixa {
 public class Preferences {
  public string theme="light",url=DefaultChannel;
  public const string DefaultChannel="https://github.com/bitencurtu/pao-caixa-atualizacoes/releases/latest/download/";
  public static Preferences Load(string dir){
   var p=new Preferences();bool loaded=false;
   try{var saved=J.Read<Preferences>(File.ReadAllText(Path.Combine(dir,"native-preferences.json")));if(saved!=null){saved.url=ValidateUrl(saved.url);p=saved;loaded=true;}}catch{}
   if(!loaded){try{var old=J.Read<Dictionary<string,string>>(File.ReadAllText(Path.Combine(dir,"update-settings.json")));if(!string.IsNullOrWhiteSpace(old["url"]))p.url=ValidateUrl(old["url"]);}catch{}}
   if(p.theme!="dark")p.theme="light";if(string.IsNullOrEmpty(p.url))p.url=DefaultChannel;return p;
  }
  public static string ValidateUrl(string raw){Uri u;if(!Uri.TryCreate((raw??"").Trim(),UriKind.Absolute,out u)||u.Scheme!="https"||!string.IsNullOrEmpty(u.UserInfo)||!string.IsNullOrEmpty(u.Query)||!string.IsNullOrEmpty(u.Fragment))throw new Exception("Informe um endereço HTTPS sem senha, parâmetros ou fragmentos.");return u.AbsoluteUri.TrimEnd(new char[] { '/' })+"/";}
  static void Atomic(string path,string text){string tmp=path+".tmp";File.WriteAllText(tmp,text);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}
  public void Save(string dir){Atomic(Path.Combine(dir,"native-preferences.json"),J.Write(this));Atomic(Path.Combine(dir,"update-settings.json"),J.Write(new Dictionary<string,string>{{"url",url}}));}
 }
}
