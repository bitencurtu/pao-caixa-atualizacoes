using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
namespace PaoCaixa {
 public sealed class CalendarPicker:Control,IThemed {
  DateTime selected,month;Point hover=new Point(-1,-1);
  public event EventHandler DateSelected,Cancelled;
  public DateTime SelectedDate {get{return selected;}set{selected=value.Date;month=new DateTime(value.Year,value.Month,1);Invalidate();}}
  public DateTime DisplayMonth {get{return month;}}
  public CalendarPicker(DateTime date){Size=new Size(322,368);TabStop=true;Cursor=Cursors.Hand;AccessibleName="Escolher data. Use as setas para escolher o dia e Enter para confirmar.";AccessibleRole=AccessibleRole.Table;SelectedDate=date;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
  Rectangle Previous {get{return new Rectangle(16,38,34,34);}}
  Rectangle Next {get{return new Rectangle(Width-50,38,34,34);}}
  Rectangle Today {get{return new Rectangle(16,Height-51,Width-32,35);}}
  Rectangle Cell(int index){int w=(Width-28)/7;return new Rectangle(14+index%7*w,106+index/7*33,w,33);}
  bool Day(int index,out DateTime date){int offset=index-(int)month.DayOfWeek;long ticks=month.Ticks+offset*TimeSpan.TicksPerDay;date=DateTime.MinValue;if(ticks<DateTime.MinValue.Ticks||ticks>DateTime.MaxValue.Date.Ticks)return false;date=new DateTime(ticks);return true;}
  public void NavigateMonth(int count){try{DateTime next=month.AddMonths(count);int day=Math.Min(selected.Day,DateTime.DaysInMonth(next.Year,next.Month));SelectedDate=new DateTime(next.Year,next.Month,day);}catch(ArgumentOutOfRangeException){}}
  void Confirm(){if(DateSelected!=null)DateSelected(this,EventArgs.Empty);}
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;g.Clear(Ui.Card);g.SmoothingMode=SmoothingMode.AntiAlias;Ui.Box(g,new Rectangle(0,0,Width-1,Height-1),Ui.Card,Ui.Line,12);
   Ui.Text(g,"ESCOLHA UMA DATA",new Rectangle(20,12,Width-40,20),7.5f,Ui.Muted,FontStyle.Bold);
   string title=month.ToString("MMMM 'de' yyyy",D.BR);title=char.ToUpper(title[0],D.BR)+title.Substring(1);Ui.Text(g,title,new Rectangle(54,36,Width-108,38),10.5f,Ui.Ink,FontStyle.Bold,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
   foreach(bool previous in new[]{true,false}){var r=previous?Previous:Next;bool enabled=previous?month.Year>1||month.Month>1:month.Year<9999||month.Month<12;Ui.Box(g,r,r.Contains(hover)&&enabled?Ui.Soft:Ui.Card,Ui.Line,8);Icons.Draw(g,previous?"arrow-left":"arrow",new Rectangle(r.X+9,r.Y+9,16,16),enabled?Ui.Ink:Ui.Muted);}
   string[] names={"Dom","Seg","Ter","Qua","Qui","Sex","Sáb"};for(int i=0;i<7;i++){var r=Cell(i);Ui.Text(g,names[i],new Rectangle(r.X,80,r.Width,23),8,Ui.Muted,FontStyle.Regular,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
   for(int i=0;i<42;i++){DateTime date;if(!Day(i,out date))continue;var cell=Cell(i);var box=new Rectangle(cell.X+3,cell.Y+1,cell.Width-6,cell.Height-2);bool active=date==selected,today=date==DateTime.Today;Color ink=active?Color.White:date.Month==month.Month?Ui.Ink:Ui.Muted;
    if(active||cell.Contains(hover)||today)Ui.Box(g,box,active?Ui.Accent:cell.Contains(hover)?Ui.Soft:Ui.Card,today&&!active?Ui.Green:Color.Empty,8);
    Ui.Text(g,date.Day.ToString(CultureInfo.InvariantCulture),cell,9,ink,active?FontStyle.Bold:FontStyle.Regular,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
    if(active&&Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(box.X+3,box.Y+3,box.Width-6,box.Height-6),Color.White,Ui.Accent);
   }
   using(var pen=new Pen(Ui.Line))g.DrawLine(pen,16,Height-62,Width-16,Height-62);
   Ui.Box(g,Today,Today.Contains(hover)?Ui.Soft:Ui.Bg,Color.Empty,8);Icons.Draw(g,"calendar",new Rectangle(Today.X+12,Today.Y+9,17,17),Ui.Green);
   Ui.Text(g,"Hoje · "+DateTime.Today.ToString("dd/MM/yyyy"),new Rectangle(Today.X+36,Today.Y,Today.Width-49,Today.Height),9,Ui.Green,FontStyle.Bold);
  }
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);hover=e.Location;Invalidate();}
  protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=new Point(-1,-1);Invalidate();}
  protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);if(e.Button!=MouseButtons.Left)return;Focus();if(Previous.Contains(e.Location)){NavigateMonth(-1);return;}if(Next.Contains(e.Location)){NavigateMonth(1);return;}if(Today.Contains(e.Location)){SelectedDate=DateTime.Today;Confirm();return;}for(int i=0;i<42;i++){DateTime date;if(Cell(i).Contains(e.Location)&&Day(i,out date)){SelectedDate=date;Confirm();return;}}}
  protected override bool IsInputKey(Keys keyData){switch(keyData&Keys.KeyCode){case Keys.Left:case Keys.Right:case Keys.Up:case Keys.Down:case Keys.Home:case Keys.PageUp:case Keys.PageDown:case Keys.Enter:case Keys.Escape:return true;}return base.IsInputKey(keyData);}
  protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);int days=0;switch(e.KeyCode){case Keys.Left:days=-1;break;case Keys.Right:days=1;break;case Keys.Up:days=-7;break;case Keys.Down:days=7;break;case Keys.PageUp:NavigateMonth(e.Control?-12:-1);break;case Keys.PageDown:NavigateMonth(e.Control?12:1);break;case Keys.Home:SelectedDate=DateTime.Today;break;case Keys.Enter:Confirm();break;case Keys.Escape:if(Cancelled!=null)Cancelled(this,EventArgs.Empty);break;default:return;}if(days!=0){try{SelectedDate=selected.AddDays(days);}catch(ArgumentOutOfRangeException){}}e.Handled=true;e.SuppressKeyPress=true;}
  protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
  protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
  public void ThemeChanged(){BackColor=Ui.Card;ForeColor=Ui.Ink;Invalidate();}
 }
}
