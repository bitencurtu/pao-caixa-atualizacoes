using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using PaoCaixa;

sealed class CashPagingTestWindow : MainForm {
 public Action BeginTests;
 public CashPagingTestWindow(string path):base(path){}
 protected override void OnShown(EventArgs e){if(BeginTests!=null)BeginInvoke(BeginTests);}
}
static class CashPaginationTests {
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static bool failed;
 static object Get(MainForm form,string field){return typeof(MainForm).GetField(field,Flags).GetValue(form);}
 static void Set(MainForm form,string field,object value){typeof(MainForm).GetField(field,Flags).SetValue(form,value);}
 static void Call(MainForm form,string name){typeof(MainForm).GetMethod(name,Flags).Invoke(form,null);}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("CASH_PAGE_PASS "+message);}
 static IEnumerable<T> Find<T>(Control root) where T:Control{
  foreach(Control c in root.Controls){
   var v=c as T;if(v!=null)yield return v;
   foreach(var nested in Find<T>(c))yield return nested;
  }
 }
 static async Task<bool> Until(Func<bool> condition,int max=5000){
  var clock=Stopwatch.StartNew();
  while(clock.ElapsedMilliseconds<max){
   if(condition())return true;
   await Task.Delay(10);
  }
  return false;
 }
 static async void Run(CashPagingTestWindow form,Store db,User user){
  try{
   Set(form,"db",db);Set(form,"user",user);Call(form,"ReadPresentation");
   var nav=(SideNavigation)Get(form,"nav");
   nav.Reset(new[]{"Meu caixa","Meu perfil"});nav.Visible=true;
   Set(form,"initializing",true);
   var original=(Control)Get(form,"body");
   Call(form,"LoadPage");
   Check(await Until(()=>!Object.ReferenceEquals(original,Get(form,"body"))&&((Control)Get(form,"body")).Parent!=null),"opening Meu caixa");
   var body=(Control)Get(form,"body");
   var tables=Find<DataGridView>(body).ToList();
   var movements=tables.Single(x=>x.Columns.Count>0&&x.Columns[0].HeaderText=="Data e hora");
   var registers=tables.Single(x=>x.Columns.Count>0&&x.Columns[0].HeaderText=="Abertura");
   var movementPager=Find<PaginationBar>(movements.Parent).Single();
   var registerPager=Find<PaginationBar>(registers.Parent).Single();
   var opened=db.Open();Check(opened!=null,"fixture has an open cash session");
   int firstMoves=Math.Min(100,db.CashMoves(opened.id,0).Count);
   int laterMoves=Math.Min(100,db.CashMoves(opened.id,100).Count);
   int firstRegisters=Math.Min(100,db.Registers(0).Count);
   int laterRegisters=Math.Min(100,db.Registers(100).Count);
   Check(firstMoves==100&&laterMoves>0&&firstRegisters==100&&laterRegisters>0,"fixture contains two pages for each history");
   Check(movements.Rows.Count==firstMoves&&registers.Rows.Count==firstRegisters,"first pages populated");
   movementPager.Next.PerformClick();
   Check(await Until(()=>movementPager.PageText=="Página 2"),"movement next page");
   Check(movements.Rows.Count==laterMoves,"movement rows changed only in the movement table");
   Check(registers.Rows.Count==firstRegisters&&registerPager.PageText=="Página 1","register list untouched by movement paging");
   Check(Object.ReferenceEquals(body,Get(form,"body"))&&Object.ReferenceEquals(movements,Find<DataGridView>(body).Single(x=>x.Columns[0].HeaderText=="Data e hora")),"cash page and grid kept alive");
   registerPager.Next.PerformClick();
   Check(await Until(()=>registerPager.PageText=="Página 2"),"register history next page");
   Check(registers.Rows.Count==laterRegisters,"register rows updated");
   Check(registers.Rows[0].Tag is Register,"register row Tag preserved for detail view");
   Check(movementPager.PageText=="Página 2"&&movements.Rows.Count==laterMoves,"movements unchanged by register paging");
   Check(Object.ReferenceEquals(body,Get(form,"body")),"neither pager reconstructs Meu caixa");
   registerPager.Previous.PerformClick();
   Check(await Until(()=>registerPager.PageText=="Página 1"),"register previous page");
   movementPager.Previous.PerformClick();
   Check(await Until(()=>movementPager.PageText=="Página 1"),"movement previous page");
   Check(registers.Rows.Count==firstRegisters&&movements.Rows.Count==firstMoves,"both histories restore first page");
   Console.WriteLine("CASH_PAGINATION_COMPLETE");
  }catch(Exception e){failed=true;Console.Error.WriteLine("CASH_PAGING_FAILED "+e);}
  finally{form.Close();}
 }
 [STAThread]static void Main(string[] args){
  try{
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   using(var db=new Store(args[0])){
    var user=db.Login(Store.OwnerEmail,"Teste-Pdv-2026");
    var empty=D.Methods.ToDictionary(m=>m,m=>0L);
    if(db.Open()!=null)db.CloseCash(empty);
    for(int i=0;i<104;i++){db.OpenCash(100);db.CloseCash(empty);}
    db.OpenCash(100);
    for(int i=0;i<104;i++)db.MoveCash("supply",1,"Teste de paginação");
    using(var form=new CashPagingTestWindow(args[0])){
     form.BeginTests=()=>Run(form,db,user);
     Application.Run(form);
    }
   }
  }catch(Exception ex){failed=true;Console.Error.WriteLine("CASH_PAGING_FAILED "+ex);}
  if(failed)Environment.Exit(1);
 }
}
