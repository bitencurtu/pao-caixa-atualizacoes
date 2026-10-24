using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using PaoCaixa;
class DiagForm:MainForm {
 public DiagForm(string directory):base(directory){}
 protected override void OnShown(EventArgs e){} // sem abertura remota no cenário controlado
}
class UiDiag {
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Set(MainForm form,string f,object value){typeof(MainForm).GetField(f,F).SetValue(form,value);}
 static object Get(MainForm form,string f){return typeof(MainForm).GetField(f,F).GetValue(form);}
 static void Load(MainForm form){typeof(MainForm).GetMethod("LoadPage",F).Invoke(form,null);}
 static void Pump(int ms){var sw=Stopwatch.StartNew();while(sw.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(1);}}
 [STAThread] static void Main(string[] args) {
  try {
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   string dir=args[0];
   using(var store=new Store(dir)){
    var user=store.Login(Store.OwnerEmail,"Teste-Pdv-2026");
    using(var form=new DiagForm(dir)){
     form.Show();Pump(100);
     Set(form,"db",store);Set(form,"user",user);
     typeof(MainForm).GetMethod("ReadPresentation",F).Invoke(form,null);
     var nav=(SideNavigation)Get(form,"nav");
     nav.Reset(new[]{"Pedidos","Produtos","Configurações","Meu perfil"});
     nav.Visible=true;
     for(int i=0;i<12;i++){
      nav.SelectedIndex=i%nav.Pages.Count;
      Console.WriteLine("TEST_NAV;BEGIN;"+i+";"+nav.SelectedPage);
      Load(form);
      Pump(80);
      Console.WriteLine("TEST_NAV;END;"+i+";"+nav.SelectedPage+";UI_OK="+!form.IsDisposed);
     }
    }
   }
  }catch(Exception e){Console.Error.WriteLine("TEST_FAIL:"+e);Environment.Exit(1);}
 }
}
