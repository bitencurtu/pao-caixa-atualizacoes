using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using PaoCaixa;
class SalesBenchWindow:MainForm {
 public Action StartTest;
 public SalesBenchWindow(string directory):base(directory){}
 protected override void OnShown(EventArgs e){if(StartTest!=null)BeginInvoke(StartTest);}
}
class SalesHistoryVisualTests {
 static readonly BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static bool failed;
 static object Get(MainForm f,string field){return typeof(MainForm).GetField(field,F).GetValue(f);}
 static void Set(MainForm f,string field,object value){typeof(MainForm).GetField(field,F).SetValue(f,value);}
 static void Call(MainForm f,string method){typeof(MainForm).GetMethod(method,F).Invoke(f,null);}
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS sales-visual "+message);}
 static DataGridView FindGrid(Control root){
  foreach(Control c in root.Controls){
   var g=c as DataGridView;
   if(g!=null&&g.Columns.Count==6&&g.Columns[0].HeaderText=="Nº")return g;
   var nested=FindGrid(c);if(nested!=null)return nested;
  }
  return null;
 }
 static Button FindButton(Control root,string text){
  foreach(Control c in root.Controls){
   var b=c as Button;
   if(b!=null&&b.Text==text)return b;
   var nested=FindButton(c,text);if(nested!=null)return nested;
  }
  return null;
 }
 static async Task<bool> WaitPage(MainForm f,string expected,Control previous){
  var clock=Stopwatch.StartNew();
  while(clock.ElapsedMilliseconds<5000){
   var current=(Control)Get(f,"body");
   if((string)Get(f,"page")==expected&&current!=previous&&current.Parent!=null)return true;
   await Task.Delay(10);
  }
  return false;
 }
 static async void Run(SalesBenchWindow form,Store db,User user){
  try{
   Set(form,"db",db);Set(form,"user",user);Call(form,"ReadPresentation");
   var nav=(SideNavigation)Get(form,"nav");nav.Reset(new[]{"Vendas","Produtos","Meu perfil"});
   nav.Visible=true;Set(form,"initializing",true);
   var previous=(Control)Get(form,"body");
   Call(form,"LoadPage");
   Check(await WaitPage(form,"Vendas",previous),"sales page loaded");
   var grid=FindGrid((Control)Get(form,"body"));
   Check(grid!=null,"sales grid exists");
   int expected;
   lock(db.Sync)expected=Math.Min(100,db.Sales(DateTime.Today,DateTime.Today.AddDays(1),0).Count);
   Check(grid.Rows.Count==expected,"first visible grid already has complete sale rows");
   var button=FindButton((Control)Get(form,"body"),"Consultar");
   Check(button!=null,"sales filtering button preserved");
   int bodyToggles=0,navToggles=0;
   Control body=(Control)Get(form,"body");
   body.EnabledChanged+=(sender,e)=>bodyToggles++;
   nav.EnabledChanged+=(sender,e)=>navToggles++;
   button.PerformClick();await Task.Delay(140);
   Check(grid.Rows.Count==expected,"manual sales query renders same results");
   Check(bodyToggles==0&&navToggles==0,"filter query does not disable or flash entire interface");
   previous=body;nav.SelectedIndex=1;Call(form,"LoadPage");
   Check(await WaitPage(form,"Produtos",previous),"navigation away from sales still works");
   previous=(Control)Get(form,"body");nav.SelectedIndex=0;Call(form,"LoadPage");
   Check(await WaitPage(form,"Vendas",previous),"reopening sales page works");
   grid=FindGrid((Control)Get(form,"body"));
   Check(grid!=null&&grid.Rows.Count==expected,"reopened sales grid stays fully populated");
   Console.WriteLine("SALES_VISUAL_DONE");
  }catch(Exception e){failed=true;Console.Error.WriteLine("SALES_VISUAL_FAIL "+e);}
  finally{form.Close();}
 }
 [STAThread] static void Main(string[] args){
  try{
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   using(var db=new Store(args[0])){
    var user=db.Login(Store.OwnerEmail,"Teste-Pdv-2026");
    using(var form=new SalesBenchWindow(args[0])){
     form.StartTest=()=>Run(form,db,user);
     Application.Run(form);
    }
   }
  }catch(Exception ex){failed=true;Console.Error.WriteLine("SALES_VISUAL_FAIL "+ex);}
  if(failed)Environment.Exit(1);
 }
}
