using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace PaoCaixa {
 public partial class MainForm {
  CardPanel GridSection(string title,string description,DataGridView grid){var panel=new CardPanel{Padding=new Padding(1)};panel.Controls.Add(grid);panel.Controls.Add(new SectionHeading(title,description));return panel;}
  void BuildClassicReport(bool today){
   var p=Content(false);p.AutoScroll=true;var toolbar=Ui.Flow();toolbar.Dock=DockStyle.None;toolbar.AutoSize=false;toolbar.Height=58;
   var from=new DateField{Value=today?DateTime.Today:new DateTime(DateTime.Today.Year,DateTime.Today.Month,1)};
   var to=new DateField{Value=DateTime.Today};
   if(!today){var a=Ui.Caption("De");a.Margin=new Padding(6,14,6,0);var b=Ui.Caption("até");b.Margin=new Padding(6,14,6,0);toolbar.Controls.Add(a);toolbar.Controls.Add(from);toolbar.Controls.Add(b);toolbar.Controls.Add(to);}
   var cards=new StatsRow(new StatCard(today?"Vendido hoje":"Faturamento","chart"),new StatCard("Margem bruta","up"),new StatCard("Tíquete médio","receipt"){Amber=true},new StatCard(today?"Estoque para revisar":"Média diária","package"){Amber=true});
   var banner=today?new WelcomeBanner(user.name):null;var ranking=new RankingPanel();var payments=new PaymentsPanel();
   var recentGrid=Ui.Grid("Venda","Data e hora","Pagamento","Total","Status");recentGrid.Columns[0].FillWeight=55;recentGrid.Columns[1].FillWeight=130;recentGrid.Columns[2].FillWeight=95;recentGrid.Columns[3].FillWeight=88;recentGrid.Columns[4].FillWeight=90;
   recentGrid.CellDoubleClick+=(sender,e)=>{if(e.RowIndex>=0&&recentGrid.Rows[e.RowIndex].Tag is Sale)Receipt((Sale)recentGrid.Rows[e.RowIndex].Tag);};
   var latest=GridSection("Últimas vendas","O movimento mais recente do balcão.",recentGrid);var stock=new StockAlerts();stock.Bind(products);if(user.role=="admin")stock.Adjust=product=>GuardUi(()=>Adjust(product));var daily=new DailyPanel();
   p.Controls.Add(toolbar);p.Controls.Add(cards);if(banner!=null)p.Controls.Add(banner);
   Control first=today?(Control)latest:ranking,second=today?(Control)stock:payments;p.Controls.Add(first);p.Controls.Add(second);if(!today)p.Controls.Add(daily);
   Action arrange=()=>{
    int w=ResponsiveLayout.ContentWidth(p.ClientSize.Width),x=ResponsiveLayout.ContentLeft(p.ClientSize.Width),y=0;
    int toolbarHeight=Math.Max(58,Math.Min(205,toolbar.GetPreferredSize(new Size(w,0)).Height+8));
    toolbar.SetBounds(x,y,w,toolbarHeight);y+=toolbarHeight;
    if(banner!=null){banner.SetBounds(x,y,w,152);y+=174;}
    int statHeight=StatsRow.HeightFor(w,cards.Cards.Count);
    cards.SetBounds(x,y,w,statHeight);y+=statHeight+24;
    bool two=w>=790;
    int firstWidth=two?(today?(int)(w*.62):(int)(w*.58)):w;
    int secondWidth=two?w-firstWidth-22:w;
    int panelHeight=today?395:460;
    first.SetBounds(x,y,firstWidth,panelHeight);
    second.SetBounds(x+(two?firstWidth+22:0),two?y:y+panelHeight+22,secondWidth,panelHeight);
    y+=two?panelHeight:panelHeight*2+22;
    if(!today){daily.SetBounds(x,y+23,w,260);y+=283;}
    p.AutoScrollMinSize=new Size(0,y+14);
   }p.SizeChanged+=(sender,e)=>arrange();
   Action<ReportView,DateTime,DateTime> showReport=(view,a,b)=>{
    var r=view.result;cards.Cards[0].Value=D.Money(r.revenue);cards.Cards[0].Detail=r.count+" vendas concluídas"+(today?" hoje":" no período");cards.Cards[1].Value=D.Money(r.revenue-r.cost);cards.Cards[1].Detail="Após desconto e custo dos produtos";cards.Cards[2].Value=D.Money(r.count==0?0:(long)Math.Round(r.revenue/(decimal)r.count));cards.Cards[2].Detail="Média por venda concluída";cards.Cards[3].Value=today?products.Count(x=>x.active&&x.stock<=x.minStock).ToString():D.Money((long)Math.Round(r.revenue/(decimal)Math.Max(1,(b-a).TotalDays)));cards.Cards[3].Detail=today?"Produtos no mínimo ou abaixo":"Inclui todos os dias do período";
    foreach(var card in cards.Cards)card.Invalidate();ranking.Bind(r.ranking);payments.Result=r;payments.Invalidate();daily.Bind(r.daily);recentGrid.Rows.Clear();foreach(var sale in view.recent){int row=recentGrid.Rows.Add("#"+sale.number.ToString("0000"),LocalDate(sale.at),string.Join(" + ",sale.payments.Select(x=>D.MethodNames[Array.IndexOf(D.Methods,x.method)])),D.Money(sale.total),sale.status=="paid"?"Concluída":"Cancelada");recentGrid.Rows[row].Tag=sale;}if(view.recent.Count==0&&today)recentGrid.Rows.Add("","Nenhuma venda por aqui.","","","");arrange();
   };
   Action fetch=()=>{
    DateTime a=from.Value.Date,b=to.Value.Date.AddDays(1);
    if(b<=a)throw new Exception("Confira o período.");
    Fire(()=>{
     var result=ReportResult.Build(db.Report(a,b));
     return new ReportView{result=result,recent=today?db.Sales(a,b,0,6):new List<Sale>()};
    },value=>{if(!p.IsDisposed&&page==(today?"Visão geral":"Relatórios"))showReport((ReportView)value,a,b);});
   };
   if(today&&user.role!="financial")toolbar.Controls.Add(Ui.Primary("Nova venda",()=>Go("Pedidos"),"plus"));
   if(!today){toolbar.Controls.Add(Ui.Button("Hoje",()=>{from.Value=to.Value=DateTime.Today;fetch();}));toolbar.Controls.Add(Ui.Button("Este mês",()=>{from.Value=new DateTime(DateTime.Today.Year,DateTime.Today.Month,1);to.Value=DateTime.Today;fetch();}));}
   toolbar.Controls.Add(Ui.Button("Consultar",()=>fetch()));toolbar.Controls.Add(Ui.Button("Exportar período",()=>ExportCsv(from.Value.Date,to.Value.Date.AddDays(1))));if(today)toolbar.Controls.Add(Ui.Link("Ver todas as vendas",()=>Go("Vendas"),"arrow"));
   arrange();var initial=reportViewReady;reportViewReady=null;
   if(initial==null)throw new InvalidOperationException("O relatório inicial não foi carregado.");
   showReport(initial,from.Value.Date,to.Value.Date.AddDays(1));
  }
  class ReportView {public ReportResult result;public List<Sale> recent;}
  void BuildClassicCash(){
   var p=Content(false);p.AutoScroll=true;
   var view=cashViewReady;cashViewReady=null;
   if(view==null)throw new InvalidOperationException("Os dados do caixa ainda não foram carregados.");
   currentRegister=view.open;cashOccupied=view.occupied;var toolbar=Ui.Flow();toolbar.Dock=DockStyle.None;toolbar.AutoSize=false;toolbar.Height=55;
    var intro=Ui.Caption(view.open==null?view.occupied?"Outro operador está com o caixa aberto.":"Seu caixa está fechado. Abra para começar a receber.":"Aberto em "+LocalDate(view.open.openedAt)+" · "+view.open.operatorName);intro.AutoSize=false;intro.Height=38;
    if(view.open==null)toolbar.Controls.Add(Ui.Primary("Abrir caixa",()=>OpenCash(),"wallet"));else{toolbar.Controls.Add(Ui.Button("Suprimento",()=>CashMovement("supply")));toolbar.Controls.Add(Ui.Button("Sangria",()=>CashMovement("withdrawal")));toolbar.Controls.Add(Ui.Primary("Conferir e fechar caixa",()=>CloseCash(view.totals),"check"));}
    var cards=new StatsRow(new StatCard("Dinheiro esperado","wallet"),new StatCard("Vendas deste caixa","chart"),new StatCard("Suprimentos","plus"){Amber=true},new StatCard("Sangrias","minus"){Amber=true});
    if(view.totals!=null){cards.Cards[0].Value=D.Money(view.totals.drawer);cards.Cards[0].Detail="Fundo de troco: "+D.Money(view.open.opening);cards.Cards[1].Value=D.Money(view.totals.revenue);cards.Cards[1].Detail=view.totals.sales+" vendas concluídas";cards.Cards[2].Value=D.Money(view.totals.supplied);cards.Cards[2].Detail="Dinheiro extra colocado no caixa";cards.Cards[3].Value=D.Money(view.totals.withdrawn);cards.Cards[3].Detail="Dinheiro retirado do caixa";}else foreach(var card in cards.Cards){card.Value=D.Money(0);card.Detail="Nenhum caixa aberto";}
    var movementGrid=Ui.Grid("Data e hora","Tipo","Valor","Motivo");foreach(var move in view.moves.Take(100))movementGrid.Rows.Add(LocalDate(move.at),move.type=="supply"?"Suprimento":"Sangria",D.Money(move.amount),move.reason);
    var movements=GridSection("Movimentações","Suprimentos e sangrias deste caixa.",movementGrid);
    int cashPaginationVersion=0;
    int cashPanelSerial=pageLoadSerial;
    PaginationBar mp=null;
    Action<int> changeCashPage=async requestedOffset=>{
     if(view.open==null||requestedOffset<0||p.IsDisposed||page!="Meu caixa"||pageLoadSerial!=cashPanelSerial||nav.SelectedPage!="Meu caixa")return;
     int version=++cashPaginationVersion;
     mp.Previous.Enabled=false;mp.Next.Enabled=false;
     try{
      var fetched=await Task.Run(()=>{lock(db.Sync)return db.CashMoves(view.open.id,requestedOffset);});
      if(IsDisposed||p.IsDisposed||page!="Meu caixa"||nav.SelectedPage!="Meu caixa"||pageLoadSerial!=cashPanelSerial||version!=cashPaginationVersion)return;
      movementGrid.SuspendLayout();
      try{
       movementGrid.Rows.Clear();
       foreach(var move in fetched.Take(100))
        movementGrid.Rows.Add(LocalDate(move.at),move.type=="supply"?"Suprimento":"Sangria",D.Money(move.amount),move.reason);
      }finally{movementGrid.ResumeLayout(true);}
      cashOffset=requestedOffset;
      view.moves=fetched;
     }catch(Exception ex){
      if(!IsDisposed&&!p.IsDisposed&&page=="Meu caixa"&&pageLoadSerial==cashPanelSerial&&version==cashPaginationVersion)
       MessageBox.Show(this,ex.Message,"Não foi possível consultar movimentações");
     }finally{
      if(!IsDisposed&&!p.IsDisposed&&page=="Meu caixa"&&pageLoadSerial==cashPanelSerial&&version==cashPaginationVersion){
       mp.PageText="Página "+(cashOffset/100+1);
       mp.Previous.Enabled=cashOffset>0;
       mp.Next.Enabled=view.moves.Count>100;
      }
     }
    };
    mp=new PaginationBar(()=>changeCashPage(Math.Max(0,cashOffset-100)),()=>{if(view.moves.Count>100)changeCashPage(cashOffset+100);});
    mp.PageText="Página "+(cashOffset/100+1);
    mp.Previous.Enabled=cashOffset>0;mp.Next.Enabled=view.moves.Count>100;
    movements.Controls.Add(mp);
    var historyGrid=Ui.Grid("Abertura","Operador","Situação","Fundo","Diferença");foreach(var register in view.history.Take(100)){int row=historyGrid.Rows.Add(LocalDate(register.openedAt),register.operatorName,register.status=="open"?"Aberto":"Fechado",D.Money(register.opening),register.status=="closed"?D.Money(register.difference):"—");historyGrid.Rows[row].Tag=register;}
    historyGrid.CellDoubleClick+=(sender,e)=>{if(e.RowIndex<0)return;var register=(Register)historyGrid.Rows[e.RowIndex].Tag;Fire(()=>db.SessionTotals(register.id),data=>{var totals=(Totals)data;MessageBox.Show(this,"Vendas: "+D.Money(totals.revenue)+"\nDinheiro: "+D.Money(totals.drawer)+"\nPix: "+D.Money(totals.payments["pix"])+"\nDébito: "+D.Money(totals.payments["debito"])+"\nCrédito: "+D.Money(totals.payments["credito"])+"\nSuprimentos: "+D.Money(totals.supplied)+"\nSangrias: "+D.Money(totals.withdrawn),"Resumo do caixa");});};
    var history=GridSection("Histórico de caixas","Aberturas e fechamentos registrados.",historyGrid);
    int historyPaginationVersion=0;
    PaginationBar hp=null;
    Action<int> changeRegisterPage=async requestedOffset=>{
     if(requestedOffset<0||p.IsDisposed||page!="Meu caixa"||pageLoadSerial!=cashPanelSerial||nav.SelectedPage!="Meu caixa")return;
     int version=++historyPaginationVersion;
     hp.Previous.Enabled=false;hp.Next.Enabled=false;
     try{
      var fetched=await Task.Run(()=>{lock(db.Sync)return db.Registers(requestedOffset);});
      if(IsDisposed||p.IsDisposed||page!="Meu caixa"||nav.SelectedPage!="Meu caixa"||pageLoadSerial!=cashPanelSerial||version!=historyPaginationVersion)return;
      historyGrid.SuspendLayout();
      try{
       historyGrid.Rows.Clear();
       foreach(var register in fetched.Take(100)){
        int row=historyGrid.Rows.Add(LocalDate(register.openedAt),register.operatorName,
         register.status=="open"?"Aberto":"Fechado",D.Money(register.opening),
         register.status=="closed"?D.Money(register.difference):"—");
        historyGrid.Rows[row].Tag=register;
       }
      }finally{historyGrid.ResumeLayout(true);}
      registerOffset=requestedOffset;
      view.history=fetched;
     }catch(Exception ex){
      if(!IsDisposed&&!p.IsDisposed&&page=="Meu caixa"&&pageLoadSerial==cashPanelSerial&&version==historyPaginationVersion)
       MessageBox.Show(this,ex.Message,"Não foi possível consultar histórico de caixas");
     }finally{
      if(!IsDisposed&&!p.IsDisposed&&page=="Meu caixa"&&pageLoadSerial==cashPanelSerial&&version==historyPaginationVersion){
       hp.PageText="Página "+(registerOffset/100+1);
       hp.Previous.Enabled=registerOffset>0;
       hp.Next.Enabled=view.history.Count>100;
      }
     }
    };
    hp=new PaginationBar(()=>changeRegisterPage(Math.Max(0,registerOffset-100)),()=>{if(view.history.Count>100)changeRegisterPage(registerOffset+100);});
    hp.PageText="Página "+(registerOffset/100+1);
    hp.Previous.Enabled=registerOffset>0;hp.Next.Enabled=view.history.Count>100;
    history.Controls.Add(hp);
    p.Controls.AddRange(new Control[]{intro,toolbar,cards,movements,history});
    Action arrange=()=>{
     int w=ResponsiveLayout.ContentWidth(p.ClientSize.Width),x=ResponsiveLayout.ContentLeft(p.ClientSize.Width);
     intro.SetBounds(x,0,w,38);
     int toolbarHeight=Math.Max(60,Math.Min(180,toolbar.GetPreferredSize(new Size(w,0)).Height+8));
     toolbar.SetBounds(x,39,w,toolbarHeight);
     int cardsY=39+toolbarHeight+16;
     int statHeight=StatsRow.HeightFor(w,cards.Cards.Count);
     cards.SetBounds(x,cardsY,w,statHeight);
     int y=cardsY+statHeight+24;
     movements.SetBounds(x,y,w,325);
     history.SetBounds(x,y+348,w,350);
     p.AutoScrollMinSize=new Size(0,y+714);
    };
    p.SizeChanged+=(sender,e)=>arrange();arrange();
  }
 }
}
