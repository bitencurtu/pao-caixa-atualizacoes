using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
namespace PaoCaixa {
 public class SectionHeading:Control,IThemed {
  public string Title,Description,IconName;public SectionHeading(string title,string description,string icon=null){Title=title;Description=description;IconName=icon;Dock=DockStyle.Top;Height=81;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Ui.Bg);g.SmoothingMode=SmoothingMode.AntiAlias;Ui.Box(g,new Rectangle(0,0,Width-1,Height+16),Ui.Card,Ui.Line,12);Ui.Text(g,Title,new Rectangle(21,Height<75?6:14,Width-43,30),12,Ui.Ink,FontStyle.Bold);Ui.Text(g,Description,new Rectangle(21,Height<75?34:46,Width-43,22),8.5f,Ui.Muted);using(var p=new Pen(Ui.Line))g.DrawLine(p,0,Height-1,Width,Height-1);}
  public void ThemeChanged(){Invalidate();}
 }
 public static class ResponsiveLayout {
  // Medidas lógicas: limitar larguras sem distorcer cartões em monitores grandes.
  public static int SidebarWidth(int formWidth){return formWidth<980?194:formWidth<1180?212:232;}
  public static int BodyPadding(int availableWidth){return availableWidth<680?14:availableWidth<900?20:28;}
  public static int ContentWidth(int availableWidth){return Math.Max(1,Math.Min(1160,availableWidth-18));}
  public static int ContentLeft(int availableWidth){return Math.Max(0,(availableWidth-ContentWidth(availableWidth))/2);}
  public static Size InitialClientSize(Rectangle workArea){
   return new Size(Math.Max(1,Math.Min(1280,workArea.Width-46)),Math.Max(1,Math.Min(780,workArea.Height-78)));
  }
  public static Size MinimumWindowSize(Rectangle workArea){
   return new Size(Math.Max(1,Math.Min(820,workArea.Width-16)),Math.Max(1,Math.Min(590,workArea.Height-16)));
  }
 }
 public class StatsRow:Panel {
  public readonly List<StatCard> Cards=new List<StatCard>();
  public StatsRow(params StatCard[] cards){Cards.AddRange(cards);Controls.AddRange(cards);Height=147;}
  public static int ColumnsFor(int width,int cardCount){
   return Math.Min(cardCount,width>=710?4:width>=440?2:1);
  }
  public static int HeightFor(int width,int cardCount){
   if(width<1||cardCount<1)return 0;
   int cols=ColumnsFor(width,cardCount),rows=(cardCount+cols-1)/cols;
   return rows*147+(rows-1)*16;
  }
  protected override void OnLayout(LayoutEventArgs e){
   base.OnLayout(e);if(Cards==null||Cards.Count==0||Width<1)return;
   int cols=ColumnsFor(Width,Cards.Count),gap=16,w=Math.Max(1,(Width-(cols-1)*gap)/cols);
   int required=HeightFor(Width,Cards.Count);
   if(Height!=required)Height=required;
   for(int i=0;i<Cards.Count;i++)Cards[i].SetBounds((i%cols)*(w+gap),(i/cols)*163,w,147);
  }
 }
 public class CategoryTabs:Control,IThemed {
  public readonly List<string> Items=new List<string>();public string Selected="Todos";public event Action Changed;int offset;readonly List<KeyValuePair<Rectangle,string>> hits=new List<KeyValuePair<Rectangle,string>>();
  public CategoryTabs(IEnumerable<string> categories){Items.AddRange(categories);Dock=DockStyle.Top;Height=54;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Ui.Bg);g.SmoothingMode=SmoothingMode.AntiAlias;hits.Clear();int x=-offset;foreach(string c in Items){int w=TextRenderer.MeasureText(c,Font).Width+24;var r=new Rectangle(x,10,w,31);hits.Add(new KeyValuePair<Rectangle,string>(r,c));bool on=c==Selected;Ui.Box(g,r,on?Ui.Soft:Ui.Bg,on?Ui.Dark?Ui.Green:Color.FromArgb(209,223,190):Color.Empty,6);Ui.Text(g,c,r,8.5f,on?Ui.Green:Ui.Muted,FontStyle.Regular,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);x+=w+7;}if(x>Width){Ui.Box(g,new Rectangle(Width-25,10,25,31),Ui.Bg,Color.Empty,0);Icons.Draw(g,"chevron",new Rectangle(Width-21,18,14,14),Ui.Muted);}}
  protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);if(e.X>Width-26){offset+=100;if(hits.Count>0&&hits[hits.Count-1].Key.Right<Width)offset=0;Invalidate();return;}foreach(var h in hits)if(h.Key.Contains(e.Location)){Selected=h.Value;Invalidate();if(Changed!=null)Changed();break;}}
  public void ThemeChanged(){Invalidate();}
 }
 public class DailyPanel:CanvasView,IThemed {
  public Dictionary<string,long> Values=new Dictionary<string,long>();public DailyPanel(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
  public void Bind(Dictionary<string,long> values){Values=values;VirtualSize=new Size(0,110+Values.Count*42);Invalidate();}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Ui.Bg);g.SmoothingMode=SmoothingMode.AntiAlias;int w=ClientSize.Width-(ScrollBar.Visible?18:0),sy=ScrollOffset.Y;Ui.Box(g,new Rectangle(0,sy,w-1,Math.Max(Height-1,VirtualSize.Height)),Ui.Card,Ui.Line,12);Ui.Text(g,"Vendas por dia",new Rectangle(22,18+sy,w-44,28),12,Ui.Ink,FontStyle.Bold);Ui.Text(g,"Evolução do faturamento no período selecionado.",new Rectangle(22,52+sy,w-44,22),8.5f,Ui.Muted);int y=100+sy;if(Values.Count==0)Ui.Text(g,"Nenhuma venda concluída neste período.",new Rectangle(22,y,w-44,30),9,Ui.Muted);long max=Values.Count==0?1:Math.Max(1,Values.Values.Max());foreach(var item in Values.OrderBy(x=>x.Key)){DateTime d;string label=DateTime.TryParse(item.Key,out d)?d.ToString("dd/MM"):item.Key;Ui.Text(g,label,new Rectangle(22,y,61,30),8.5f,Ui.Muted);int bw=Math.Max(1,w-226);Ui.Box(g,new Rectangle(98,y+10,bw,9),Ui.Soft,Color.Empty,4);int n=(int)(bw*item.Value/(decimal)max);if(n>0)Ui.Box(g,new Rectangle(98,y+10,n,9),Ui.Green,Color.Empty,4);Ui.Text(g,D.Money(item.Value),new Rectangle(w-124,y,102,30),9,Ui.Ink,FontStyle.Bold,TextFormatFlags.Right|TextFormatFlags.VerticalCenter);y+=42;}}
  public void ThemeChanged(){Invalidate();}
 }
 public class StockAlerts:CanvasView,IThemed {
  public List<Product> Items=new List<Product>();public Action<Product> Adjust;readonly List<KeyValuePair<Rectangle,Product>> hits=new List<KeyValuePair<Rectangle,Product>>();public StockAlerts(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
  public void Bind(List<Product> products){Items=products.Where(x=>x.active&&x.stock<=x.minStock).ToList();VirtualSize=new Size(0,112+Items.Count*73);Invalidate();}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Ui.Bg);g.SmoothingMode=SmoothingMode.AntiAlias;int w=ClientSize.Width-(ScrollBar.Visible?18:0),sy=ScrollOffset.Y;Ui.Box(g,new Rectangle(0,sy,w-1,Math.Max(Height-1,VirtualSize.Height)),Ui.Card,Ui.Line,12);Ui.Text(g,"Atenção ao estoque",new Rectangle(22,18+sy,w-44,28),12,Ui.Ink,FontStyle.Bold);Ui.Text(g,"Hora de preparar a próxima fornada.",new Rectangle(22,52+sy,w-44,22),8.5f,Ui.Muted);hits.Clear();if(Items.Count==0){Icons.Draw(g,"check",new Rectangle(w/2-18,134+sy,36,36),Ui.Green);Ui.Text(g,"Estoque em dia",new Rectangle(22,181+sy,w-44,29),12,Ui.Ink,FontStyle.Bold,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);Ui.Text(g,"Nenhum produto abaixo do estoque mínimo.",new Rectangle(22,220+sy,w-44,36),8.5f,Ui.Muted,FontStyle.Regular,TextFormatFlags.HorizontalCenter|TextFormatFlags.WordBreak);return;}for(int i=0;i<Items.Count;i++){int y=105+sy+i*73;var p=Items[i];hits.Add(new KeyValuePair<Rectangle,Product>(new Rectangle(0,y,w,73),p));Icons.Draw(g,"help",new Rectangle(21,y+19,20,20),ProductCatalog.ArtInk("bread"));Ui.Text(g,p.name,new Rectangle(54,y+10,w-156,28),9,Ui.Ink,FontStyle.Bold);Ui.Text(g,"Mínimo: "+D.Qty(p.minStock,p.unit)+" "+p.unit,new Rectangle(54,y+39,w-130,22),7.5f,Ui.Muted);Ui.Text(g,D.Qty(p.stock,p.unit)+" "+p.unit,new Rectangle(w-92,y+20,70,30),9,Ui.Green,FontStyle.Bold,TextFormatFlags.Right|TextFormatFlags.VerticalCenter);}}
  protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);if(Adjust==null)return;foreach(var h in hits)if(h.Key.Contains(e.Location)){Adjust(h.Value);return;}}
  public void ThemeChanged(){Invalidate();}
 }
}
