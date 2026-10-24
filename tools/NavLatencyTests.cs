using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using PaoCaixa;
class BenchWindow : MainForm {
 public BenchWindow(string dir):base(dir){}
 protected override void OnShown(EventArgs e){} // Evitar inicializar o fluxo remoto em ambiente de teste.
}
class NavigationLatencyTests {
 static readonly BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static object Get(MainForm c,string name){return typeof(MainForm).GetField(name,F).GetValue(c);}
 static void Set(MainForm c,string name,object value){typeof(MainForm).GetField(name,F).SetValue(c,value);}
 static void Call(MainForm c,string name){typeof(MainForm).GetMethod(name,F).Invoke(c,null);}
 static void Pump(int millis){var t=Stopwatch.StartNew();while(t.ElapsedMilliseconds<millis){Application.DoEvents();Thread.Sleep(2);}Application.DoEvents();}
 static void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);}
 [STAThread] static void Main(string[] args){
  try{
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   string dir=args[0];
   using(var db=new Store(dir)){
    var user=db.Login(Store.OwnerEmail,"Teste-Pdv-2026");
    using(var form=new BenchWindow(dir)){
     form.Show();Pump(60);
     Set(form,"db",db);Set(form,"user",user);
     Call(form,"ReadPresentation");
     var nav=(SideNavigation)Get(form,"nav");
     nav.Reset(new[]{"Pedidos","Visão geral","Produtos","Meu caixa","Relatórios","Configurações","Meu perfil"});
     nav.Visible=true;
     Set(form,"initializing",false);
     Call(form,"LoadPage");Pump(60);
     var samples=new List<double>();
     for(int i=1;i<=28;i++){
      Control previous=(Control)Get(form,"body");
      var watch=Stopwatch.StartNew();
      int target=i%nav.Pages.Count;
      nav.SelectedIndex=target;
      string page=nav.SelectedPage;
      bool completed=false;
      while(watch.ElapsedMilliseconds<5000){
       Application.DoEvents();
       var current=(Control)Get(form,"body");
       if((string)Get(form,"page")==page && current!=previous && current.Parent!=null){
        completed=true;break;
       }
       Thread.Sleep(3);
      }
      Check(completed,"Navigation timed out: "+page+" actual="+Get(form,"page")+" nav="+nav.SelectedPage+" busy="+Get(form,"busy")+" init="+Get(form,"initializing")+" bodyChanged="+(!Object.ReferenceEquals(Get(form,"body"),previous))+" bodyParent="+(((Control)Get(form,"body")).Parent!=null)+" status="+((Label)Get(form,"status")).Text);
      Pump(20);
      double duration=watch.Elapsed.TotalMilliseconds;
      Console.WriteLine("NAV_LATENCY;"+page+";"+duration.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture));
      samples.Add(duration);
     }
     // Preserva rascunho verdadeiro quando existe uma alteracao.
     var draft=(Draft)Get(form,"draft");
     draft.note="Teste de persistencia 2.0.12";
     var previousPage=(Control)Get(form,"body");
     int next=(nav.SelectedIndex+1)%nav.Pages.Count;
     nav.SelectedIndex=next;
     var waiting=Stopwatch.StartNew();
     while(waiting.ElapsedMilliseconds<5000){
      Application.DoEvents();Thread.Sleep(5);
      if((Control)Get(form,"body")!=previousPage && ((Control)Get(form,"body")).Parent!=null)break;
     }
     Pump(30);
     lock(db.Sync)Check(db.GetDraft().note=="Teste de persistencia 2.0.12","Rascunho alterado nao foi salvo antes de trocar de tela");
     Console.WriteLine("SAVED_CHANGED_DRAFT;PASS");
     samples.Sort();
     Console.WriteLine("LATENCY_SUMMARY;MEDIAN;"+samples[samples.Count/2].ToString("0.0",System.Globalization.CultureInfo.InvariantCulture));
     Console.WriteLine("LATENCY_SUMMARY;MAX;"+samples[samples.Count-1].ToString("0.0",System.Globalization.CultureInfo.InvariantCulture));
     if(File.Exists(Path.Combine(dir,"diagnostico-navegacao.log")))foreach(var line in File.ReadAllLines(Path.Combine(dir,"diagnostico-navegacao.log")))Console.WriteLine("PROFILE;"+line);
    }
   }
  }catch(Exception e){Console.Error.WriteLine("NAV_TEST_FAIL "+e);Environment.Exit(1);}
 }
}
