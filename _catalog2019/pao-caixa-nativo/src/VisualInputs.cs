using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
namespace PaoCaixa {
 public class DateField:Panel,IThemed {
  readonly TextBox edit=new TextBox{BorderStyle=BorderStyle.None,MaxLength=10};readonly SoftButton calendar;ToolStripDropDown popup;bool focused;
  public DateField(){DoubleBuffered=true;ResizeRedraw=true;Size=new Size(154,40);Margin=new Padding(4);AccessibleName="Data";edit.Font=Ui.Font(9.5f);edit.Text=DateTime.Today.ToString("dd/MM/yyyy");Controls.Add(edit);calendar=(SoftButton)Ui.Link("",OpenCalendar,"calendar");calendar.AutoSize=false;calendar.MinimumSize=Size.Empty;calendar.AccessibleName="Abrir calendário";Controls.Add(calendar);edit.Enter+=(s,e)=>{focused=true;Invalidate();};edit.Leave+=(s,e)=>{focused=false;Invalidate();};edit.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.F4){e.SuppressKeyPress=true;OpenCalendar();}};}
  public DateTime Value {get{DateTime value;if(!DateTime.TryParseExact(edit.Text.Trim(),"dd/MM/yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out value))throw new Exception("Confira a data. Use dia/mês/ano, por exemplo 11/10/2026.");return value;}set{edit.Text=value.ToString("dd/MM/yyyy");}}
  public void OpenCalendar(){
   if(popup!=null&&!popup.IsDisposed&&popup.Visible){popup.Close();return;}
   if(popup!=null){popup.Dispose();popup=null;}
   DateTime selected;if(!DateTime.TryParseExact(edit.Text.Trim(),"dd/MM/yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out selected))selected=DateTime.Today;
   var month=new CalendarPicker(selected);var current=new ToolStripDropDown{Padding=Padding.Empty,BackColor=Ui.Card,DropShadowEnabled=true};popup=current;
   var host=new ToolStripControlHost(month){Margin=Padding.Empty,Padding=Padding.Empty,AutoSize=false,Size=month.Size};current.Items.Add(host);
   month.DateSelected+=(s,e)=>{Value=month.SelectedDate;current.Close();if(!edit.IsDisposed)edit.Focus();};month.Cancelled+=(s,e)=>{current.Close();if(!edit.IsDisposed)edit.Focus();};
   // Windows still reparents the popup after Closed. Dispose on the next UI turn.
   // Keep ownership until then so disposing the field also releases a queued popup.
   current.Closed+=(s,e)=>{if(IsDisposed||Disposing)return;try{BeginInvoke(new Action(()=>{if(popup==current)popup=null;current.Dispose();}));}catch(InvalidOperationException){}};
   var below=PointToScreen(new Point(0,Height+5));var area=Screen.FromControl(this).WorkingArea;
   int x=Math.Max(area.Left,Math.Min(below.X,area.Right-month.Width));int y=below.Y+month.Height<=area.Bottom?below.Y:Math.Max(area.Top,PointToScreen(Point.Empty).Y-month.Height-5);
   current.Show(new Point(x,y));month.Focus();
  }
  protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(calendar==null)return;edit.SetBounds(13,Math.Max(4,(Height-edit.PreferredHeight)/2),Math.Max(20,Width-58),edit.PreferredHeight);calendar.SetBounds(Width-39,4,34,Height-8);}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Ui.Background(Parent));e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Ui.Box(e.Graphics,new Rectangle(0,0,Width-1,Height-1),Ui.Card,focused?Ui.Green:Ui.Line,8);}
  public void ThemeChanged(){BackColor=Ui.Card;edit.BackColor=Ui.Card;edit.ForeColor=Ui.Ink;calendar.ThemeChanged();if(popup!=null&&!popup.IsDisposed){popup.BackColor=Ui.Card;foreach(ToolStripItem item in popup.Items){var host=item as ToolStripControlHost;if(host!=null)Ui.Apply(host.Control);}}Invalidate();}
  protected override void Dispose(bool disposing){if(disposing&&popup!=null)popup.Dispose();base.Dispose(disposing);}
 }
 public class ThemeSwitch:Control,IThemed {
  int selected;public event EventHandler SelectedIndexChanged;
  public int SelectedIndex {get{return selected;}set{value=value==1?1:0;if(selected==value)return;selected=value;Invalidate();if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);}}
  public ThemeSwitch(){Size=new Size(178,38);TabStop=true;Cursor=Cursors.Hand;AccessibleName="Tema claro ou escuro";SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Ui.Card);g.SmoothingMode=SmoothingMode.AntiAlias;Ui.Box(g,new Rectangle(0,0,Width-1,Height-1),Ui.Bg,Ui.Line,10);int half=Width/2;Ui.Box(g,new Rectangle(selected*half+3,3,half-6,Height-6),Ui.Accent,Color.Empty,7);for(int i=0;i<2;i++){var color=i==selected?Color.White:Ui.Muted;Icons.Draw(g,i==0?"sun":"moon",new Rectangle(i*half+10,(Height-17)/2,17,17),color);Ui.Text(g,i==0?"Claro":"Escuro",new Rectangle(i*half+33,0,half-39,Height),8.5f,color,i==selected?FontStyle.Bold:FontStyle.Regular);}if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(4,4,Width-8,Height-8),Ui.Green,Ui.Card);}
  protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);Focus();SelectedIndex=e.X<Width/2?0:1;}
  protected override bool IsInputKey(Keys keys){var key=keys&Keys.KeyCode;return key==Keys.Left||key==Keys.Right||key==Keys.Home||key==Keys.End||base.IsInputKey(keys);}
  protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Home){SelectedIndex=0;e.Handled=true;}else if(e.KeyCode==Keys.Right||e.KeyCode==Keys.End){SelectedIndex=1;e.Handled=true;}else if(e.KeyCode==Keys.Space){SelectedIndex=1-selected;e.Handled=true;}}
  public void ThemeChanged(){BackColor=Ui.Card;Invalidate();}
 }
 public class PaginationBar:Panel,IThemed {
  public readonly Button Previous,Next;readonly Label label=Ui.Caption("Página 1");
  public string PageText {get{return label.Text;}set{label.Text=value;}}
  public PaginationBar(Action previous,Action next){Dock=DockStyle.Bottom;Height=62;DoubleBuffered=true;Previous=Ui.IconButton("Anterior",previous,"arrow-left");Next=Ui.IconButton("Próxima",next,"arrow",true);Previous.AutoSize=Next.AutoSize=false;Controls.AddRange(new Control[]{Previous,label,Next});label.AutoSize=false;label.TextAlign=ContentAlignment.MiddleCenter;label.Margin=Padding.Empty;}
  protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(Previous==null)return;int x=Math.Max(8,Width-349);Previous.SetBounds(x,11,112,40);label.SetBounds(x+120,11,90,40);Next.SetBounds(x+218,11,112,40);}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Ui.Card);using(var pen=new Pen(Ui.Line))e.Graphics.DrawLine(pen,16,0,Width-16,0);}
  public void ThemeChanged(){BackColor=Ui.Card;Invalidate();}
 }
 // Animate only while the wheel is moving; no idle redraw loop.
 public class SmoothPanel:Panel,ISmoothScrollHost {
  readonly ScrollAnimator wheel;
  public SmoothPanel(){DoubleBuffered=true;ResizeRedraw=true;wheel=new ScrollAnimator(()=>Math.Abs(AutoScrollPosition.Y),()=>Math.Max(0,DisplayRectangle.Height-ClientSize.Height),y=>AutoScrollPosition=new Point(Math.Abs(AutoScrollPosition.X),y));}
  bool ISmoothScrollHost.ScrollWheel(int delta){return AutoScroll&&VerticalScroll.Visible&&wheel.Scroll(delta,ClientSize.Height);}
  protected override void OnMouseWheel(MouseEventArgs e){if(!((ISmoothScrollHost)this).ScrollWheel(e.Delta)){base.OnMouseWheel(e);return;}var handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;}
  protected override void OnScroll(ScrollEventArgs e){if(wheel!=null&&!wheel.Adjusting)wheel.Cancel();base.OnScroll(e);}
  protected override void Dispose(bool disposing){if(disposing)wheel.Dispose();base.Dispose(disposing);}
 }
}
