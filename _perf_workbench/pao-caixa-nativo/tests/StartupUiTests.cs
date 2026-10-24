using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using PaoCaixa;
class StartupUiTests {
 static object Field(MainForm f,string name){return typeof(MainForm).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f);}
 static Button Button(Control parent,string text){foreach(Control c in parent.Controls){var b=c as Button;if(b!=null&&b.Text==text)return b;var nested=Button(c,text);if(nested!=null)return nested;}return null;}
 static void Snap(Form f,string dir,string name){f.Refresh();Application.DoEvents();using(var image=new Bitmap(f.ClientSize.Width,f.ClientSize.Height)){using(var g=Graphics.FromImage(image))g.CopyFromScreen(f.PointToScreen(Point.Empty),Point.Empty,f.ClientSize);image.Save(Path.Combine(dir,name+".png"));}}
 [STAThread] static void Main(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Directory.CreateDirectory(args[1]);
  foreach(bool close in new[]{false,true}){
   string directory=Path.Combine(args[0],close?"close":"cancel");Directory.CreateDirectory(directory);var form=new MainForm(directory);int phase=0,ticks=0,checking=0;var timer=new System.Windows.Forms.Timer{Interval=180};
   form.StartupCheckForTests=(updater,token)=>{Interlocked.Exchange(ref checking,1);token.WaitHandle.WaitOne();token.ThrowIfCancellationRequested();};
   timer.Tick+=(s,e)=>{try{if(++ticks>80)throw new Exception("Startup did not finish after cancellation");if(phase==0){if(!(bool)Field(form,"startupRunning")||Thread.VolatileRead(ref checking)==0)return;var body=(Panel)Field(form,"body");if(((SideNavigation)Field(form,"nav")).Visible||Button(body,"Entrar")!=null)throw new Exception("Login/register accessible during update");phase=1;
     if(close){form.Close();return;}Snap(form,args[1],"abertura-claro");((ThemeSwitch)Field(form,"theme")).SelectedIndex=1;Snap(form,args[1],"abertura-escuro");Button(body,"Continuar sem atualizar").PerformClick();return;}
    if(!close&&!(bool)Field(form,"startupRunning")){if(Button((Panel)Field(form,"body"),"Entrar")==null)throw new Exception("Cancellation did not release login");if(!((Label)Field(form,"status")).Text.Contains("interrompida"))throw new Exception("Cancellation message missing");Snap(form,args[1],"login-apos-cancelar");Console.WriteLine("PASS UI startup gate, both themes and safe cancellation to login");timer.Stop();form.Close();}
   }catch(Exception ex){Console.WriteLine("FAIL startup UI "+ex);Environment.Exit(1);}};
   form.Shown+=(s,e)=>timer.Start();Application.Run(form);timer.Dispose();int callbacks=0;typeof(MainForm).GetMethod("PostUi",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,new object[]{new Action(()=>callbacks++)});Application.DoEvents();if(callbacks!=0)throw new Exception("Callback executado depois de fechar janela.");Console.WriteLine("PASS UI callback after window close is ignored safely");if(close)Console.WriteLine("PASS UI close during network check cancels then exits safely");
  }
 }
}
