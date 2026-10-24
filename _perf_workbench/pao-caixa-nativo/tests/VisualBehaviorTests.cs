using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using PaoCaixa;
class VisualBehaviorTests {
 static int passed;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS visual "+name);}
 static void Pump(int milliseconds){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<milliseconds){Application.DoEvents();System.Threading.Thread.Sleep(5);}Application.DoEvents();}
 static void Invoke(Control c,string name,object argument){c.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new[]{argument});}
 static void Key(CalendarPicker c,Keys key){Invoke(c,"OnKeyDown",new KeyEventArgs(key));}
 static void Wheel(CanvasView c,int delta){typeof(CanvasView).GetMethod("OnMouseWheel",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{new HandledMouseEventArgs(MouseButtons.None,0,40,40,delta)});}
 static ToolStripDropDown Popup(DateField f){return (ToolStripDropDown)typeof(DateField).GetField("popup",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);}
 static Message WheelMessage(Control source,Point screen,int delta){long coordinates=((long)(screen.Y&65535)<<16)|(long)(ushort)screen.X;return Message.Create(source.Handle,0x020A,new IntPtr(unchecked((int)((uint)(ushort)(short)delta<<16))),new IntPtr(unchecked((int)coordinates)));}
 [STAThread] static void Main(){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  using(var form=new Form{ClientSize=new Size(600,480),StartPosition=FormStartPosition.Manual,Location=new Point(80,60)}){
   var canvas=new CanvasView{Location=new Point(0,0),Size=new Size(300,200)};form.Controls.Add(canvas);canvas.VirtualSize=new Size(0,1400);form.Show();Pump(30);
   int lines=SystemInformation.MouseWheelScrollLines;int step=lines<0?176:Math.Min(lines,20)*24;
   Wheel(canvas,-120);Check(canvas.ScrollBar.Value==0,"wheel starts without a jump");Pump(90);Check(step==0||canvas.ScrollBar.Value>0&&canvas.ScrollBar.Value<step,"wheel interpolates through intermediate pixels");Pump(600);Check(canvas.ScrollBar.Value==step,"wheel settles at exact destination");
   canvas.ScrollOffset=Point.Empty;Wheel(canvas,-120);Wheel(canvas,-120);Wheel(canvas,-120);Pump(750);Check(canvas.ScrollBar.Value==step*3,"rapid wheel input accumulates without losing distance");
   canvas.ScrollOffset=new Point(0,400);Wheel(canvas,-120);Pump(40);int turning=canvas.ScrollBar.Value;Wheel(canvas,120);Pump(700);Check(step==0||canvas.ScrollBar.Value<turning,"direction reversal responds immediately");
   Wheel(canvas,-120);Pump(30);canvas.ScrollBar.Value=450;Pump(700);Check(canvas.ScrollBar.Value==450,"manual scrollbar stops animation");
   Wheel(canvas,-120);canvas.ScrollOffset=new Point(0,200);Pump(700);Check(canvas.ScrollBar.Value==200,"explicit scroll position stops animation");
   canvas.ScrollOffset=new Point(0,1100);Wheel(canvas,-120);canvas.Height=1300;Pump(700);Check(canvas.ScrollBar.Value<=100,"resize clamps pending scroll to new bounds");
   canvas.VirtualSize=new Size(0,30);Pump(50);Check(canvas.ScrollBar.Value==0&&!canvas.ScrollBar.Visible,"shrinking content removes obsolete scroll range");canvas.Dispose();
   var panel=new SmoothPanel{Dock=DockStyle.Fill,AutoScroll=true,AutoScrollMinSize=new Size(0,1400)};var label=new Label{Text="Wheel over child",Location=new Point(15,15),Size=new Size(160,35)};panel.Controls.Add(label);form.Controls.Add(panel);Pump(30);
   using(var router=new SmoothWheelRouter(form)){
    var message=WheelMessage(label,label.PointToScreen(new Point(10,10)),-120);bool routed=router.PreFilterMessage(ref message);Pump(750);Check(lines==0?!routed:routed&&Math.Abs(panel.AutoScrollPosition.Y)>0,"wheel over label reaches the hovered viewport");
    panel.AutoScrollPosition=Point.Empty;var grid=Ui.Grid("Data");grid.Dock=DockStyle.None;grid.SetBounds(15,100,280,150);panel.Controls.Add(grid);Pump(30);message=WheelMessage(grid,grid.PointToScreen(new Point(30,50)),-120);Check(!router.PreFilterMessage(ref message),"native grids keep their own wheel behavior");
    var font=grid.ColumnHeadersDefaultCellStyle.Font;Ui.Apply(grid);font=grid.ColumnHeadersDefaultCellStyle.Font;for(int i=0;i<30;i++){Ui.Dark=i%2==0;Ui.Apply(grid);}Check(object.ReferenceEquals(font,grid.ColumnHeadersDefaultCellStyle.Font),"theme changes reuse the owned grid header font");grid.Dispose();
    var text=new TextBox{Multiline=true,Location=new Point(20,160),Size=new Size(180,90)};panel.Controls.Add(text);Pump(20);message=WheelMessage(text,text.PointToScreen(new Point(10,10)),-120);Check(!router.PreFilterMessage(ref message),"multiline text keeps its own wheel behavior");text.Dispose();
   }
   panel.Dispose();
   var field=new DateField{Location=new Point(20,20),Value=new DateTime(2026,1,31)};form.Controls.Add(field);Ui.Dark=false;Ui.Apply(field);field.OpenCalendar();Pump(20);var popup=Popup(field);var calendar=(CalendarPicker)((ToolStripControlHost)popup.Items[0]).Control;
   calendar.NavigateMonth(1);Check(calendar.SelectedDate==new DateTime(2026,2,28),"January 31 navigation clamps to February 28");Key(calendar,Keys.Enter);Pump(20);Check(field.Value==new DateTime(2026,2,28)&&popup.IsDisposed,"Enter confirms date and disposes popup");
   field.OpenCalendar();Pump(20);popup=Popup(field);calendar=(CalendarPicker)((ToolStripControlHost)popup.Items[0]).Control;DateTime unchanged=field.Value;Key(calendar,Keys.Right);Key(calendar,Keys.Escape);Pump(20);Check(field.Value==unchanged&&popup.IsDisposed,"Escape closes without changing the filter");
   field.OpenCalendar();Pump(20);popup=Popup(field);calendar=(CalendarPicker)((ToolStripControlHost)popup.Items[0]).Control;calendar.SelectedDate=new DateTime(2026,10,1);Invoke(calendar,"OnMouseClick",new MouseEventArgs(MouseButtons.Left,1,33,188,0));Pump(20);Check(field.Value==new DateTime(2026,10,11)&&popup.IsDisposed,"mouse selects exact day and closes safely");
   field.OpenCalendar();Pump(20);popup=Popup(field);calendar=(CalendarPicker)((ToolStripControlHost)popup.Items[0]).Control;Invoke(calendar,"OnMouseClick",new MouseEventArgs(MouseButtons.Left,1,80,calendar.Height-34,0));Pump(20);Check(field.Value==DateTime.Today&&popup.IsDisposed,"Today button selects and closes safely");
   field.OpenCalendar();Pump(20);popup=Popup(field);calendar=(CalendarPicker)((ToolStripControlHost)popup.Items[0]).Control;calendar.SelectedDate=new DateTime(2024,2,28);Key(calendar,Keys.Right);Check(calendar.SelectedDate==new DateTime(2024,2,29),"keyboard respects leap day");Key(calendar,Keys.Right);Check(calendar.SelectedDate==new DateTime(2024,3,1),"keyboard crosses month boundary");
   calendar.SelectedDate=DateTime.MinValue;calendar.NavigateMonth(-1);Key(calendar,Keys.Left);Check(calendar.SelectedDate==DateTime.MinValue,"minimum date boundary is safe");calendar.SelectedDate=DateTime.MaxValue.Date;calendar.NavigateMonth(1);Key(calendar,Keys.Right);Check(calendar.SelectedDate==DateTime.MaxValue.Date,"maximum date boundary is safe");
   Ui.Dark=true;field.ThemeChanged();Check(calendar.BackColor==Ui.Card,"open calendar follows dark theme");Key(calendar,Keys.Home);Check(calendar.SelectedDate==DateTime.Today,"Home selects today");Key(calendar,Keys.Escape);
   for(int i=0;i<12;i++){field.OpenCalendar();Pump(5);popup=Popup(field);popup.Close();Pump(10);Check(popup.IsDisposed,"popup lifecycle "+i);}
   var area=Screen.FromControl(form).WorkingArea;form.Location=new Point(area.Right-form.Width,area.Bottom-form.Height);field.Location=new Point(form.ClientSize.Width-field.Width,form.ClientSize.Height-field.Height);Pump(30);field.OpenCalendar();Pump(30);popup=Popup(field);Check(area.Contains(popup.Bounds),"calendar remains inside screen near bottom-right corner");field.Dispose();Check(popup.IsDisposed,"disposing date field closes its popup");
   form.Close();
  }
  Ui.Dark=false;using(var animator=new ScrollAnimator(()=>0,()=>100,value=>{})){Check(!animator.Moving,"scroll timer is inactive while idle");animator.Scroll(-120,100);animator.Cancel();Check(!animator.Moving,"cancel releases active scroll timer");}
  Console.WriteLine("PASS "+passed+" visual behavior checks");
 }catch(Exception ex){Console.Error.WriteLine("FAIL visual "+ex);Environment.Exit(1);}}
}
