using System;
using System.Drawing;
using System.Drawing.Drawing2D;
namespace PaoCaixa {
 public static class Icons {
  public static void Draw(Graphics g,string name,Rectangle r,Color color) {
   if(r.Width<=0||r.Height<=0)return;
   var state=g.Save();g.TranslateTransform(r.X,r.Y);g.ScaleTransform(r.Width/24f,r.Height/24f);
   using(var p=new Pen(color,1.45f)){p.StartCap=p.EndCap=LineCap.Round;p.LineJoin=LineJoin.Round;
    switch(name) {
     case "bread":case "wheat":
      g.DrawLine(p,6,22,20,2);g.DrawBezier(p,9,18,3,18,2,15,4,11);g.DrawBezier(p,9,18,14,20,17,17,15,13);g.DrawBezier(p,13,12,7,12,6,9,8,5);g.DrawBezier(p,13,12,18,14,21,11,19,7);g.DrawBezier(p,16,7,13,4,15,1,19,1);break;
     case "coffee":
      g.DrawLine(p,7,2,7,5);g.DrawLine(p,12,1,12,5);g.DrawLine(p,17,2,17,5);g.DrawRectangle(p,3,8,14,10);g.DrawArc(p,14,8,7,7,-90,180);g.DrawLine(p,2,22,20,22);break;
     case "juice":case "glass":
      g.DrawLines(p,new[]{new PointF(4,6),new PointF(6,21),new PointF(17,21),new PointF(19,6),new PointF(4,6)});g.DrawLine(p,6,12,17,12);g.DrawLines(p,new[]{new PointF(12,11),new PointF(15,1),new PointF(22,1)});break;
     case "croissant":
      g.DrawBezier(p,2,16,4,6,19,5,22,16);g.DrawBezier(p,2,16,4,23,8,22,8,15);g.DrawBezier(p,22,16,20,23,16,22,16,15);g.DrawArc(p,7,8,10,12,0,180);g.DrawLine(p,8,9,10,19);g.DrawLine(p,15,9,14,19);break;
     case "cake":
      g.DrawLines(p,new[]{new PointF(3,10),new PointF(21,10),new PointF(21,21),new PointF(3,21),new PointF(3,10)});g.DrawBezier(p,3,14,7,19,8,10,12,15);g.DrawBezier(p,12,15,16,20,17,10,21,14);g.DrawLine(p,7,6,7,10);g.DrawLine(p,12,5,12,10);g.DrawLine(p,17,6,17,10);g.DrawLine(p,7,2,7,3);g.DrawLine(p,12,1,12,2);g.DrawLine(p,17,2,17,3);break;
     case "sandwich":
      g.DrawPolygon(p,new[]{new PointF(3,18),new PointF(12,3),new PointF(22,18)});g.DrawLine(p,3,22,22,22);g.DrawLine(p,6,15,19,15);g.DrawLine(p,8,11,15,11);break;
     case "basket":
      g.DrawLines(p,new[]{new PointF(2,9),new PointF(6,21),new PointF(18,21),new PointF(22,9),new PointF(2,9)});g.DrawLine(p,7,9,11,2);g.DrawLine(p,17,9,13,2);g.DrawLine(p,9,12,10,18);g.DrawLine(p,15,12,14,18);break;
     case "dashboard":
      g.DrawRectangle(p,3,3,7,7);g.DrawRectangle(p,14,3,7,7);g.DrawRectangle(p,3,14,7,7);g.DrawRectangle(p,14,14,7,7);break;
     case "package":
      g.DrawPolygon(p,new[]{new PointF(3,7),new PointF(12,2),new PointF(21,7),new PointF(21,17),new PointF(12,22),new PointF(3,17)});g.DrawLine(p,3,7,12,12);g.DrawLine(p,21,7,12,12);g.DrawLine(p,12,12,12,22);g.DrawLine(p,8,4,17,9);break;
     case "wallet":
      g.DrawRectangle(p,2,5,19,15);g.DrawRectangle(p,15,10,7,6);g.DrawLine(p,3,5,18,2);g.DrawEllipse(p,17,12,1,1);break;
     case "receipt":
      g.DrawLines(p,new[]{new PointF(4,22),new PointF(4,2),new PointF(20,2),new PointF(20,22),new PointF(16,20),new PointF(12,22),new PointF(8,20),new PointF(4,22)});g.DrawLine(p,8,7,16,7);g.DrawLine(p,8,12,16,12);g.DrawLine(p,8,16,14,16);break;
     case "chart":
      g.DrawLine(p,3,3,3,21);g.DrawLine(p,3,21,22,21);g.DrawLine(p,8,17,8,12);g.DrawLine(p,13,17,13,8);g.DrawLine(p,18,17,18,4);break;
     case "search":
      g.DrawEllipse(p,2,2,14,14);g.DrawLine(p,14,14,22,22);break;
     case "settings":
      g.DrawEllipse(p,7,7,10,10);g.DrawEllipse(p,10,10,4,4);for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(p,12+(float)Math.Cos(a)*7,12+(float)Math.Sin(a)*7,12+(float)Math.Cos(a)*10,12+(float)Math.Sin(a)*10);}break;
     case "lock":
      g.DrawRectangle(p,4,10,16,12);g.DrawArc(p,7,2,10,14,180,180);g.DrawLine(p,12,15,12,18);break;
     case "user":
      g.DrawEllipse(p,8,2,8,8);g.DrawArc(p,3,13,18,16,180,180);break;
     case "logout":
      g.DrawLines(p,new[]{new PointF(9,3),new PointF(3,3),new PointF(3,21),new PointF(9,21)});g.DrawLine(p,9,12,22,12);g.DrawLines(p,new[]{new PointF(17,7),new PointF(22,12),new PointF(17,17)});break;
     case "storage":
      g.DrawRectangle(p,3,3,18,18);g.DrawLine(p,3,15,21,15);g.DrawEllipse(p,16,17,2,2);g.DrawLine(p,6,18,11,18);break;
     case "scan":
      g.DrawLines(p,new[]{new PointF(2,7),new PointF(2,2),new PointF(7,2)});g.DrawLines(p,new[]{new PointF(17,2),new PointF(22,2),new PointF(22,7)});g.DrawLines(p,new[]{new PointF(2,17),new PointF(2,22),new PointF(7,22)});g.DrawLines(p,new[]{new PointF(17,22),new PointF(22,22),new PointF(22,17)});for(int i=6;i<20;i+=3)g.DrawLine(p,i,7,i,17);break;
     case "check":g.DrawLines(p,new[]{new PointF(4,12),new PointF(10,18),new PointF(21,5)});break;
     case "plus":g.DrawLine(p,12,4,12,20);g.DrawLine(p,4,12,20,12);break;
     case "minus":g.DrawLine(p,4,12,20,12);break;
     case "close":g.DrawLine(p,5,5,19,19);g.DrawLine(p,19,5,5,19);break;
     case "arrow-left":g.DrawLine(p,3,12,21,12);g.DrawLines(p,new[]{new PointF(9,6),new PointF(3,12),new PointF(9,18)});break;
     case "calendar":g.DrawRectangle(p,3,5,18,16);g.DrawLine(p,3,10,21,10);g.DrawLine(p,8,2,8,7);g.DrawLine(p,16,2,16,7);g.DrawRectangle(p,7,14,3,3);break;
     case "sun":g.DrawEllipse(p,7,7,10,10);for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(p,12+(float)Math.Cos(a)*8,12+(float)Math.Sin(a)*8,12+(float)Math.Cos(a)*11,12+(float)Math.Sin(a)*11);}break;
     case "moon":g.DrawBezier(p,19,18,3,23,0,8,10,3);g.DrawBezier(p,10,3,7,15,15,20,19,18);break;
     case "arrow":g.DrawLine(p,3,12,21,12);g.DrawLines(p,new[]{new PointF(15,6),new PointF(21,12),new PointF(15,18)});break;
     case "up":g.DrawLines(p,new[]{new PointF(5,19),new PointF(19,5),new PointF(7,5),new PointF(19,5),new PointF(19,17)});break;
     case "chevron":g.DrawLines(p,new[]{new PointF(9,5),new PointF(16,12),new PointF(9,19)});break;
     case "trash":g.DrawLine(p,3,5,21,5);g.DrawRectangle(p,6,5,12,16);g.DrawLine(p,9,2,15,2);g.DrawLine(p,10,9,10,17);g.DrawLine(p,14,9,14,17);break;
     case "download":g.DrawLine(p,12,2,12,16);g.DrawLines(p,new[]{new PointF(6,10),new PointF(12,16),new PointF(18,10)});g.DrawLines(p,new[]{new PointF(3,16),new PointF(3,22),new PointF(21,22),new PointF(21,16)});break;
     case "help":g.DrawEllipse(p,2,2,20,20);g.DrawArc(p,8,6,8,7,180,250);g.DrawLine(p,12,12,12,14);g.DrawEllipse(p,11.5f,17,1,1);break;
     case "pix":g.DrawPolygon(p,new[]{new PointF(12,2),new PointF(22,12),new PointF(12,22),new PointF(2,12)});g.DrawLines(p,new[]{new PointF(4,12),new PointF(8,12),new PointF(12,8),new PointF(16,12),new PointF(20,12)});break;
     case "card":g.DrawRectangle(p,2,5,20,15);g.DrawLine(p,2,10,22,10);g.DrawLine(p,6,16,10,16);break;
     default:g.DrawEllipse(p,3,3,18,18);g.DrawLine(p,12,7,12,13);g.DrawEllipse(p,11.5f,17,1,1);break;
    }
   }g.Restore(state);
  }
 }
}
