using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using PaoCaixa;

class RealUsageAuditWindow:MainForm {
 public Action StartAudit;
 public RealUsageAuditWindow(string dir):base(dir){}
 protected override void OnShown(EventArgs e){if(StartAudit!=null)BeginInvoke(StartAudit);}
}
class RealUsageAudit {
 static int errors,findings;
 static readonly BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static object Get(MainForm form,string name){return typeof(MainForm).GetField(name,F).GetValue(form);}
 static void Set(MainForm form,string name,object value){typeof(MainForm).GetField(name,F).SetValue(form,value);}
 static void Invoke(MainForm form,string name,params object[] args){typeof(MainForm).GetMethod(name,F).Invoke(form,args);}
 static IEnumerable<T> Find<T>(Control root)where T:Control{
  foreach(Control child in root.Controls){
   if(child is T)yield return (T)child;
   foreach(T c in Find<T>(child))yield return c;
  }
 }
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);Console.WriteLine("USAGE_PASS;"+label);}
 static void Finding(string category,string message){findings++;Console.WriteLine("USAGE_BUG;"+category+";"+message);}
 static async Task<bool> Wait(Func<bool> condition,int ms=7000){
  var sw=Stopwatch.StartNew();
  while(sw.ElapsedMilliseconds<ms){
   if(condition())return true;
   await Task.Delay(12);
  }
  return false;
 }
 static void ReopenFixture(Store db){
  if(db.Open()==null)db.OpenCash(3000);
 }
 static Product SetupData(Store db){
  ReopenFixture(db);
  var p=new Product{name="Produto auditoria venda",code="AUDIT-2017-"+DateTime.UtcNow.Ticks,unit="un",category="Testes",price=155,cost=40,stock=600000,minStock=1000,active=true,icon="bread"};
  db.SaveProduct(p);
  p=db.Products().First(x=>x.code==p.code);
  long original=p.stock;
  var sale=db.Finish(new List<Line>{new Line{productId=p.id,quantity=1000}},0,
   new List<Payment>{new Payment{method="dinheiro",amount=55},new Payment{method="pix",amount=100}},100,"Venda mista de teste");
  Check(sale.change==45&&sale.total==155,"troco de venda mista calculado");
  Check(db.Products().First(x=>x.id==p.id).stock==original-1000,"baixa de estoque automatica");
  db.Cancel(sale.id,"Cancelamento de teste");
  Check(db.Products().First(x=>x.id==p.id).stock==original,"estoque restaurado no cancelamento");
  bool rejected=false;
  try{db.Cancel(sale.id,"Cancelamento duplicado");}catch{rejected=true;}
  Check(rejected,"cancelamento repetido bloqueado");
  long next=db.NextNumber();
  for(int i=0;i<213;i++){
   db.Finish(new List<Line>{new Line{productId=p.id,quantity=1000}},0,
     new List<Payment>{new Payment{method="pix",amount=155}},0,"Auditoria");
  }
  Check(db.NextNumber()==next+213,"numeracao unica em vendas consecutivas");
  var rows=db.Sales(DateTime.Today.AddDays(-1),DateTime.Today.AddDays(2),0,500);
  Console.WriteLine("AUDIT_SORT;count="+rows.Count+";numbers="+string.Join(",",rows.Take(10).Select(v=>v.number.ToString()).ToArray())+";last="+string.Join(",",rows.Skip(Math.Max(0,rows.Count-5)).Select(v=>v.number.ToString()).ToArray()));
  Check(rows.Count>=213&&rows[0].number>rows[1].number,"historico ordenado pela venda recente");
  var last=ReportResult.Build(db.Report(DateTime.Today.AddDays(-1),DateTime.Today.AddDays(2)));
  Check(last.count>=213&&last.revenue>=213*155,"relatorio inclui vendas da auditoria");
  Check(db.Products().First(x=>x.id==p.id).stock==original-213000,"estoque apos 213 vendas");
  var b=db.Backup("auditoria");
  Check(System.IO.File.Exists(b),"backup completo SQLite criado");
  Console.WriteLine("USAGE_DATA;SALES=213;CANCELED=1;BACKUP=CREATED");
  return p;
 }
 static async void Run(RealUsageAuditWindow form,Store db,User user,Product product){
  try{
   Set(form,"db",db);Set(form,"user",user);
   Invoke(form,"ReadPresentation");
   var nav=(SideNavigation)Get(form,"nav");
   nav.Reset(new[]{"Vendas","Produtos","Meu perfil"});
   nav.Visible=true;
   Set(form,"initializing",true); // teste controla diretamente as trocas
   var old=(Control)Get(form,"body");
   Invoke(form,"LoadPage");
   Check(await Wait(()=>Get(form,"page") as string=="Vendas"&&!Object.ReferenceEquals(old,Get(form,"body"))),"abrir Histórico de vendas");
   var body=(Control)Get(form,"body");
   var pager=Find<PaginationBar>(body).Single();
   var grid=Find<DataGridView>(body).First(g=>g.Columns.Count==6&&g.Columns[0].HeaderText=="Nº");
   Check(grid.Rows.Count==100&&pager.Next.Enabled,"histórico mostra primeiros 100 registros e Próxima");
   // Simular três cliques no mesmo instante, antes de qualquer consulta concluir.
   pager.Next.PerformClick();
   pager.Next.PerformClick();
   pager.Next.PerformClick();
   bool final=await Wait(()=>pager.PageText=="Página 4");
   if(final&&grid.Rows.Count==0)Finding("historico-vendas","cliques rápidos em Próxima saltam para uma página vazia mesmo quando há vendas");
   else Console.WriteLine("USAGE_RESULT;historico-vendas;page="+pager.PageText+";rows="+grid.Rows.Count);
   Check(Object.ReferenceEquals(body,Get(form,"body")),"histórico de vendas mantém painel ao paginar");

   // No ledger de um produto recém cadastrado há apenas uma movimentação.
   // O botão Próxima não deveria permitir sair de sua única página.
   bool ledgerDone=false;
   var timer=new System.Windows.Forms.Timer{Interval=90};
   timer.Tick+=(sender,e)=>{
    var ledger=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x!=form&&x.Text.StartsWith("Estoque •"));
    if(ledger==null)return;
    timer.Stop();
    try{
     var lgrid=Find<DataGridView>(ledger).First();
     var buttons=Find<Button>(ledger).ToList();
     var nextButton=buttons.FirstOrDefault(x=>x.Text=="Próxima");
     Check(lgrid.Rows.Count==1,"livro de estoque começa com movimentação inicial");
     Check(nextButton!=null,"livro de estoque tem botão Próxima");
     nextButton.PerformClick();
     if(lgrid.Rows.Count==0)Finding("historico-estoque","Próxima habilitado com apenas uma movimentação mostra página vazia");
     else Console.WriteLine("USAGE_RESULT;historico-estoque;rows="+lgrid.Rows.Count);
     ledgerDone=true;
    }catch(Exception ex){errors++;Console.Error.WriteLine("AUDIT_UI_ERROR "+ex);}
    finally{ledger.Close();}
   };
   timer.Start();
   Invoke(form,"StockLedger",product);
   Check(await Wait(()=>ledgerDone),"consulta do livro de estoque terminada");
   timer.Dispose();
   Console.WriteLine("USAGE_AUDIT_DONE;findings="+findings);
  }catch(Exception e){errors++;Console.Error.WriteLine("AUDIT_UI_ERROR "+e);}
  finally{Set(form,"busy",false);Set(form,"closingForUpdate",true);form.Close();}
 }
 [STAThread]static void Main(string[] args){
  try{
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   using(var db=new Store(args[0])){
    User user=db.Login(Store.OwnerEmail,"Teste-Pdv-2026");
    Product p=SetupData(db);
    using(var form=new RealUsageAuditWindow(args[0])){
     form.StartAudit=()=>Run(form,db,user,p);
     Application.Run(form);
    }
   }
  }catch(Exception e){errors++;Console.Error.WriteLine("AUDIT_CORE_ERROR "+e);}
  Console.WriteLine("USAGE_AUDIT_SUMMARY;bugs="+findings+";errors="+errors);
  if(errors>0)Environment.Exit(1);
 }
}
