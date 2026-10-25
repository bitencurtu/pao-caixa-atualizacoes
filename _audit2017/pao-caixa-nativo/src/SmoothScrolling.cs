using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
namespace PaoCaixa {
 internal interface ISmoothScrollHost {bool ScrollWheel(int delta);}
 // One timer per moving viewport. The monotonic clock is unaffected by clock changes.
 internal sealed class ScrollAnimator:IDisposable {
  readonly Timer timer=new Timer{Interval=16};readonly Stopwatch clock=new Stopwatch();
  readonly Func<int> read,maximum;readonly Action<int> write;double position;int target;bool disposed;
  public bool Adjusting {get;private set;}
  public bool Moving {get{return timer.Enabled;}}
  public ScrollAnimator(Func<int> read,Func<int> maximum,Action<int> write){this.read=read;this.maximum=maximum;this.write=write;timer.Tick+=(s,e)=>Tick();}
  public bool Scroll(int delta,int viewport){
   if(disposed||delta==0)return false;int lines=SystemInformation.MouseWheelScrollLines;if(lines==0)return false;
   int current=read(),max=Math.Max(0,maximum());double distance=delta/120d*(lines<0?Math.Max(24,viewport-24):Math.Min(lines,20)*24);
   // A reversal follows the current position immediately instead of finishing the old direction.
   int origin=Moving&&Math.Sign(target-current)==Math.Sign(-delta)?target:current;
   int next=(int)Math.Max(0,Math.Min(max,Math.Round(origin-distance)));
   if(next==current){Cancel();return false;}target=next;
   if(!Moving){position=current;clock.Restart();timer.Start();}return true;
  }
  void Tick(){
   if(disposed)return;target=Math.Max(0,Math.Min(Math.Max(0,maximum()),target));
   double elapsed=Math.Min(100,Math.Max(1,clock.Elapsed.TotalMilliseconds));clock.Restart();
   position+=(target-position)*(1-Math.Exp(-elapsed/85d));bool done=Math.Abs(target-position)<.6;
   int value=done?target:(int)Math.Round(position);value=Math.Max(0,Math.Min(Math.Max(0,maximum()),value));
   Adjusting=true;try{if(read()!=value)write(value);}finally{Adjusting=false;}
   if(done)Cancel();
  }
  public void Cancel(){timer.Stop();clock.Reset();}
  public void Dispose(){if(disposed)return;disposed=true;timer.Dispose();clock.Stop();}
 }
 // Wheel input follows the viewport under the pointer, including its labels and cards.
 // Native dropdowns, editable multiline text and grids keep their own wheel handling.
 internal sealed class SmoothWheelRouter:IMessageFilter,IDisposable {
  readonly Form root;bool disposed;
  public SmoothWheelRouter(Form root){this.root=root;Application.AddMessageFilter(this);}
  public bool PreFilterMessage(ref Message message){
   if(disposed||message.Msg!=0x020A||root.IsDisposed||!root.Visible||!root.Enabled)return false;
   var source=Control.FromChildHandle(message.HWnd);if(source==null||source.TopLevelControl!=root)return false;
   long coordinates=message.LParam.ToInt64();var screen=new Point((short)(coordinates&65535),(short)((coordinates>>16)&65535));
   if(!root.RectangleToScreen(root.ClientRectangle).Contains(screen))return false;
   Control control=root;while(true){var child=control.GetChildAtPoint(control.PointToClient(screen),GetChildAtPointSkip.Invisible);if(child==null)break;control=child;}
   int delta=(short)((message.WParam.ToInt64()>>16)&65535);
   for(var current=control;current!=null;current=current.Parent){
    if(!current.Enabled)return false;
    var text=current as TextBoxBase;if(current is ComboBox||current is DataGridView||current is ToolStrip||(text!=null&&text.Multiline))return false;
    var host=current as ISmoothScrollHost;if(host!=null&&host.ScrollWheel(delta))return true;
   }return false;
  }
  public void Dispose(){if(disposed)return;disposed=true;Application.RemoveMessageFilter(this);}
 }
}
