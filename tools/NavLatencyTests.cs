using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using PaoCaixa;
class BenchWindow : MainForm {
 public Action StartBenchmark;
 public BenchWindow(string dir):base(dir){}
 protected override void OnShown(EventArgs e){
  if(StartBenchmark!=null)BeginInvoke(StartBenchmark);
 }
}
class NavigationLatencyTests {
 static readonly BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static int failed;
 static object Get(MainForm c,string name){return typeof(MainForm).GetField(name,F).GetValue(c);}
 static void Set(MainForm c,string name,object value){typeof(MainForm).GetField(name,F).SetValue(c,value);}
 static void Call(MainForm c,string name){typeof(MainForm).GetMethod(name,F).Invoke(c,null);}
 static void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);}
 static async void Run(BenchWindow form,Store db,User user,string dir){
  try{
   Set(form,"db",db);Set(form,"user",user);
   Call(form,"ReadPresentation");
   var nav=(SideNavigation)Get(form,"nav");
   nav.Reset(new[]{"Pedidos","Visão geral","Produtos","Meu caixa","Relatórios","Configurações","Meu perfil"});
   nav.Visible=true;
   Set(form,"initializing",true);
   Call(form,"LoadPage");
   await Task.Delay(110);
   var samples=new List<double>();
   for(int i=1;i<=28;i++){
    var previous=(Control)Get(form,"body");
    var watch=Stopwatch.StartNew();
    nav.SelectedIndex=i%nav.Pages.Count;
    string expected=nav.SelectedPage;
    Call(form,"LoadPage");
    bool completed=false;
    while(watch.ElapsedMilliseconds<5000){
     var current=(Control)Get(form,"body");
     if((string)Get(form,"page")==expected&&current!=previous&&current.Parent!=null){
      completed=true;break;
     }
     await Task.Delay(8);
    }
    Check(completed,"Navigation timed out: "+expected+" actual="+Get(form,"page")+" busy="+Get(form,"busy")+" bodyChanged="+(!Object.ReferenceEquals(Get(form,"body"),previous)));
    await Task.Delay(20);
    double duration=watch.Elapsed.TotalMilliseconds;
    Console.WriteLine("NAV_LATENCY;"+expected+";"+duration.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture));
    samples.Add(duration);
   }
   // Confirma que pedidos alterados continuam salvos, inclusive antes de mudar de pagina.
   var draft=(Draft)Get(form,"draft");
   draft.note="Teste de persistencia 2.0.12";
   bool saved=false;
   typeof(MainForm).GetMethod("SaveDraft",F).Invoke(form,new object[]{new Action(()=>saved=true)});
   var timer=Stopwatch.StartNew();
   while(!saved&&timer.ElapsedMilliseconds<5000)await Task.Delay(8);
   Check(saved,"Salvamento assíncrono não concluiu");
   lock(db.Sync)Check(db.GetDraft().note=="Teste de persistencia 2.0.12","Rascunho alterado nao foi salvo");
   Console.WriteLine("SAVED_CHANGED_DRAFT;PASS");
   samples.Sort();
   Console.WriteLine("LATENCY_SUMMARY;MEDIAN;"+samples[samples.Count/2].ToString("0.0",System.Globalization.CultureInfo.InvariantCulture));
   Console.WriteLine("LATENCY_SUMMARY;MAX;"+samples[samples.Count-1].ToString("0.0",System.Globalization.CultureInfo.InvariantCulture));
   if(File.Exists(Path.Combine(dir,"diagnostico-navegacao.log")))
    foreach(var line in File.ReadAllLines(Path.Combine(dir,"diagnostico-navegacao.log")))
     Console.WriteLine("PROFILE;"+line);
  }catch(Exception ex){failed=1;Console.Error.WriteLine("NAV_TEST_FAIL "+ex);}
  finally{form.Close();}
 }
 [STAThread] static void Main(string[] args){
  try{
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   string dir=args[0];
   using(var db=new Store(dir)){
    var user=db.Login(Store.OwnerEmail,"Teste-Pdv-2026");
    using(var form=new BenchWindow(dir)){
     form.StartBenchmark=()=>Run(form,db,user,dir);
     Application.Run(form);
    }
   }
  }catch(Exception ex){failed=1;Console.Error.WriteLine("NAV_TEST_FAIL "+ex);}
  if(failed!=0)Environment.Exit(1);
 }
}
