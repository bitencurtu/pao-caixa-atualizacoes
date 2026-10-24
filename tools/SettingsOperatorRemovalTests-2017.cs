using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using PaoCaixa;
class SettingsQaForm : MainForm {
 public Action StartQa;
 public SettingsQaForm(string path):base(path){}
 protected override void OnShown(EventArgs e){if(StartQa!=null)BeginInvoke(StartQa);}
}
class SettingsOperatorRemovalTests {
 static bool failed;
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(MainForm f,string n){return typeof(MainForm).GetField(n,F).GetValue(f);}
 static void Set(MainForm f,string n,object value){typeof(MainForm).GetField(n,F).SetValue(f,value);}
 static void Invoke(MainForm f,string n,params object[] values){typeof(MainForm).GetMethod(n,F).Invoke(f,values);}
 static void Check(bool good,string message){if(!good)throw new Exception("SETTING_TEST_FAIL "+message);Console.WriteLine("SETTING_PASS "+message);}
 static IEnumerable<T> Find<T>(Control c)where T:Control{
  foreach(Control child in c.Controls){
   var obj=child as T;
   if(obj!=null)yield return obj;
   foreach(T nested in Find<T>(child))yield return nested;
  }
 }
 static async void Run(SettingsQaForm form,Store store,User user){
  try{
   Set(form,"db",store);Set(form,"user",user);
   Invoke(form,"ReadPresentation");
   var initial=store.Shop();
   var settings=new Settings{name=initial.name,@operator="Operador legado preservado",footer=initial.footer};
   store.SaveSettings(settings);
   Invoke(form,"ReadPresentation");
   Invoke(form,"Root","Configurações");
   Invoke(form,"BuildClassicSettings");
   foreach(var size in new[]{new Size(850,650),new Size(1120,720),new Size(1440,850)}){
    form.ClientSize=size;form.PerformLayout();await Task.Delay(15);
    var labels=Find<Label>(form).Select(x=>x.Text).ToList();
    Check(labels.Contains("Nome da padaria")&&labels.Contains("Mensagem no comprovante"),"useful fields remain at "+size.Width);
    Check(!labels.Any(x=>x.Contains("Operador padrão")),"operator label removed at "+size.Width);
   }
   var txt=Find<TextBox>((Panel)Field(form,"body")).ToList();
   Check(txt.Count==2,"exactly two editable settings fields remain");
   var save=Find<Button>(form).SingleOrDefault(x=>x.Text=="Salvar configurações");
   Check(save!=null,"save settings button retained");
   var now=store.Shop();
   txt.Single(x=>x.Text==now.name).Text="Padaria Teste Responsivo";
   txt.Single(x=>x.Text==now.footer).Text="Muito obrigado!";
   save.PerformClick();
   var start=System.Diagnostics.Stopwatch.StartNew();
   bool done=false;
   while(start.ElapsedMilliseconds<5000){
    await Task.Delay(20);
    var updated=store.Shop();
    if(updated.name=="Padaria Teste Responsivo"&&updated.footer=="Muito obrigado!"){
     Check(updated.@operator=="Operador legado preservado","legacy operator value preserved on save");
     done=true;break;
    }
   }
   Check(done,"name and receipt footer saved through UI");
   Console.WriteLine("SETTINGS_OPERATOR_REMOVAL_DONE");
  }catch(Exception ex){failed=true;Console.Error.WriteLine(ex);}
  finally{Set(form,"busy",false);Set(form,"closingForUpdate",true);form.Close();}
 }
 [STAThread] static void Main(string[] args){
  try{
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   using(var store=new Store(args[0])){
    var user=store.Login(Store.OwnerEmail,"Teste-Pdv-2026");
    using(var form=new SettingsQaForm(args[0])){
     form.StartQa=()=>Run(form,store,user);
     Application.Run(form);
    }
   }
  }catch(Exception ex){failed=true;Console.Error.WriteLine(ex);}
  if(failed)Environment.Exit(1);
 }
}
