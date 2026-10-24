using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace PaoCaixa {
 public partial class MainForm {
  bool startupRunning,startupCloseRequested;CancellationTokenSource startupCancellation;
#if UI_QA
  internal Action<Updates,CancellationToken> StartupCheckForTests;
#endif
  async Task UpdateBeforeLogin(){
   startupRunning=true;startupCancellation=new CancellationTokenSource();nav.Visible=false;header.Page="Iniciando";Root("Preparando o Pão & Caixa");
   var p=Content(false);var card=new CardPanel{Size=new Size(520,255)};p.Controls.Add(card);
   var title=Ui.Label("Preparando sua padaria",18);title.SetBounds(26,22,468,35);
   var message=Ui.Label("Verificando atualizações…");message.AutoSize=false;message.SetBounds(26,76,468,44);
   var progress=new ProgressBar{Style=ProgressBarStyle.Marquee,MarqueeAnimationSpeed=30};progress.SetBounds(27,132,466,10);
   Button cancel=null;cancel=Ui.Button("Continuar sem atualizar",()=>{cancel.Enabled=false;message.Text="Interrompendo a atualização…";startupCancellation.Cancel();});cancel.AutoSize=false;cancel.SetBounds(27,178,466,44);
   card.Controls.AddRange(new Control[]{title,message,progress,cancel});Action arrange=()=>card.Location=new Point(Math.Max(0,(p.ClientSize.Width-card.Width)/2),Math.Max(8,(p.ClientSize.Height-card.Height)/2));p.SizeChanged+=(s,e)=>arrange();arrange();ApplyTheme();
   Action<string,int> report=(text,percent)=>{if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke(new Action(()=>{if(!startupRunning||message.IsDisposed)return;message.Text=text;status.Text=text;if(percent>=0){progress.Style=ProgressBarStyle.Continuous;progress.Value=Math.Max(0,Math.Min(100,percent));}}));}catch(InvalidOperationException){}};
   var job=new StartupUpdate(updates,prefs.url,dataDir,()=>{lock(db.Sync)return db.StartupUpdateBlockReason();},()=>{lock(db.Sync)db.PrepareStartupInstall();},exe=>{
    Invoke(new Action(()=>{Process.Start(new ProcessStartInfo(exe,StartupUpdate.InstallerArguments(AppDomain.CurrentDomain.BaseDirectory)){UseShellExecute=true});closingForUpdate=true;Close();}));
   });
#if UI_QA
   if(StartupCheckForTests!=null)job.Check=token=>StartupCheckForTests(updates,token);
#endif
   StartupResult result;try{result=await Task.Run(()=>job.Run(report,startupCancellation.Token));}
   finally{startupRunning=false;startupCancellation.Dispose();startupCancellation=null;}
   if(IsDisposed||result.Installing)return;if(startupCloseRequested){Close();return;}
   initializing=false;updateTimer.Interval=4*60*60*1000;statusText=(statusText==null?"":statusText+" ")+result.Message;LoginPage();if(!remote.Denied)status.Text=statusText;
  }
 }
}
