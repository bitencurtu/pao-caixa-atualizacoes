using System;
using System.Drawing;
using System.Windows.Forms;
namespace PaoCaixa {
 public partial class MainForm {
  void BuildClassicSettings(){
   var p=Content(false);p.AutoScroll=true;
   var data=new CardPanel{Padding=new Padding(0)};data.Controls.Add(new SectionHeading("Dados da padaria","Usados no sistema e no comprovante."));
   var name=new TextBox{Text=shop.name,MaxLength=80};var operatorName=new TextBox{Text=shop.@operator,MaxLength=60};var footer=new TextBox{Text=shop.footer,MaxLength=200};
   var nameBox=new FieldBox(name);var operatorBox=new FieldBox(operatorName);var footerBox=new FieldBox(footer);
   var nameLabel=Ui.Caption("Nome da padaria");var operatorLabel=Ui.Caption("Operador padrão");var footerLabel=Ui.Caption("Mensagem no comprovante");
   var save=Ui.Primary("Salvar configurações",()=>{var settings=new Settings{name=name.Text,@operator=operatorName.Text,footer=footer.Text};Fire(()=>{db.SaveSettings(settings);return null;},x=>Reload());},"check");save.AutoSize=false;
   data.Controls.AddRange(new Control[]{nameLabel,nameBox,operatorLabel,operatorBox,footerLabel,footerBox,save});
   Action dataLayout=()=>{int w=data.ClientSize.Width-44;nameLabel.SetBounds(22,99,w,23);nameBox.SetBounds(22,127,w,43);operatorLabel.SetBounds(22,185,w,23);operatorBox.SetBounds(22,213,w,43);footerLabel.SetBounds(22,271,w,23);footerBox.SetBounds(22,299,w,43);save.SetBounds(22,365,w,45);};data.SizeChanged+=(sender,e)=>dataLayout();
   var backups=new CardPanel{Padding=new Padding(0)};backups.Controls.Add(new SectionHeading("Backup dos dados","Uma cópia para proteger seus registros."));
   var backupNotice=Ui.Caption("Salvando no banco offline.\nOs registros ficam neste computador, separados da instalação.");backupNotice.AutoSize=false;
   var complete=Ui.Primary("Baixar backup completo",()=>Backup(),"download");complete.AutoSize=false;
   var commercial=Ui.Button("Backup comercial JSON",()=>BusinessBackup());commercial.AutoSize=false;
   var restore=Ui.Button("Importar backup JSON",()=>Restore());restore.AutoSize=false;
   var folder=Ui.Link("Abrir pasta de backups",()=>System.Diagnostics.Process.Start(db.Backups),"storage");folder.AutoSize=false;
   var backupNote=Ui.Caption("O backup completo inclui contas e pedidos em andamento. A importação JSON substitui os dados comerciais após sua confirmação.");backupNote.AutoSize=false;
   backups.Controls.AddRange(new Control[]{backupNotice,complete,commercial,restore,folder,backupNote});
   Action backupLayout=()=>{int w=backups.ClientSize.Width-44;backupNotice.SetBounds(22,101,w,66);complete.SetBounds(22,183,w,43);commercial.SetBounds(22,236,w,40);restore.SetBounds(22,286,w,40);folder.SetBounds(22,334,w,40);backupNote.SetBounds(22,383,w,58);};backups.SizeChanged+=(sender,e)=>backupLayout();
   var update=new CardPanel{Padding=new Padding(0)};update.Controls.Add(new SectionHeading("Atualizações do programa","Ao iniciar, o programa procura e instala novas versões com internet."));
   updateLabel=Ui.Label(updates.Status);updateLabel.AutoSize=false;var channel=Ui.Caption("Canal: "+prefs.url);channel.AutoSize=false;
   var actions=Ui.Flow();actions.Dock=DockStyle.None;actions.AutoSize=false;
   actions.Controls.Add(Ui.Button("Procurar atualização",()=>CheckUpdate()));actions.Controls.Add(Ui.Button("Baixar atualização",()=>DownloadUpdate()));actions.Controls.Add(Ui.Primary("Instalar e reiniciar",()=>InstallUpdate(),"download"));
   if(user.Owner)actions.Controls.Add(Ui.Button("Alterar canal",()=>ChangeUpdateChannel(channel)));
   var updateNotice=Ui.Caption("Feche o caixa e conclua os pedidos antes de instalar. O programa criará um backup dos dados.");updateNotice.AutoSize=false;
   var storage=Ui.Caption("Banco local: "+db.FilePath);storage.AutoSize=false;
   update.Controls.AddRange(new Control[]{updateLabel,channel,actions,updateNotice,storage});
   Action updateLayout=()=>{int w=update.ClientSize.Width-44;updateLabel.SetBounds(22,105,w,73);channel.SetBounds(22,188,w,44);actions.SetBounds(18,244,w+6,w<800?100:48);updateNotice.SetBounds(22,w<800?360:303,w,49);storage.SetBounds(22,w<800?414:357,w,44);};update.SizeChanged+=(sender,e)=>updateLayout();
   p.Controls.AddRange(new Control[]{data,backups,update});Action arrange=()=>{int w=Math.Max(380,p.Width-20);bool two=w>=790;int cw=two?(w-24)/2:w;data.SetBounds(0,0,cw,466);backups.SetBounds(two?cw+24:0,two?0:490,cw,466);int y=two?490:980;update.SetBounds(0,y,w,w<800?485:420);dataLayout();backupLayout();updateLayout();p.AutoScrollMinSize=new Size(0,y+update.Height+14);};p.SizeChanged+=(sender,e)=>arrange();arrange();
  }
  void ChangeUpdateChannel(Label channelLabel){
   lock(db.Sync)db.Owner();if(updateChecking||updateDownloading)throw new Exception("Aguarde a consulta ou o download terminar antes de alterar o canal.");
   using(var form=new Fields("Canal de atualizações")){
    form.ClientSize=new Size(600,200);form.Ok.Text="Salvar e verificar";var text=form.TextField("Endereço HTTPS",prefs.url);text.MaxLength=2048;
    if(Ui.Show(this,form)!=DialogResult.OK)return;
    string url=Preferences.ValidateUrl(text.Text);var saved=new Preferences{theme=prefs.theme,url=url};saved.Save(dataDir);prefs=saved;
    updates.Available=null;updates.Downloaded=null;updates.Status="Canal salvo. Verificando conexão…";channelLabel.Text="Canal salvo: "+url;UpdateStatus();CheckUpdate();
   }
  }
 }
}
