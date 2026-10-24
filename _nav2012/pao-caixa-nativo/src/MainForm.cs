using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace PaoCaixa {
 public partial class MainForm:Form {
  readonly string dataDir; readonly RemoteControl remote; Store db;Preferences prefs;User user;Settings shop;
  readonly Panel shell=new Panel{Dock=DockStyle.Fill};
  Panel body=new Panel{Dock=DockStyle.Fill,Padding=new Padding(28)};
  readonly SideNavigation nav=new SideNavigation{Visible=false};
  readonly TopBar header=new TopBar();
  readonly Label status=Ui.Caption("Abrindo os dados…");
  readonly ThemeSwitch theme;
  readonly SmoothWheelRouter wheelRouter;

  bool busy,initializing=true,closingForUpdate,updateFlowRunning,preparingPage;int pageLoadSerial;OwnerView ownerViewReady;ReportView reportViewReady;CashView cashViewReady;string page;int registerOffset,cashOffset;readonly Updates updates=new Updates();Label updateLabel;
  List<Product> products=new List<Product>();Draft draft=new Draft();string lastSavedDraftJson;ProductCatalog catalogue;DataGridView cartGrid;CartList cartView;CategoryTabs categories;TextBox search,discount,note;Label total,subtotal,catalogCaption;SectionHeading cartHeading;Button receive;bool filling;Register currentRegister;bool cashOccupied;long nextNumber;
  readonly Timer draftTimer=new Timer{Interval=450};readonly Timer updateTimer=new Timer{Interval=30000};
  // Consulta automatica em segundo plano a cada 15 segundos. Nunca bloqueia a UI
  // esperando a rede, nem abre janelas a cada consulta.
  readonly Timer remoteTimer=new Timer{Interval=15000};bool remoteChecking,started,remoteLockedScreen,pendingRemoteLock;
  public MainForm(string directory){
   dataDir=directory;
   wheelRouter=new SmoothWheelRouter(this);
#if UI_QA
   remote=new RemoteControl(directory,Path.Combine(directory,"qa-remote-control.json"));
#else
   remote=new RemoteControl(directory);
#endif
   prefs=Preferences.Load(directory);Ui.Dark=prefs.theme=="dark";theme=header.Theme;
   Text="Pão & Caixa — "+Updates.Version;Font=Ui.Font();var workArea=Screen.PrimaryScreen.WorkingArea;ClientSize=new Size(Math.Min(1280,Math.Max(800,workArea.Width-40)),Math.Min(780,Math.Max(550,workArea.Height-70)));MinimumSize=new Size(820,590);StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;KeyPreview=true;
   try{using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.ico"))Icon=new Icon(stream);}catch{}
   theme.SelectedIndex=Ui.Dark?1:0;status.Dock=DockStyle.Bottom;status.Padding=new Padding(28,0,8,0);status.AutoSize=false;status.Height=25;status.Font=Ui.Font(8);
   shell.Controls.Add(body);shell.Controls.Add(header);shell.Controls.Add(status);Controls.Add(shell);Controls.Add(nav);
   nav.Logout=()=>GuardUi(()=>{if(!busy)Logout();});nav.Profile=()=>Go("Meu perfil");
   header.CashAction=()=>GuardUi(()=>{if(busy||user==null)return;if(user.role!="financial"&&currentRegister==null&&!cashOccupied)OpenCash();else Go("Meu caixa");});
   theme.SelectedIndexChanged+=(sender,e)=>{try{prefs.theme=theme.SelectedIndex==1?"dark":"light";Ui.Dark=prefs.theme=="dark";prefs.Save(dataDir);ApplyTheme();}catch(Exception ex){MessageBox.Show(ex.Message,"Tema");}};
   nav.SelectedIndexChanged+=(sender,e)=>{if(!initializing&&user!=null&&!busy)FlushDraftThen(()=>LoadPage());};
   draftTimer.Tick+=(sender,e)=>{draftTimer.Stop();if(!busy&&user!=null&&user.role!="financial")SaveDraft(null);else draftTimer.Start();};
   Shown+=async(sender,e)=>{await Run(()=>{remote.Check();db=new Store(dataDir,remote);try{db.DailyBackup();}catch{statusText="Backup diário não concluído. Confira o espaço livre em Configurações.";}return null;});if(!IsDisposed&&db!=null){started=true;await UpdateBeforeLogin();}};
   FormClosing+=(sender,e)=>{if(closingForUpdate)return;if(startupRunning){e.Cancel=true;startupCloseRequested=true;startupCancellation.Cancel();return;}if(busy){e.Cancel=true;return;}if(user!=null&&user.role!="financial"){try{var finalDraft=Snapshot();string finalJson=J.Write(finalDraft);if(finalJson!=lastSavedDraftJson){lock(db.Sync)db.SetDraft(finalDraft);lastSavedDraftJson=finalJson;}}catch(Exception ex){e.Cancel=true;MessageBox.Show(ex.Message,"Não foi possível guardar o pedido");}}};
   updateTimer.Tick+=(sender,e)=>{if(user!=null&&user.role=="admin"&&!busy&&!updateChecking&&!updateDownloading&&!updateFlowRunning){CheckUpdate();updateTimer.Interval=4*60*60*1000;}};updateTimer.Start();
   remoteTimer.Tick+=(sender,e)=>{
    if(!started||IsDisposed)return;
    // Se o bloqueio chegou enquanto havia uma operacao/dialogo, aplica
    // assim que for seguro trocar a tela.
    if(pendingRemoteLock)ApplyRemoteState();
    if(!remoteChecking)RefreshRemote();
   };remoteTimer.Start();
   FormClosed+=(sender,e)=>{wheelRouter.Dispose();remoteTimer.Dispose();updateTimer.Dispose();draftTimer.Dispose();if(db!=null)db.Dispose();if(cartGrid!=null)cartGrid.Dispose();};ApplyTheme();
  }
  void GuardUi(Action action){try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Pão & Caixa",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
  protected override void Dispose(bool disposing){if(disposing&&wheelRouter!=null)wheelRouter.Dispose();base.Dispose(disposing);}
  void PostUi(Action action){if(IsDisposed||Disposing||!IsHandleCreated)return;try{BeginInvoke(new Action(()=>{if(!IsDisposed&&!Disposing)action();}));}catch(InvalidOperationException){}}
  void Go(string target){if(busy||user==null)return;int index=nav.Pages.IndexOf(target);if(index>=0)nav.SelectedIndex=index;}
  void RefreshShell(){nav.ShopName=shop==null?"Minha padaria":shop.name;nav.UserName=user==null?"":user.name;nav.Role=user==null?"":RoleName(user.role);header.CashOpen=currentRegister!=null;header.CashText=cashOccupied?"Caixa em uso":currentRegister==null?"Caixa fechado":"Caixa aberto";header.Page=page=="Visão geral"?"Visão geral":page=="Relatórios"?"Relatórios":SideNavigation.Title(page??"");nav.Invalidate();header.Invalidate();}

  string statusText;
  async Task Run(Func<object> work,Action<object> then=null){if(busy){MessageBox.Show(this,"Aguarde a operação terminar.","Pão & Caixa");return;}busy=true;body.Enabled=false;nav.Enabled=false;status.Text="Aguarde…";try{var result=await Task.Run(()=>{if(db==null)return work();lock(db.Sync)return work();});if(!IsDisposed&&then!=null)then(result);}
   catch(Exception ex){
#if UI_QA
Console.Error.WriteLine(ex.ToString());
#endif
MessageBox.Show(this,ex.Message,"Pão & Caixa",MessageBoxButtons.OK,MessageBoxIcon.Information);if(db==null){closingForUpdate=true;Close();return;}if(user!=null){try{lock(db.Sync)db.Current();}catch{user=null;LoginPage();}}}
   finally{busy=false;if(!IsDisposed){body.Enabled=true;nav.Enabled=true;status.Text=user==null?(statusText??"Dados salvos neste computador. Entre para continuar."):user.name+" • "+RoleName(user.role)+" • Dados locais • Versão "+Updates.Version;
    if(pendingRemoteLock)PostUi(()=>ApplyRemoteState());}}
  }
  async void Fire(Func<object> work,Action<object> then=null){await Run(work,then);}
  static string RoleName(string r){return r=="operator"?"Operador":r=="financial"?"Financeiro":"Administrador";}
  void Root(string caption){
   body.SuspendLayout();cartGrid=null;
   foreach(Control c in body.Controls.Cast<Control>().ToArray())c.Dispose();body.Controls.Clear();
   string description=caption=="Pedidos"?"Uma venda de cada vez. Tudo sob controle.":caption=="Visão geral"?"Os números de hoje, sem complicação.":caption=="Produtos"?"Os produtos da sua padaria, com preço e estoque sempre à mão.":caption=="Meu caixa"?"Abra, acompanhe e confira o movimento do seu caixa.":caption=="Vendas"?"Cada venda registrada, do pedido ao pagamento.":caption=="Relatórios"?"Entenda o movimento e os produtos que fazem o dia render.":caption=="Configurações"?"Deixe o sistema do jeito da sua padaria.":caption=="Meu perfil"?"Sua conta e sua senha.":caption=="Painel Biel"?"Contas, permissões e controle da instalação.":"Entre com a conta criada pelo administrador.";
   var h=new PageHeading(SideNavigation.Title(caption),description,caption=="Pedidos"?"TUDO PRONTO PARA ATENDER":null);body.Controls.Add(h);body.ResumeLayout();if(!preparingPage)RefreshShell();
  }
  Panel Content(bool card=true){Panel p=card?(Panel)new CardPanel():new SmoothPanel();p.Dock=DockStyle.Fill;body.Controls.Add(p);p.BringToFront();return p;}
  void ApplyTheme(){Ui.Apply(this);foreach(var field in FindFields(this))field.ThemeChanged();}
  static IEnumerable<FieldBox> FindFields(Control parent){foreach(Control c in parent.Controls){var field=c as FieldBox;if(field!=null)yield return field;foreach(var f in FindFields(c))yield return f;}}
  bool HasOpenDialog(){
   foreach(Form opened in Application.OpenForms){
    if(opened!=this&&!opened.IsDisposed&&opened.Visible&&opened.Modal)return true;
   }
   return false;
  }
  // Todas as mudancas de tela ocorrem na thread do Windows Forms.
  void ApplyRemoteState(){
   if(IsDisposed||initializing||startupRunning)return;
   if(!remote.Denied){
    pendingRemoteLock=false;
    if(remoteLockedScreen){remoteLockedScreen=false;LoginPage();}
    return;
   }
   if(busy||HasOpenDialog()){
    pendingRemoteLock=true;
    status.Text="Acesso bloqueado. Finalize e feche a janela aberta.";
    return;
   }
   pendingRemoteLock=false;
   if(user!=null){
    draftTimer.Stop();
    // Preserve edits made while the network request was in progress, before replacing the screen.
    if(user.role!="financial"){try{lock(db.Sync)db.PreserveDraftOnRemoteLock(Snapshot());}catch{pendingRemoteLock=true;status.Text="Acesso suspenso. Não foi possível guardar o pedido; ele permanece nesta tela. Confira o espaço em disco.";return;}}
    try{lock(db.Sync)db.Logout();}catch{}
    user=null;
   }
   if(!remoteLockedScreen)RemoteLockedPage();
  }
  void RemoteLockedPage(){
   remoteLockedScreen=true;
   nav.Visible=false;Root("Acesso bloqueado");header.Page="Acesso bloqueado";header.Invalidate();
   var p=Content(false);var box=new CardPanel{Size=new Size(550,280),Padding=new Padding(26)};p.Controls.Add(box);
   Action arrange=()=>box.Location=new Point(Math.Max(0,(p.ClientSize.Width-box.Width)/2),Math.Max(10,(p.ClientSize.Height-box.Height)/2));p.SizeChanged+=(sender,e)=>arrange();arrange();
   var title=Ui.Label("Acesso bloqueado",17);title.SetBounds(22,20,500,42);box.Controls.Add(title);
   var reason=Ui.Label("Entre em contato com o responsável pelo sistema.",10);reason.SetBounds(22,70,505,46);box.Controls.Add(reason);
   var number=Ui.Label("Identificador deste computador:",9);number.SetBounds(22,125,480,24);box.Controls.Add(number);
   var id=Ui.Label(remote.InstallationId,10);id.SetBounds(22,148,505,26);box.Controls.Add(id);
   var tryAgain=Ui.Primary("Verificar liberação",()=>RefreshRemote(true),"check");tryAgain.SetBounds(22,209,495,45);box.Controls.Add(tryAgain);
   status.Text="Acesso bloqueado • Seus dados permanecem neste computador";ApplyTheme();
  }
  async void RefreshRemote(bool showFeedback=false){
   if(remoteChecking||IsDisposed||!remote.Enabled)return;
   remoteChecking=true;
   try{
    Draft pending=null;
    // O rascunho normalmente ja e salvo pelo draftTimer (450 ms).
    // Salva aqui apenas quando houver edicoes ainda pendentes.
    if(user!=null&&!busy&&user.role!="financial"&&draftTimer.Enabled)pending=Snapshot();
    await Task.Run(()=>{
     if(pending!=null){try{lock(db.Sync)db.SetDraft(pending);}catch{}}
     remote.Check();
    });
    if(IsDisposed)return;
    // Atualiza o estado sem reiniciar e sem recriar a tela a cada consulta.
    ApplyRemoteState();
    if(showFeedback&&!IsDisposed)MessageBox.Show(this,
      (remote.Denied?RemoteControl.AccessBlockedMessage:"Acesso liberado."),
      "Acesso ao sistema",MessageBoxButtons.OK,
      remote.Denied?MessageBoxIcon.Warning:MessageBoxIcon.Information);
   }catch(Exception){if(!IsDisposed)ApplyRemoteState();}finally{remoteChecking=false;}
  }
  async void DiagnoseRemote(){
   if(remoteChecking||busy){MessageBox.Show(this,"Aguarde a operacao atual.","Cloudflare");return;}
   remoteChecking=true;
   try{await Task.Run(()=>remote.Check());
    if(!IsDisposed)MessageBox.Show(this,remote.LastCheckStatus+"\n\nO resultado tambem foi salvo em:\n"+
      System.IO.Path.Combine(dataDir,"remote-control-diagnostico.txt"),
      "Diagnostico Cloudflare",MessageBoxButtons.OK,
      remote.LastCheckStatus.StartsWith("Conectado")?MessageBoxIcon.Information:MessageBoxIcon.Warning);
    if(!IsDisposed)ApplyRemoteState();
   }catch(Exception ex){if(!IsDisposed)MessageBox.Show(this,ex.Message,"Falha Cloudflare");}
   finally{remoteChecking=false;}
  }
  void LoginPage(){
   if(remote.Denied){ApplyRemoteState();return;}
   remoteLockedScreen=false;pendingRemoteLock=false;
   nav.Visible=false;header.Page="Entrar";Root("Bem-vindo ao Pão & Caixa");var p=Content(false);
   var box=new CardPanel{Size=new Size(440,371),Padding=new Padding(27)};p.Controls.Add(box);
   Action arrange=()=>box.Location=new Point(Math.Max(0,(p.ClientSize.Width-box.Width)/2),Math.Max(10,(p.ClientSize.Height-box.Height)/2-12));p.SizeChanged+=(sender,e)=>arrange();arrange();
   var intro=Ui.Label("Acesse sua padaria",17);intro.SetBounds(26,20,380,35);box.Controls.Add(intro);var notice=Ui.Caption("Tudo salvo neste computador, mesmo sem internet.");notice.SetBounds(27,64,380,24);box.Controls.Add(notice);
   var emailLabel=Ui.Label("E-mail");emailLabel.SetBounds(27,110,370,25);box.Controls.Add(emailLabel);var email=new TextBox{MaxLength=254};var emailBox=new FieldBox(email,"Seu e-mail");emailBox.SetBounds(27,140,386,44);box.Controls.Add(emailBox);
   var passLabel=Ui.Label("Senha");passLabel.SetBounds(27,199,370,25);box.Controls.Add(passLabel);var pass=new TextBox{UseSystemPasswordChar=true,MaxLength=128};var passBox=new FieldBox(pass,"Sua senha","lock");passBox.SetBounds(27,229,386,44);box.Controls.Add(passBox);
   var enter=Ui.Primary("Entrar",()=>{string emailValue=email.Text,password=pass.Text;pass.Clear();Fire(()=>db.Login(emailValue,password),x=>{user=(User)x;StartSession();});},"arrow");enter.AutoSize=false;enter.SetBounds(27,300,386,45);box.Controls.Add(enter);AcceptButton=enter;ApplyTheme();email.Focus();
  }
  void StartSession(){
   remoteLockedScreen=false;
   registerOffset=cashOffset=0;initializing=true;var pages=user.role=="operator"?new[]{"Pedidos","Meu caixa","Vendas","Meu perfil"}:user.role=="financial"?new[]{"Visão geral","Relatórios","Vendas","Meu perfil"}:new[]{"Pedidos","Visão geral","Produtos","Meu caixa","Vendas","Relatórios","Configurações","Meu perfil"};
   nav.Reset(user.Owner?pages.Concat(new[]{"Painel Biel"}):pages);nav.Visible=true;AcceptButton=null;initializing=false;
   BeginInvoke(new Action(()=>Fire(()=>{ReadPresentation();return null;},x=>LoadPage())));
  }
  void ReadPresentation(){shop=db.Shop();products=db.Products();draft=db.GetDraft();lastSavedDraftJson=J.Write(draft);currentRegister=db.Open();cashOccupied=db.Occupied();nextNumber=db.NextNumber();}

  void Logout(){if(user==null)return;FlushDraftThen(()=>{lock(db.Sync)db.Logout();user=null;products.Clear();draft=new Draft();LoginPage();});}
  Draft Snapshot(){return J.Read<Draft>(J.Write(draft));}
  void SaveDraft(Action next){
    if(user==null||user.role=="financial"){if(next!=null)next();return;}
    var d=Snapshot();
    string json=J.Write(d);
    // Nao grava e nao desativa a interface em toda navegacao sem mudancas.
    if(json==lastSavedDraftJson){if(next!=null)next();return;}
    Fire(()=>{db.SetDraft(d);return null;},x=>{
     lastSavedDraftJson=json;
     if(next!=null)next();
    });
   }
  void FlushDraftThen(Action next){draftTimer.Stop();if(user!=null&&user.role!="financial")SaveDraft(next);else next();}
  // Prepara e configura a nova pagina fora da arvore visual. A anterior nao e desmontada.
  // Libera referencias a controles da venda quando a tela deixa de existir.
  // Os dados do pedido continuam no Draft e no banco SQLite.
  void ReleaseOrderViewReferences(){
   catalogue=null;cartView=null;categories=null;search=null;discount=null;note=null;
   total=null;subtotal=null;catalogCaption=null;cartHeading=null;receive=null;
   cartGrid=null;
  }
  const int WM_SETREDRAW=0x000B;
  const uint RDW_INVALIDATE=0x0001,RDW_ERASE=0x0004,RDW_ALLCHILDREN=0x0080,RDW_UPDATENOW=0x0100;
  [System.Runtime.InteropServices.DllImport("user32.dll")]
  static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
  [System.Runtime.InteropServices.DllImport("user32.dll")]
  [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
  static extern bool RedrawWindow(IntPtr hWnd,IntPtr rect,IntPtr region,uint flags);
  // Criar cada HWND nativo enquanto a página está posicionada fora da área visível.
  // Uma árvore destacada não recebe todos os handles de DataGridView, TextBox etc.
  static void CreatePageHandles(Control control){
   if(control.IsDisposed||!control.Visible)return;
   control.CreateControl();
   control.PerformLayout();
   foreach(Control child in control.Controls)CreatePageHandles(child);
  }
  void SwapPage(Panel previous,Panel next){
   // Apenas durante a preparação, não alteramos a página antiga.
   int index=shell.Controls.GetChildIndex(previous);
   next.Dock=DockStyle.None;
   next.SetBounds(shell.ClientSize.Width+64,0,Math.Max(1,previous.Width),Math.Max(1,previous.Height));
   shell.Controls.Add(next);
   try{
    CreatePageHandles(next);
   }catch{
    shell.Controls.Remove(next);
    throw;
   }
   bool suspended=false;
   try{
    // Evita quadros intermediários enquanto reordena os HWND já criados.
    if(shell.IsHandleCreated){SendMessage(shell.Handle,WM_SETREDRAW,IntPtr.Zero,IntPtr.Zero);suspended=true;}
    shell.SuspendLayout();
    try{
     next.Dock=DockStyle.Fill;
     shell.Controls.SetChildIndex(next,index);
     shell.Controls.Remove(previous);
    }finally{shell.ResumeLayout(true);}
   }finally{
    if(suspended&&!shell.IsDisposed){
     SendMessage(shell.Handle,WM_SETREDRAW,new IntPtr(1),IntPtr.Zero);
     RedrawWindow(shell.Handle,IntPtr.Zero,IntPtr.Zero,RDW_INVALIDATE|RDW_ERASE|RDW_ALLCHILDREN|RDW_UPDATENOW);
    }
   }
   previous.Dispose();
  }
  async void LoadPage(){
   if(user==null)return;
   int serial=++pageLoadSerial;
   string wanted=nav.SelectedPage;
   // Concluir as consultas antes da troca; respostas antigas sao descartadas pelo serial.
   if(wanted=="Painel Biel"||wanted=="Meu caixa"||wanted=="Visão geral"||wanted=="Relatórios"){
    status.Text="Carregando "+SideNavigation.Title(wanted)+"…";
    object loaded=null;
    try{
     DateTime today=DateTime.Today;
     int movesPage=cashOffset,registerPage=registerOffset;
     loaded=await Task.Run(()=>{
      lock(db.Sync){
       if(wanted=="Painel Biel")return (object)new OwnerView{users=db.Users(),control=db.Control(),audit=db.AuditList()};
       if(wanted=="Meu caixa"){
        var register=db.Open();
        return (object)new CashView{
         open=register,occupied=db.Occupied(),totals=register==null?null:db.SessionTotals(register.id),
         moves=register==null?new List<CashMove>():db.CashMoves(register.id,movesPage),
         history=db.Registers(registerPage)
        };
       }
       DateTime from=wanted=="Visão geral"?today:new DateTime(today.Year,today.Month,1);
       DateTime to=today.AddDays(1);
       return (object)new ReportView{
        result=ReportResult.Build(db.Report(from,to)),
        recent=wanted=="Visão geral"?db.Sales(from,to,0,6):new List<Sale>()
       };
      }
     });
    }catch(Exception ex){
     if(!IsDisposed&&serial==pageLoadSerial&&user!=null){
      status.Text=user.name+" • "+RoleName(user.role)+" • Dados locais • Versão "+Updates.Version;
      MessageBox.Show(this,ex.Message,"Não foi possível carregar "+SideNavigation.Title(wanted));
     }
     return;
    }
    if(IsDisposed||user==null||serial!=pageLoadSerial||nav.SelectedPage!=wanted)return;
    ownerViewReady=loaded as OwnerView;
    cashViewReady=loaded as CashView;
    reportViewReady=loaded as ReportView;
    status.Text=user.name+" • "+RoleName(user.role)+" • Dados locais • Versão "+Updates.Version;
   }
   var previous=body;
   var next=new Panel{Dock=DockStyle.Fill,Padding=new Padding(28),Size=previous.Size};
   string previousPage=page;
   body=next;
   preparingPage=true;
   try{
    page=wanted;Root(page);
    switch(page){
     case "Pedidos":BuildOrder();break;
     case "Meu caixa":BuildCash();break;
     case "Vendas":BuildSales();break;
     case "Produtos":BuildProducts(false);break;
     case "Estoque":BuildProducts(true);break;
     case "Relatórios":BuildReport(false);break;
     case "Visão geral":BuildReport(true);break;
     case "Configurações":BuildSettings();break;
     case "Meu perfil":BuildProfile();break;
     case "Painel Biel":BuildOwner();break;
    }
    Ui.Apply(next);
    SwapPage(previous,next);
    preparingPage=false;
    RefreshShell();
    // Controles descartados nao podem continuar vivos via campos do MainForm.
    if(wanted!="Pedidos")ReleaseOrderViewReferences();
    if(wanted!="Configurações")updateLabel=null;
    ownerViewReady=null;cashViewReady=null;reportViewReady=null;
   }catch(Exception ex){
    preparingPage=false;
    if(!next.IsDisposed)next.Dispose();
    if(!previous.IsDisposed&&previous.Parent==shell)body=previous;
    page=previousPage;ownerViewReady=null;cashViewReady=null;reportViewReady=null;
    if(!IsDisposed){RefreshShell();MessageBox.Show(this,ex.Message,"Não foi possível abrir o painel");}
   }
  }
  void Reload(){PostUi(()=>{if(user!=null)Fire(()=>{ReadPresentation();return null;},x=>LoadPage());});}
  void BuildOrder(){
   var p=Content(false);p.AutoScroll=false;var left=new Panel();var right=new CardPanel{Padding=new Padding(0)};p.Controls.Add(left);p.Controls.Add(right);
   var orderTitle=body.Controls.OfType<PageHeading>().FirstOrDefault();Action footerLayout=null;Action arrange=()=>{if(orderTitle!=null)orderTitle.Height=ClientSize.Height<680?68:96;int w=p.ClientSize.Width,rw=Math.Min(351,Math.Max(270,(int)(w*.39))),h=p.ClientSize.Height;left.SetBounds(0,0,Math.Max(100,w-rw-20),h);right.SetBounds(w-rw,0,rw,h);if(cartHeading!=null&&!cartHeading.IsDisposed)cartHeading.Height=h<360?52:h<460?64:81;if(footerLayout!=null)footerLayout();};p.SizeChanged+=(sender,e)=>arrange();
   search=new TextBox{MaxLength=200};var searchBox=new FieldBox(search,"Buscar produto ou ler código de barras…","search"){Dock=DockStyle.Top,Height=49};
   categories=new CategoryTabs(new[]{"Todos","Pães","Salgados","Doces","Lanches","Bebidas"}.Concat(products.Where(x=>x.active).Select(x=>x.category)).Distinct());categories.Changed+=()=>FillCatalog();
   catalogCaption=Ui.Caption("");catalogCaption.Dock=DockStyle.Top;catalogCaption.AutoSize=false;catalogCaption.Height=36;
   catalogue=new ProductCatalog();catalogue.Chosen+=product=>GuardUi(()=>AddProduct(product));var tip=Ui.Caption("Leitor de código de barras? Clique na busca e passe o produto.");tip.Dock=DockStyle.Bottom;tip.AutoSize=false;tip.Height=40;
   left.Controls.Add(catalogue);left.Controls.Add(tip);left.Controls.Add(catalogCaption);left.Controls.Add(categories);left.Controls.Add(searchBox);
   search.TextChanged+=(sender,e)=>FillCatalog();search.KeyDown+=(sender,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;GuardUi(()=>{var exact=products.Find(x=>x.active&&string.Equals(x.code,search.Text.Trim(),StringComparison.OrdinalIgnoreCase));if(exact!=null)AddProduct(exact);else AddSelected(false);});}};
   cartHeading=new SectionHeading("Venda atual","#"+nextNumber.ToString("0000")+" · "+user.name);cartGrid=Ui.Grid("Produto","Qtd.","Total");cartView=new CartList{Draft=draft,Products=products};
   cartView.Action=(index,action)=>GuardUi(()=>{if(index>=cartGrid.Rows.Count)return;cartGrid.CurrentCell=cartGrid.Rows[index].Cells[0];if(action=="remove")RemoveLine();else if(action=="edit")EditLine();else ChangeQuantity(index,action=="plus"?1:-1);});
   var footer=new Panel{Dock=DockStyle.Bottom,Height=294,Tag="card"};var clear=Ui.Link("Limpar venda",()=>{if(MessageBox.Show(this,"Limpar os itens desta venda?","Pedido",MessageBoxButtons.YesNo)==DialogResult.Yes){draft=new Draft();RefreshCart();QueueDraft();}},"trash");clear.AutoSize=false;clear.MinimumSize=new Size(100,25);
   var subLabel=Ui.Caption("Subtotal");subtotal=Ui.Label("",10);subtotal.AutoSize=false;subtotal.TextAlign=ContentAlignment.MiddleRight;
   var discLabel=Ui.Caption("Desconto (R$)");discount=new TextBox{Text=draft.discount,MaxLength=50};var discountBox=new FieldBox(discount,"0,00");
   note=new TextBox{Text=draft.note,MaxLength=200};var noteBox=new FieldBox(note,"Observação da venda (opcional)");
   var totalLabel=Ui.Caption("Total da venda");total=Ui.Label("",22);total.AutoSize=false;total.TextAlign=ContentAlignment.MiddleRight;
   receive=Ui.Primary("Receber pagamento   F4",()=>Checkout(),"arrow");receive.AutoSize=false;var caption=Ui.Caption("Pagamento conferido por você");caption.AutoSize=false;caption.TextAlign=ContentAlignment.MiddleCenter;
   footer.Controls.AddRange(new Control[]{clear,subLabel,subtotal,discLabel,discountBox,noteBox,totalLabel,total,receive,caption});
   footerLayout=()=>{bool compact=right.Height<460;int w=footer.ClientSize.Width-36;footer.Height=compact?226:294;clear.SetBounds(14,compact?0:2,145,26);subLabel.SetBounds(18,compact?26:38,w/2,23);subtotal.SetBounds(18+w/2,compact?24:36,w/2,27);discLabel.SetBounds(18,compact?55:75,w-104,25);discountBox.SetBounds(footer.Width-114,compact?49:68,96,compact?34:38);noteBox.SetBounds(18,compact?90:119,w,compact?35:39);totalLabel.SetBounds(18,compact?129:173,compact?86:103,29);total.SetBounds(compact?108:126,compact?126:166,footer.Width-(compact?126:144),compact?35:43);FitTotal();receive.SetBounds(18,compact?166:218,w,compact?40:45);caption.SetBounds(18,compact?207:268,w,18);};footer.SizeChanged+=(sender,e)=>footerLayout();
   right.Controls.Add(cartView);right.Controls.Add(footer);right.Controls.Add(cartHeading);
   discount.TextChanged+=(sender,e)=>{if(!filling){draft.discount=discount.Text;RefreshTotal();QueueDraft();}};note.TextChanged+=(sender,e)=>{if(!filling){draft.note=note.Text;QueueDraft();}};
   arrange();footerLayout();FillCatalog();RefreshCart();BeginInvoke(new Action(()=>{if(page=="Pedidos"&&search!=null&&!search.IsDisposed)search.Focus();}));
  }
  void ChangeQuantity(int index,int direction){var line=draft.cart[index];var product=products.Find(x=>x.id==line.productId);if(product==null)return;long q=line.quantity+direction*(product.unit=="kg"?50:1000);if(q<=0)draft.cart.RemoveAt(index);else{D.Calculate(products,new List<Line>{new Line{productId=product.id,quantity=q}},0);line.quantity=q;}RefreshCart();QueueDraft();}

  protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if((keyData==Keys.F4||keyData==Keys.F12)&&page=="Pedidos"&&!busy){GuardUi(()=>Checkout());return true;}if(keyData==Keys.F2&&user!=null&&user.role!="financial"){if(page!="Pedidos")Go("Pedidos");else if(search!=null)search.Focus();return true;}return base.ProcessCmdKey(ref msg,keyData);}
  void FillCatalog(){if(catalogue==null||catalogue.IsDisposed)return;string q=search.Text.Trim(),category=categories.Selected;var rows=products.Where(product=>product.active&&(category=="Todos"||product.category==category)&&(product.name.IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0||product.code.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0)).Take(300).ToList();catalogue.Bind(rows,draft);catalogCaption.Text=(category=="Todos"?"Todos os produtos":category)+"   ·   "+rows.Count+" disponíveis";}

  Product SelectedProduct(DataGridView g){return g.CurrentRow==null?null:g.CurrentRow.Tag as Product;}
  void AddSelected(bool unused){var p=catalogue.SelectedProduct;if(p!=null)AddProduct(p);}
  void AddProduct(Product p){long q=1000;if(p.unit=="kg"){using(var f=new Fields("Peso de "+p.name)){f.ClientSize=new Size(450,150);var t=f.TextField("Quantidade em kg","0,100");if(Ui.Show(this,f)!=DialogResult.OK)return;q=D.Parse(t.Text,1000);}}var line=draft.cart.Find(x=>x.productId==p.id);long next=q+(line==null?0:line.quantity);D.Calculate(products,new List<Line>{new Line{productId=p.id,quantity=next}},0);if(line==null)draft.cart.Add(new Line{productId=p.id,quantity=q});else line.quantity=next;RefreshCart();QueueDraft();search.Clear();search.Focus();}
  void EditLine(){if(cartGrid.CurrentRow==null)return;var line=cartGrid.CurrentRow.Tag as Line;if(line==null)return;var p=products.Find(x=>x.id==line.productId);if(p==null)return;using(var f=new Fields("Quantidade")){f.ClientSize=new Size(450,150);var t=f.TextField("Quantidade ("+p.unit+")",D.Qty(line.quantity,p.unit));if(Ui.Show(this,f)!=DialogResult.OK)return;long q=D.Parse(t.Text,1000);D.Calculate(products,new List<Line>{new Line{productId=p.id,quantity=q}},0);line.quantity=q;RefreshCart();QueueDraft();}}
  void RemoveLine(){if(cartGrid.CurrentRow==null)return;var l=cartGrid.CurrentRow.Tag as Line;if(l!=null)draft.cart.Remove(l);RefreshCart();QueueDraft();}
  void RefreshCart(){if(cartGrid==null||cartGrid.IsDisposed)return;filling=true;cartGrid.Rows.Clear();foreach(var l in draft.cart){var p=products.Find(x=>x.id==l.productId);int n=cartGrid.Rows.Add(p==null?"Produto indisponível":p.name,p==null?l.quantity.ToString():D.Qty(l.quantity,p.unit)+" "+p.unit,p==null?"—":D.Money(D.Rounded(p.price,l.quantity)));cartGrid.Rows[n].Tag=l;}discount.Text=draft.discount;note.Text=draft.note;filling=false;if(cartView!=null&&!cartView.IsDisposed){cartView.Draft=draft;cartView.Products=products;cartView.RefreshItems();}if(cartHeading!=null&&!cartHeading.IsDisposed){cartHeading.Title="Venda atual";cartHeading.Description="#"+nextNumber.ToString("0000")+" · "+user.name+" · "+draft.cart.Count+" "+(draft.cart.Count==1?"item":"itens");cartHeading.Invalidate();}if(catalogue!=null&&!catalogue.IsDisposed){catalogue.Draft=draft;catalogue.Invalidate();}RefreshTotal();}
  long Discount(){return string.IsNullOrWhiteSpace(draft.discount)?0:D.Parse(draft.discount,100);}
  void RefreshTotal(){try{var c=D.Calculate(products,draft.cart,Discount());total.Text=D.Money(c.total);if(subtotal!=null)subtotal.Text=D.Money(c.subtotal);if(receive!=null)receive.Enabled=draft.cart.Count>0;}catch(Exception ex){total.Text=ex.Message;if(receive!=null)receive.Enabled=false;}FitTotal();}
  void FitTotal(){if(total==null||total.IsDisposed||total.Width<20)return;float size=cartView!=null&&cartView.Parent!=null&&cartView.Parent.Height<460?20:22;for(;size>12;size--){using(var font=Ui.Font(size,FontStyle.Bold))if(TextRenderer.MeasureText(total.Text,font).Width<=total.Width-4)break;}if(total.Font.Size!=size){var old=total.Font;total.Font=Ui.Font(size,FontStyle.Bold);old.Dispose();}}
  void QueueDraft(){draftTimer.Stop();draftTimer.Start();}
  void Checkout(){draftTimer.Stop();var c=D.Calculate(products,draft.cart,Discount());if(c.items.Count==0)throw new Exception("Adicione um produto ao pedido.");if(c.total<=0)throw new Exception("O total deve ser maior que zero.");using(var f=new Fields("Pagamento • "+D.Money(c.total))){var values=new List<TextBox>();for(int i=0;i<4;i++)values.Add(f.TextField(D.MethodNames[i]+" (R$)",i==0?(c.total/100m).ToString("0.00",D.BR):"0,00"));var received=f.TextField("Dinheiro recebido",(c.total/100m).ToString("0.00",D.BR));f.Add("Troco",Ui.Label("Calculado ao confirmar"));if(Ui.Show(this,f)!=DialogResult.OK){QueueDraft();return;}var pays=new List<Payment>();for(int i=0;i<4;i++){long n=D.Parse(values[i].Text,100);if(n>0)pays.Add(new Payment{method=D.Methods[i],amount=n});}long cash=pays.Where(x=>x.method=="dinheiro").Sum(x=>x.amount);long recv=cash==0?0:D.Parse(received.Text,100);var snapshot=Snapshot();Fire(()=>{db.SetDraft(snapshot);return db.Finish(snapshot.cart,Discount(),pays,recv,snapshot.note);},x=>{var sale=(Sale)x;draft=new Draft();MessageBox.Show(this,"Venda #"+sale.number+" concluída.\nTroco: "+D.Money(sale.change),"Pagamento registrado");Reload();});}}
  void OpenCash(){using(var f=new Fields("Abrir caixa")){f.ClientSize=new Size(470,180);var amount=f.TextField("Fundo de troco (R$)","0,00");f.Add("Operador",Ui.Label(user.name));if(Ui.Show(this,f)!=DialogResult.OK)return;long n=D.Parse(amount.Text,100);Fire(()=>{db.OpenCash(n);return db.Open();},x=>{currentRegister=(Register)x;cashOccupied=false;RefreshShell();if(page=="Meu caixa")LoadPage();else MessageBox.Show(this,"Caixa aberto. Os itens do pedido foram mantidos.","Caixa");});}}
  void BuildCash(){BuildClassicCash();}
  class CashView{public Register open;public bool occupied;public Totals totals;public List<CashMove> moves;public List<Register> history;}
  void CashMovement(string kind){using(var f=new Fields(kind=="supply"?"Suprimento":"Sangria")){f.ClientSize=new Size(470,200);var amount=f.TextField("Valor (R$)");var reason=f.TextField("Motivo");if(Ui.Show(this,f)!=DialogResult.OK)return;long n=D.Parse(amount.Text,100);string text=reason.Text;Fire(()=>{db.MoveCash(kind,n,text);return null;},x=>LoadPage());}}
  void CloseCash(Totals t){using(var f=new Fields("Conferir e fechar caixa")){var inputs=new List<TextBox>();for(int i=0;i<4;i++)inputs.Add(f.TextField(D.MethodNames[i]+" contado",((i==0?t.drawer:t.payments[D.Methods[i]])/100m).ToString("0.00",D.BR)));f.Add("Dinheiro esperado",Ui.Label(D.Money(t.drawer)));if(Ui.Show(this,f)!=DialogResult.OK)return;var amounts=new Dictionary<string,long>();for(int i=0;i<4;i++)amounts[D.Methods[i]]=D.Parse(inputs[i].Text,100);Fire(()=>{db.CloseCash(amounts);return null;},x=>LoadPage());}}
  static string LocalDate(string s){DateTime d;return DateTime.TryParse(s,out d)?d.ToLocalTime().ToString("dd/MM/yyyy HH:mm"):s;}
  void BuildSales(){var p=Content();var bar=Ui.Flow();var from=new DateField{Value=DateTime.Today};var to=new DateField{Value=DateTime.Today};var fromLabel=Ui.Caption("De");fromLabel.Margin=new Padding(6,14,6,0);var toLabel=Ui.Caption("até");toLabel.Margin=new Padding(6,14,6,0);bar.Controls.Add(fromLabel);bar.Controls.Add(from);bar.Controls.Add(toLabel);bar.Controls.Add(to);var grid=Ui.Grid("Nº","Data","Operador","Total","Pagamento","Situação");int offset=0;PaginationBar pager=null;List<Sale> rows=null;Action fetch=()=>{DateTime a=from.Value.Date,b=to.Value.Date.AddDays(1);if(b<=a)throw new Exception("Confira o período.");Fire(()=>db.Sales(a,b,offset),x=>{rows=(List<Sale>)x;grid.Rows.Clear();foreach(var v in rows.Take(100)){int n=grid.Rows.Add(v.number,LocalDate(v.at),v.operatorName,D.Money(v.total),string.Join(" + ",v.payments.Select(z=>D.MethodNames[Array.IndexOf(D.Methods,z.method)])),v.status=="paid"?"Concluída":"Cancelada");grid.Rows[n].Tag=v;}pager.PageText="Página "+(offset/100+1);pager.Previous.Enabled=offset>0;pager.Next.Enabled=rows.Count>100;ApplyTheme();});};bar.Controls.Add(Ui.Button("Consultar",()=>{offset=0;fetch();}));pager=new PaginationBar(()=>{offset=Math.Max(0,offset-100);fetch();},()=>{if(rows!=null&&rows.Count>100){offset+=100;fetch();}});
   var bottom=Ui.Flow();bottom.Dock=DockStyle.Bottom;bottom.Controls.Add(Ui.Button("Ver / imprimir comprovante",()=>{if(grid.CurrentRow!=null)Receipt((Sale)grid.CurrentRow.Tag);}));if(user.role!="financial")bottom.Controls.Add(Ui.Button("Cancelar venda",()=>{if(grid.CurrentRow==null)return;var v=(Sale)grid.CurrentRow.Tag;using(var f=new Fields("Cancelar venda #"+v.number)){f.ClientSize=new Size(460,170);var why=f.TextField("Motivo");if(Ui.Show(this,f)!=DialogResult.OK)return;string reason=why.Text;Fire(()=>{db.Cancel(v.id,reason);products=db.Products();return null;},x=>{BeginInvoke(fetch);});}}));p.Controls.Add(grid);p.Controls.Add(bottom);p.Controls.Add(pager);p.Controls.Add(bar);BeginInvoke(fetch);}
  void Receipt(Sale v){var b=new StringBuilder();b.AppendLine(shop.name);b.AppendLine("Comprovante de venda #"+v.number);b.AppendLine(LocalDate(v.at)+" • "+v.operatorName);b.AppendLine("Situação: "+(v.status=="paid"?"Concluída":"Cancelada"));b.AppendLine();foreach(var i in v.items){b.AppendLine(i.name);b.AppendLine(D.Qty(i.quantity,i.unit)+" "+i.unit+" x "+D.Money(i.price)+" = "+D.Money(i.total));}b.AppendLine();b.AppendLine("Subtotal: "+D.Money(v.subtotal));b.AppendLine("Desconto: "+D.Money(v.discount));b.AppendLine("TOTAL: "+D.Money(v.total));foreach(var x in v.payments)b.AppendLine(D.MethodNames[Array.IndexOf(D.Methods,x.method)]+": "+D.Money(x.amount));b.AppendLine("Recebido: "+D.Money(v.cashReceived));b.AppendLine("Troco: "+D.Money(v.change));if(!string.IsNullOrWhiteSpace(v.note))b.AppendLine(v.note);b.AppendLine();b.AppendLine(shop.footer);string receipt=b.ToString();using(var f=new Form{Text="Comprovante #"+v.number,Size=new Size(530,650),StartPosition=FormStartPosition.CenterParent,Font=Font}){var t=new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,Text=receipt,Font=new Font("Consolas",10)};var bar=Ui.Flow();bar.Dock=DockStyle.Bottom;bar.Controls.Add(Ui.Button("Imprimir",()=>{using(var doc=new PrintDocument()){var lines=receipt.Split(new char[] { '\n' });int line=0;doc.BeginPrint+=(s,e)=>line=0;doc.PrintPage+=(s,e)=>{using(var font=new Font("Consolas",10)){float y=e.MarginBounds.Top;while(line<lines.Length){string text=lines[line].TrimEnd(new char[] { '\r' });float height=Math.Max(font.GetHeight(e.Graphics),e.Graphics.MeasureString(text,font,e.MarginBounds.Width).Height);if(y+height>e.MarginBounds.Bottom)break;e.Graphics.DrawString(text,font,Brushes.Black,new RectangleF(e.MarginBounds.Left,y,e.MarginBounds.Width,height));y+=height;line++;}e.HasMorePages=line<lines.Length;}};using(var pd=new PrintDialog{Document=doc,UseEXDialog=true})if(pd.ShowDialog(f)==DialogResult.OK)doc.Print();}}));bar.Controls.Add(Ui.Button("Guardar TXT",()=>{using(var s=new SaveFileDialog{Filter="Texto|*.txt",FileName="Venda-"+v.number+".txt"})if(s.ShowDialog(f)==DialogResult.OK)File.WriteAllText(s.FileName,receipt,Encoding.UTF8);}));f.Controls.Add(t);f.Controls.Add(bar);Ui.Show(this,f);}}
  void BuildProducts(bool stock){var p=Content();var bar=Ui.Flow();var find=new TextBox{Width=220,Margin=new Padding(4,9,4,4)};bar.Controls.Add(Ui.Label("Buscar:"));bar.Controls.Add(find);var grid=Ui.Grid("Código","Produto","Categoria","Unidade","Preço","Estoque","Mínimo","Ativo");Action fill=()=>{grid.Rows.Clear();string q=find.Text.Trim();foreach(var x in products.Where(x=>x.name.IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0||x.code.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0).Take(300)){int n=grid.Rows.Add(x.code,x.name,x.category,x.unit,D.Money(x.price),D.Qty(x.stock,x.unit),D.Qty(x.minStock,x.unit),x.active?"Sim":"Não");grid.Rows[n].Tag=x;}};find.TextChanged+=(s,e)=>fill();if(!stock){bar.Controls.Add(Ui.Primary("Novo produto",()=>EditProduct(null),"plus"));bar.Controls.Add(Ui.Button("Editar produto",()=>{var x=SelectedProduct(grid);if(x!=null)EditProduct(x);}));}bar.Controls.Add(Ui.Button("Ajustar estoque",()=>{var x=SelectedProduct(grid);if(x!=null)Adjust(x);}));bar.Controls.Add(Ui.Button("Ver movimentações",()=>{var x=SelectedProduct(grid);if(x!=null)StockLedger(x);}));var info=Ui.Label("Até 300 produtos por busca. Use nome ou código para localizar.");info.Dock=DockStyle.Bottom;p.Controls.Add(grid);p.Controls.Add(info);p.Controls.Add(bar);fill();}
  void EditProduct(Product old){using(var f=new Fields(old==null?"Novo produto":"Editar produto")){f.ClientSize=new Size(540,545);var name=f.TextField("Nome",old==null?"":old.name);var code=f.TextField("Código / barras",old==null?"":old.code);var category=f.TextField("Categoria",old==null?"":old.category);var unit=f.Choice("Unidade",new[]{"un","kg"},old!=null&&old.unit=="kg"?1:0);unit.Enabled=old==null;var price=f.TextField("Preço (R$)",old==null?"":(old.price/100m).ToString("0.00",D.BR));var cost=f.TextField("Custo (R$)",old==null?"0,00":(old.cost/100m).ToString("0.00",D.BR));var quantity=f.TextField("Estoque inicial",old==null?"0":D.Qty(old.stock,old.unit));quantity.Enabled=old==null;var min=f.TextField("Estoque mínimo",old==null?"0":D.Qty(old.minStock,old.unit));var active=f.Check("Produto ativo",old==null||old.active);if(Ui.Show(this,f)!=DialogResult.OK)return;var x=new Product{id=old==null?null:old.id,name=name.Text,code=code.Text,category=category.Text,unit=unit.SelectedItem.ToString(),price=D.Parse(price.Text,100),cost=D.Parse(cost.Text,100),stock=D.Parse(quantity.Text,1000),minStock=D.Parse(min.Text,1000),active=active.Checked,icon=old==null?"bread":old.icon};Fire(()=>{db.SaveProduct(x);return null;},r=>Reload());}}
  void Adjust(Product p){using(var f=new Fields("Ajustar estoque de "+p.name)){f.ClientSize=new Size(480,210);var q=f.TextField("Entrada (+) / saída (-)","0");var why=f.TextField("Motivo");f.Add("Unidade",Ui.Label(p.unit));if(Ui.Show(this,f)!=DialogResult.OK)return;long n=D.Parse(q.Text,1000,true);string reason=why.Text;Fire(()=>{db.AdjustStock(p.id,n,reason);return null;},x=>Reload());}}
  void StockLedger(Product p){Fire(()=>db.StockHistory(p.id),x=>{using(var f=new Form{Text="Estoque • "+p.name,Size=new Size(750,520),StartPosition=FormStartPosition.CenterParent,Font=Font}){var g=Ui.Grid("Data","Entrada / saída","Motivo");f.Controls.Add(g);int offset=0;Action<List<StockMove>> fill=rows=>{g.Rows.Clear();foreach(var m in rows.Take(100))g.Rows.Add(LocalDate(m.at),D.Qty(m.quantity,p.unit)+" "+p.unit,m.reason);};fill((List<StockMove>)x);var bottom=Ui.Flow();bottom.Dock=DockStyle.Bottom;bottom.Controls.Add(Ui.IconButton("Anterior",()=>{offset=Math.Max(0,offset-100);lock(db.Sync)fill(db.StockHistory(p.id,offset));},"arrow-left"));bottom.Controls.Add(Ui.IconButton("Próxima",()=>{offset+=100;lock(db.Sync)fill(db.StockHistory(p.id,offset));},"arrow",true));f.Controls.Add(bottom);Ui.Show(this,f);}});}
  void BuildReport(bool today){BuildClassicReport(today);}
  void ExportCsv(DateTime from,DateTime to){using(var f=new SaveFileDialog{Filter="Relatório CSV|*.csv",FileName="Vendas-"+from.ToString("yyyy-MM-dd")+".csv"}){if(f.ShowDialog(this)!=DialogResult.OK)return;string path=f.FileName;GuardOutput(path);Fire(()=>{using(var w=new StreamWriter(path,false,new UTF8Encoding(true))){w.Write(ReportResult.CsvHeader);foreach(var v in db.Report(from,to))w.Write(ReportResult.CsvRow(v));}return null;},x=>MessageBox.Show(this,"Relatório CSV salvo.","Relatório"));}}
  void BuildSettings(){BuildClassicSettings();}
  void Backup(){using(var f=new SaveFileDialog{Filter="Banco completo SQLite|*.sqlite",FileName="PaoCaixa-Backup-"+DateTime.Today.ToString("yyyy-MM-dd")+".sqlite"}){if(f.ShowDialog(this)!=DialogResult.OK)return;string target=f.FileName;GuardOutput(target);Fire(()=>{db.Admin();File.Copy(db.Backup(),target,true);return null;},x=>MessageBox.Show(this,"Backup completo salvo, incluindo contas e pedidos em andamento.","Backup"));}}
  void GuardOutput(string target){var p=Path.GetFullPath(target);if(new[]{db.FilePath,db.FilePath+"-wal",db.FilePath+"-shm"}.Any(x=>p.Equals(Path.GetFullPath(x),StringComparison.OrdinalIgnoreCase)))throw new Exception("Escolha outro arquivo. O banco em uso precisa ser preservado.");}
  void BusinessBackup(){using(var f=new SaveFileDialog{Filter="Backup comercial|*.json",FileName="PaoCaixa-Dados-"+DateTime.Today.ToString("yyyy-MM-dd")+".json"}){if(f.ShowDialog(this)!=DialogResult.OK)return;string path=f.FileName;GuardOutput(path);Fire(()=>{File.WriteAllText(path,J.Write(db.Export()),Encoding.UTF8);return null;},x=>MessageBox.Show(this,"Backup comercial salvo. Para guardar contas e senhas, use Backup completo.","Backup"));}}
  void Restore(){using(var f=new OpenFileDialog{Filter="Backup comercial JSON|*.json"}){if(f.ShowDialog(this)!=DialogResult.OK)return;if(MessageBox.Show(this,"Substituir os dados comerciais pelo backup?\nAs contas serão mantidas. Um backup completo será criado antes.","Restaurar dados",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;string path=f.FileName;long revision;lock(db.Sync)revision=db.Revision();Fire(()=>{var state=J.Read<State>(File.ReadAllText(path));db.Restore(state,revision);return null;},x=>Reload());}}
  bool updateDownloading,updateChecking;
  // Um unico comando seguro: procura, baixa, verifica e inicia a instalacao.
  async void RunFullUpdate(){
   if(updateFlowRunning||updateChecking||updateDownloading||busy)return;
   updateFlowRunning=true;
   try{
    lock(db.Sync)db.Admin();
    string url=prefs.url;
    updateChecking=true;updates.Status="Procurando atualização…";UpdateStatus();
    try{await Task.Run(()=>updates.Check(url));}finally{updateChecking=false;}
    if(IsDisposed||Disposing)return;
    if(updates.Available==null){UpdateStatus();return;}
    Feed feed=updates.Available;
    updateDownloading=true;updates.Status="Baixando a versão "+feed.version+"…";UpdateStatus();
    try{
     await Task.Run(()=>updates.Download(url,dataDir,percent=>PostUi(()=>{
      if(updateDownloading&&updateLabel!=null&&!updateLabel.IsDisposed)
       updateLabel.Text="Baixando versão "+feed.version+": "+percent+"%.";
     })));
    }finally{updateDownloading=false;}
    if(IsDisposed||Disposing)return;
    string installer=updates.Downloaded;
    if(string.IsNullOrEmpty(installer))throw new InvalidOperationException("O instalador não foi baixado.");
    updates.Status="Verificando o instalador e protegendo os dados…";UpdateStatus();
    bool ready=false;
    await Run(()=>{db.PrepareInstall();Updates.Verify(installer,feed);return null;},x=>ready=true);
    if(!ready||IsDisposed||Disposing)return;
    updates.Status="Iniciando a instalação da versão "+feed.version+"…";UpdateStatus();
    var start=new ProcessStartInfo(installer,"--updated /D="+AppDomain.CurrentDomain.BaseDirectory.TrimEnd(new char[]{Path.DirectorySeparatorChar})){UseShellExecute=true};
    Process.Start(start);closingForUpdate=true;Close();
   }catch(Exception ex){
    if(!IsDisposed&&!Disposing){updates.Status="Atualização não concluída: "+Updates.Describe(ex);UpdateStatus();}
   }finally{updateChecking=false;updateDownloading=false;updateFlowRunning=false;}
  }

  void UpdateStatus(){if(updateLabel!=null&&!updateLabel.IsDisposed)updateLabel.Text=updates.Status;if(user!=null&&user.role=="admin"&&updates.Available!=null)status.Text="Nova versão "+updates.Available.version+" • Abra Configurações para atualizar • "+user.name;}
  async void CheckUpdate(){if(updateChecking||updateDownloading||updateFlowRunning)return;try{lock(db.Sync)db.Admin();updateChecking=true;updates.Status="Procurando atualização…";UpdateStatus();string url=prefs.url;await Task.Run(()=>updates.Check(url));}catch(Exception ex){updates.Status=Updates.Describe(ex);}finally{updateChecking=false;if(!IsDisposed)UpdateStatus();}}
  async void DownloadUpdate(){if(updateDownloading||updateChecking)return;try{lock(db.Sync)db.Admin();if(updates.Available==null)throw new Exception("Procure uma nova versão antes de baixar.");updateDownloading=true;string url=prefs.url;await Task.Run(()=>updates.Download(url,dataDir,percent=>PostUi(()=>{if(updateDownloading&&updateLabel!=null&&!updateLabel.IsDisposed)updateLabel.Text="Baixando atualização: "+percent+"%. Você pode continuar vendendo.";})));}catch(Exception ex){if(!IsDisposed&&!Disposing)MessageBox.Show(this,ex.Message,"Atualização");}finally{updateDownloading=false;if(!IsDisposed)UpdateStatus();}}
  void InstallUpdate(){if(updateDownloading||updateChecking)throw new Exception("Aguarde a atualização terminar.");if(updates.Downloaded==null||updates.Available==null)throw new Exception("Baixe a atualização antes de instalar.");string exe=updates.Downloaded;Fire(()=>{db.PrepareInstall();Updates.Verify(exe,updates.Available);return null;},x=>{var start=new ProcessStartInfo(exe,"--updated /D="+AppDomain.CurrentDomain.BaseDirectory.TrimEnd(new char[] { Path.DirectorySeparatorChar })){UseShellExecute=true};Process.Start(start);closingForUpdate=true;Close();});}
  void BuildProfile(){var p=Content();var list=Ui.Flow();list.FlowDirection=FlowDirection.TopDown;list.WrapContents=false;list.Controls.Add(Ui.Label(user.name+"\n"+user.email+"\n"+RoleName(user.role),15));list.Controls.Add(Ui.Button("Alterar minha senha",()=>{using(var f=new Fields("Alterar senha")){var current=f.TextField("Senha atual","",true);var next=f.TextField("Nova senha","",true);var confirm=f.TextField("Repita a nova senha","",true);if(Ui.Show(this,f)!=DialogResult.OK)return;if(next.Text!=confirm.Text)throw new Exception("As novas senhas são diferentes.");string a=current.Text,b=next.Text;Fire(()=>{db.ChangePassword(a,b);return null;},x=>MessageBox.Show(this,"Senha alterada.","Conta"));}}));p.Controls.Add(list);}
  void BuildOwner(){var p=Content();var v=ownerViewReady;ownerViewReady=null;if(v==null)throw new InvalidOperationException("Dados do painel indisponíveis.");var bar=Ui.Flow();bar.Controls.Add(Ui.Label("ID remoto: "+remote.InstallationId,9));bar.Controls.Add(Ui.Button("Copiar ID",()=>Clipboard.SetText(remote.InstallationId)));if(remote.Enabled){bar.Controls.Add(Ui.Button("Painel remoto",()=>Process.Start(remote.PanelUrl)));bar.Controls.Add(Ui.Button("Testar Cloudflare",()=>DiagnoseRemote()));}else bar.Controls.Add(Ui.Label("Controle remoto não configurado",9));bar.Controls.Add(Ui.Label(v.control.blocked==1?"Bloqueio local ativo • "+v.control.reason:"Bloqueio local desativado",13));bar.Controls.Add(Ui.Button(v.control.blocked==1?"Desbloquear localmente":"Bloquear localmente",()=>{using(var f=new Fields(v.control.blocked==1?"Desbloquear localmente":"Bloquear localmente")){f.ClientSize=new Size(500,220);var why=f.TextField("Motivo",v.control.reason);f.Add("Sua conta",Ui.Label("Continua com acesso."));if(Ui.Show(this,f)!=DialogResult.OK)return;string reason=why.Text;Fire(()=>{db.Block(v.control.blocked!=1,reason);return null;},r=>LoadPage());}}));bar.Controls.Add(Ui.Button("Criar conta",()=>EditUser(null)));var grid=Ui.Grid("Nome","E-mail","Perfil","Ativa","Proprietário");foreach(var u in v.users){int n=grid.Rows.Add(u.name,u.email,RoleName(u.role),u.active==1?"Sim":"Não",u.Owner?"Protegido":"—");grid.Rows[n].Tag=u;}var bottom=Ui.Flow();bottom.Dock=DockStyle.Bottom;bottom.Controls.Add(Ui.Button("Editar / redefinir senha",()=>{if(grid.CurrentRow!=null)EditUser((User)grid.CurrentRow.Tag);}));bottom.Controls.Add(Ui.Button("Histórico de acesso",()=>{using(var f=new Form{Text="Histórico de acesso",Size=new Size(740,500),StartPosition=FormStartPosition.CenterParent,Font=Font}){var g=Ui.Grid("Data","Ação","Detalhe");foreach(var a in v.audit)g.Rows.Add(LocalDate((string)a["at"]),a["action"],a["detail"]);f.Controls.Add(g);Ui.Show(this,f);}}));p.Controls.Add(grid);p.Controls.Add(bottom);p.Controls.Add(bar);}
  class OwnerView{public List<User> users;public ControlState control;public List<Dictionary<string,object>> audit;}
  void EditUser(User u){if(u!=null&&u.Owner)throw new Exception("Sua conta é protegida. Para alterar a senha, abra Meu perfil.");using(var f=new Fields(u==null?"Criar conta":"Editar conta")){var name=f.TextField("Nome",u==null?"":u.name);var email=f.TextField("E-mail",u==null?"":u.email);email.MaxLength=254;email.Enabled=u==null;var role=f.Choice("Perfil",new[]{"Administrador","Operador","Financeiro"},u==null?1:u.role=="admin"?0:u.role=="operator"?1:2);var active=f.Check("Conta ativa",u==null||u.active==1);var pw=f.TextField(u==null?"Senha":"Nova senha (opcional)","",true);if(Ui.Show(this,f)!=DialogResult.OK)return;string id=u==null?null:u.id,e=email.Text,n=name.Text,r=new[]{"admin","operator","financial"}[role.SelectedIndex],password=pw.Text;bool a=active.Checked;Fire(()=>{db.SaveUser(id,e,n,r,a,password);return null;},x=>LoadPage());}}
 }
}
