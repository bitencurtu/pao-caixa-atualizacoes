using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace PaoCaixa {
 public static class Ui {
  public static bool Dark;
  public static Color Bg {get{return Dark?Color.FromArgb(22,29,26):Color.FromArgb(247,247,241);}}
  public static Color Card {get{return Dark?Color.FromArgb(31,41,35):Color.White;}}
  public static Color Side {get{return Dark?Color.FromArgb(25,34,29):Color.FromArgb(255,254,249);}}
  public static Color Ink {get{return Dark?Color.FromArgb(232,238,230):Color.FromArgb(41,53,46);}}
  public static Color Muted {get{return Dark?Color.FromArgb(161,177,160):Color.FromArgb(123,133,116);}}
  public static Color Line {get{return Dark?Color.FromArgb(53,68,56):Color.FromArgb(231,232,224);}}
  public static Color Soft {get{return Dark?Color.FromArgb(43,57,43):Color.FromArgb(237,243,233);}}
  public static Color Accent=Color.FromArgb(33,77,58);
  public static Color Green {get{return Dark?Color.FromArgb(158,195,139):Accent;}}
  public static Color Background(Control c){while(c!=null){if(c.BackColor.A==255)return c.BackColor;c=c.Parent;}return Bg;}
  public static Font Font(float size=10,FontStyle style=FontStyle.Regular){return new Font("Segoe UI",size,style);}
  // Fontes de desenho sao compartilhadas: antes cada texto pintado criava um Font.
  // Nunca descarte fontes retornadas por PaintFont individualmente.
  static readonly Dictionary<string,Font> paintFonts=new Dictionary<string,Font>();
  internal static Font PaintFont(float size,FontStyle style){
   string key=size.ToString("R",CultureInfo.InvariantCulture)+":"+((int)style).ToString(CultureInfo.InvariantCulture);
   lock(paintFonts){
    Font f;
    if(!paintFonts.TryGetValue(key,out f)){f=new Font("Segoe UI",size,style);paintFonts.Add(key,f);}
    return f;
   }
  }
  public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));if(d<=0){p.AddRectangle(r);return p;}p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
  public static void Box(Graphics g,RectangleF r,Color fill,Color border,float radius=10){using(var p=Round(r,radius)){using(var b=new SolidBrush(fill))g.FillPath(b,p);if(border!=Color.Empty)using(var pen=new Pen(border))g.DrawPath(pen,p);}}
  public static void Text(Graphics g,string text,Rectangle r,float size,Color color,FontStyle style=FontStyle.Regular,TextFormatFlags flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis){TextRenderer.DrawText(g,text??"",PaintFont(size,style),r,color,flags|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);}
  public static Button Button(string label,Action action){var b=new SoftButton{Text=label,AutoSize=true,MinimumSize=new Size(96,40),Margin=new Padding(4),Cursor=Cursors.Hand};b.Click+=(s,e)=>{try{action();}catch(Exception ex){MessageBox.Show(b.FindForm(),ex.Message,"Pão & Caixa",MessageBoxButtons.OK,MessageBoxIcon.Information);}};return b;}
  public static Button Primary(string label,Action action,string icon=null){var b=(SoftButton)Button(label,action);b.Tone="primary";b.IconName=icon;return b;}
  public static Button IconButton(string label,Action action,string icon,bool right=false){var b=(SoftButton)Button(label,action);b.IconName=icon;b.IconRight=right;return b;}
  public static Button Link(string label,Action action,string icon=null){var b=(SoftButton)Button(label,action);b.Tone="link";b.IconName=icon;return b;}
  public static Label Label(string text,int size=10){return new Label{UseMnemonic=false,Text=text,AutoSize=true,Font=Font(size,size>=15?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(4,8,4,6),MaximumSize=new Size(1200,0),BackColor=Color.Transparent};}
  public static Label Caption(string text){var l=Label(text,9);l.Tag="muted";return l;}
  public static FlowLayoutPanel Flow(){return new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(2),BackColor=Color.Transparent};}
  public static DataGridView Grid(params string[] headings){var g=new CleanGrid{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None,RowTemplate={Height=55},BorderStyle=BorderStyle.None,CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal,ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None,ColumnHeadersHeight=45,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing};foreach(string h in headings){int idx=g.Columns.Add("c"+g.Columns.Count,h);g.Columns[idx].SortMode=DataGridViewColumnSortMode.NotSortable;g.Columns[idx].MinimumWidth=65;}return g;}
  // Evitar invalidar todos os controles quando o tema ja foi aplicado.
  // ConditionalWeakTable nao prende controles descartados na memoria.
  sealed class ThemeCache {public bool Initialized,WasDark;}
  static readonly ConditionalWeakTable<Control,ThemeCache> themedControls=new ConditionalWeakTable<Control,ThemeCache>();
  public static void Apply(Control c){
   if(c==null||c.IsDisposed)return;
   ThemeCache cache=themedControls.GetValue(c,key=>new ThemeCache());
   bool themeChanged=!cache.Initialized||cache.WasDark!=Dark;
   string tag=c.Tag as string;
   Color fore=tag=="muted"?Muted:Ink;
   Color back=c is TextBox||c is ComboBox||c is DataGridView||c is CardPanel?Card:tag=="sidebar"?Side:tag=="card"?Card:Bg;
   if(c is Label||c is FlowLayoutPanel)back=Color.Transparent;
   // Controles personalizados podem definir seu proprio BackColor em ThemeChanged().
   // Sem troca de tema, nem tocar nessas propriedades (isso gerava invalidacao extra).
   if(themeChanged){
    if(c.ForeColor!=fore)c.ForeColor=fore;
    if(c.BackColor!=back)c.BackColor=back;
    // Tabelas precisam receber os estilos apenas ao entrar ou mudar de tema.
    var g=c as DataGridView;
    if(g!=null){
     g.EnableHeadersVisualStyles=false;
     g.BackgroundColor=Card;g.GridColor=Line;
     g.DefaultCellStyle.BackColor=Card;g.DefaultCellStyle.ForeColor=Ink;
     g.DefaultCellStyle.Padding=new Padding(12,4,8,4);
     g.DefaultCellStyle.SelectionBackColor=Soft;g.DefaultCellStyle.SelectionForeColor=Ink;
     g.ColumnHeadersDefaultCellStyle.BackColor=Card;g.ColumnHeadersDefaultCellStyle.ForeColor=Muted;
     var clean=g as CleanGrid;if(clean!=null)g.ColumnHeadersDefaultCellStyle.Font=clean.HeaderFont;
     g.ColumnHeadersDefaultCellStyle.Padding=new Padding(12,0,0,0);
     g.AlternatingRowsDefaultCellStyle.BackColor=Card;
    }
    var custom=c as IThemed;
    if(custom!=null)custom.ThemeChanged();
    else if(c.IsHandleCreated)c.Invalidate();
    cache.WasDark=Dark;cache.Initialized=true;
   }
   foreach(Control child in c.Controls)Apply(child);
  }
  public static DialogResult Show(Form owner,Form f){Apply(f);return f.ShowDialog(owner);}
 }
 public interface IThemed {void ThemeChanged();}
 public class CleanGrid:DataGridView {
  internal readonly Font HeaderFont=Ui.Font(9);
  public CleanGrid(){DoubleBuffered=true;}
  protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing)HeaderFont.Dispose();}
 }
 public class CardPanel:Panel,IThemed {
  public bool Border=true;public int Radius=12;
  public CardPanel(){DoubleBuffered=true;ResizeRedraw=true;Padding=new Padding(16);}
  protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(Width<1||Height<1)return;using(var path=Ui.Round(new RectangleF(0,0,Width,Height),Radius)){var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Ui.Bg:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Ui.Box(e.Graphics,new RectangleF(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1)),Ui.Card,Border?Ui.Line:Color.Empty,Radius);}
  public void ThemeChanged(){Invalidate();}
 }
 public class SoftButton:Button,IThemed {
  public string Tone="secondary",IconName;public bool Active,IconRight;bool hover;
  public SoftButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Font=Ui.Font(9.5f);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
  public override Size GetPreferredSize(Size proposed){return new Size(Math.Max(MinimumSize.Width,TextRenderer.MeasureText(Text,Font).Width+30+(IconName==null?0:24)),Math.Max(MinimumSize.Height,40));}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Ui.Background(Parent));g.SmoothingMode=SmoothingMode.AntiAlias;bool primary=Tone=="primary"||Active;Color fill=primary?Ui.Accent:Tone=="link"?(hover?Ui.Soft:Ui.Background(Parent)):(hover?Ui.Soft:Ui.Card);Color ink=!Enabled?Ui.Muted:primary?Color.White:Ui.Ink;if(!Enabled&&primary)fill=Ui.Dark?Color.FromArgb(55,72,60):Color.FromArgb(155,174,153);Ui.Box(g,new RectangleF(0,0,Width-1,Height-1),fill,Tone=="link"||primary?Color.Empty:Ui.Line,8);int left=IconName==null||IconRight?12:42;int right=IconName!=null&&IconRight?38:12;if(IconName!=null)Icons.Draw(g,IconName,new Rectangle(Text.Length==0?(Width-20)/2:IconRight?Width-33:14,(Height-20)/2,20,20),ink);Ui.Text(g,Text,new Rectangle(left,0,Width-left-right,Height),9.5f,ink,FontStyle.Regular,IconName==null?TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis:TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);if(Focused&&ShowFocusCues){using(var p=new Pen(Ui.Green)){p.DashStyle=DashStyle.Dot;g.DrawRectangle(p,4,4,Width-9,Height-9);}}}
  public void ThemeChanged(){BackColor=Ui.Card;Invalidate();}
 }
 public class FieldBox:Panel,IThemed {
  public readonly TextBox Edit;public string IconName;readonly Label hint;bool focused;
  public FieldBox(TextBox edit,string placeholder=null,string icon=null){DoubleBuffered=true;ResizeRedraw=true;Height=44;MinimumSize=new Size(80,36);Edit=edit;IconName=icon;Edit.BorderStyle=BorderStyle.None;Edit.Font=Ui.Font(10);Controls.Add(Edit);hint=Ui.Caption(placeholder??"");hint.AutoSize=false;hint.Cursor=Cursors.IBeam;hint.Click+=(s,e)=>Edit.Focus();Controls.Add(hint);hint.BringToFront();hint.Visible=Edit.Text.Length==0&&hint.Text.Length>0;Edit.TextChanged+=(s,e)=>{hint.Visible=Edit.Text.Length==0&&hint.Text.Length>0;};Edit.Enter+=(s,e)=>{focused=true;hint.Visible=Edit.Text.Length==0&&hint.Text.Length>0;Invalidate();};Edit.Leave+=(s,e)=>{focused=false;hint.Visible=Edit.Text.Length==0&&hint.Text.Length>0;Invalidate();};}
  protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(Edit==null||hint==null)return;int left=IconName==null?12:40;Edit.SetBounds(left,Math.Max(5,(Height-Edit.PreferredHeight)/2),Math.Max(15,Width-left-12),Edit.PreferredHeight);hint.SetBounds(left,Math.Max(2,(Height-24)/2),Math.Max(15,Width-left-12),24);hint.TextAlign=ContentAlignment.MiddleLeft;}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Ui.Bg:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Ui.Box(e.Graphics,new RectangleF(0,0,Width-1,Height-1),Ui.Card,focused?Ui.Green:Ui.Line,8);if(IconName!=null)Icons.Draw(e.Graphics,IconName,new Rectangle(12,(Height-20)/2,20,20),Ui.Muted);}
  public void ThemeChanged(){Edit.BackColor=Ui.Card;Edit.ForeColor=Ui.Ink;hint.BackColor=Ui.Card;hint.ForeColor=Ui.Muted;Invalidate();}
 }
 public class Fields:Form {
  readonly TableLayoutPanel table=new TableLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,ColumnCount=2,Padding=new Padding(22)};
  public readonly Button Ok;public Fields(string title){Text=title;Font=Ui.Font();Size=new Size(540,520);MinimumSize=new Size(420,180);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;var bottom=Ui.Flow();bottom.Dock=DockStyle.Bottom;bottom.Padding=new Padding(16,10,16,12);Ok=Ui.Primary("Confirmar",()=>{DialogResult=DialogResult.OK;},"check");bottom.Controls.Add(Ok);var cancel=Ui.Button("Voltar",()=>DialogResult=DialogResult.Cancel);bottom.Controls.Add(cancel);CancelButton=cancel;AcceptButton=Ok;table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,43));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,57));Controls.Add(table);Controls.Add(bottom);}
  public void Add(string label,Control control){int row=table.RowCount++;MinimumSize=new Size(MinimumSize.Width,Math.Max(180,table.RowCount*52+136));table.RowStyles.Add(new RowStyle(SizeType.Absolute,52));table.Controls.Add(Ui.Label(label),0,row);var text=control as TextBox;if(text!=null)control=new FieldBox(text);control.Dock=DockStyle.Fill;control.Margin=new Padding(4,5,4,5);table.Controls.Add(control,1,row);}
  public TextBox TextField(string label,string value="",bool password=false){var t=new TextBox{Text=value??"",UseSystemPasswordChar=password,MaxLength=password?128:200};Add(label,t);return t;}
  public ComboBox Choice(string label,string[] items,int selected=0){var c=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat};c.Items.AddRange(items);c.SelectedIndex=selected;Add(label,c);return c;}
  public CheckBox Check(string label,bool value){var c=new CheckBox{Checked=value,Text="Sim",AutoSize=true};Add(label,c);return c;}
 }
}
