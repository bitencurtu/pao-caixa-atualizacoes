using System;
using System.Drawing;
using System.Windows.Forms;
namespace PaoCaixa {
 // Paint visible rows in one control instead of creating a window per card.
 public class CanvasView:Panel,ISmoothScrollHost {
  public readonly VScrollBar ScrollBar=new VScrollBar{Dock=DockStyle.Right,Width=16,Visible=false};
  Size virtualSize;
  readonly ScrollAnimator wheel;
  public CanvasView(){DoubleBuffered=true;ResizeRedraw=true;wheel=new ScrollAnimator(()=>ScrollBar.Value,()=>MaximumOffset,value=>ScrollBar.Value=value);Controls.Add(ScrollBar);ScrollBar.ValueChanged+=(sender,e)=>{if(!wheel.Adjusting)wheel.Cancel();Invalidate();};AutoScroll=false;}
  public Size VirtualSize {get{return virtualSize;}set{if(virtualSize==value)return;virtualSize=value;UpdateRange();}}
  public Point ScrollOffset {get{return new Point(0,-ScrollBar.Value);}set{wheel.Cancel();ScrollBar.Value=(int)Math.Max(0,Math.Min(Math.Abs((long)value.Y),MaximumOffset));Invalidate();}}
  int MaximumOffset {get{return Math.Max(0,virtualSize.Height-Math.Max(1,ClientSize.Height));}}
  void UpdateRange(){if(wheel==null)return;wheel.Cancel();int height=Math.Max(1,ClientSize.Height),value=Math.Min(ScrollBar.Value,MaximumOffset);ScrollBar.Minimum=0;ScrollBar.Value=0;ScrollBar.LargeChange=height;ScrollBar.SmallChange=24;ScrollBar.Maximum=Math.Max(height,virtualSize.Height)-1;ScrollBar.Value=value;ScrollBar.Visible=virtualSize.Height>height;Invalidate();}
  protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);UpdateRange();}
  bool ISmoothScrollHost.ScrollWheel(int delta){return ScrollWheel(delta);}
  bool ScrollWheel(int delta){return ScrollBar.Visible&&wheel.Scroll(delta,ClientSize.Height);}
  protected override void OnMouseWheel(MouseEventArgs e){if(!ScrollWheel(e.Delta)){base.OnMouseWheel(e);return;}var handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;}
  protected override void Dispose(bool disposing){if(disposing)wheel.Dispose();base.Dispose(disposing);}
 }
}
