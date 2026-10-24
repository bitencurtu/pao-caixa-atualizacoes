using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using PaoCaixa;

class MemoryBenchForm : MainForm {
 public MemoryBenchForm(string directory):base(directory){}
 protected override void OnShown(EventArgs e){} // fixture controlado, sem inicializacao online
}

class MemoryBench {
 [DllImport("user32.dll")] static extern uint GetGuiResources(IntPtr hProcess,uint flags);
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Set(object o,string field,object v){var f=typeof(MainForm).GetField(field,Flags);if(f==null)throw new Exception("Campo: "+field);f.SetValue(o,v);}
 static object Get(object o,string field){var f=typeof(MainForm).GetField(field,Flags);if(f==null)throw new Exception("Campo: "+field);return f.GetValue(o);}
 static void Invoke(object o,string name){var m=typeof(MainForm).GetMethod(name,Flags);if(m==null)throw new Exception("Método: "+name);m.Invoke(o,null);}
 static void Pump(int ms){var w=Stopwatch.StartNew();while(w.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(2);}}
 static void Collect(){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();Application.DoEvents();}
 static void Sample(string label,int step){Collect();using(var p=Process.GetCurrentProcess()){p.Refresh();Console.WriteLine("METRIC;"+label+";"+step+";"+(p.PrivateMemorySize64/1024/1024.0).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+";"+(p.WorkingSet64/1024/1024.0).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+";"+(GC.GetTotalMemory(false)/1024/1024.0).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+";"+GetGuiResources(p.Handle,0)+";"+GetGuiResources(p.Handle,1));}}
 [STAThread] static void Main(string[] args){
  try {
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   string path=args[0];
   using(var store=new Store(path)) {
    var user=store.Login(Store.OwnerEmail,"Teste-Pdv-2026");
    using(var form=new MemoryBenchForm(path)){
     form.Show();Pump(80);
     Set(form,"db",store);Set(form,"user",user);Invoke(form,"ReadPresentation");
     var nav=(SideNavigation)Get(form,"nav");
     // Evita salvar rascunhos por evento, navega pelo mesmo LoadPage do aplicativo.
     nav.Reset(new[]{"Pedidos","Produtos","Configurações","Meu perfil"});
     nav.Visible=true;
     Invoke(form,"LoadPage");Pump(120);
     Sample("initial",0);
     for(int i=1;i<=120;i++){
      nav.SelectedIndex=i%4;
      Invoke(form,"LoadPage");
      Pump(16);
      if(i<=12)Sample(new[]{"Pedidos","Produtos","Configurações","Meu perfil"}[i%4],i);
      if(i%20==0)Sample("switch",i);
     }
     nav.SelectedIndex=1;Invoke(form,"LoadPage");Pump(100);
     Sample("final",120);
     Console.WriteLine("BENCH_DONE;120;real_pages;Pedidos Produtos Configurações Meu perfil");
    }
   }
  }catch(Exception ex){Console.Error.WriteLine("BENCH_FAIL "+ex);Environment.Exit(1);}
 }
}
