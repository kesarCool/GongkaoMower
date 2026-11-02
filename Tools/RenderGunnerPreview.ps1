param([string]$Sheet, [string]$Output)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$source = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
public class GunnerPreview {
 static Bitmap sheet;
 static void Part(Graphics g, Rectangle src, float x, float y, float w, float h, float angle, float px, float py) {
  var s=g.Save(); g.TranslateTransform(x,y); g.RotateTransform(angle);
  g.DrawImage(sheet,new RectangleF(-px*w,-py*h,w,h),src,GraphicsUnit.Pixel); g.Restore(s);
 }
 static void Leg(Graphics g,bool near,double phase,float bx,float by) {
  float a=(float)Math.Sin(phase)*27;
  var s=g.Save(); g.TranslateTransform(bx,by); g.RotateTransform(a);
  Part(g,near?new Rectangle(837,739,154,153):new Rectangle(115,728,173,184),0,0,30,35,0,.5f,.12f);
  float knee=(float)Math.Max(0,-Math.Sin(phase))*28;
  Part(g,near?new Rectangle(1138,728,209,181):new Rectangle(476,739,184,170),0,25,38,34,knee,.5f,.12f);
  g.Restore(s);
 }
 public static void Render(string input,string output) {
  sheet=new Bitmap(input); Directory.CreateDirectory(output);
  for(int i=0;i<72;i++) {
   double t=i/18.0; bool left=i>=36; double phase=t*2*Math.PI*2.5;
   double shot=(t%.55); float recoil=shot<.13?(float)(Math.Sin(shot/.13*Math.PI)*7):0;
   float bob=(float)Math.Cos(phase*2)*2.0f;
   using(var bmp=new Bitmap(512,512)) using(var g=Graphics.FromImage(bmp)) {
    g.SmoothingMode=SmoothingMode.AntiAlias; g.InterpolationMode=InterpolationMode.HighQualityBicubic;
    g.Clear(Color.FromArgb(29,38,43));
    using(var pen=new Pen(Color.FromArgb(43,55,59),1)) for(int n=0;n<512;n+=32){g.DrawLine(pen,n,75,n,450);g.DrawLine(pen,0,n,512,n);}
    using(var f=new Font("Arial",17,FontStyle.Bold)) using(var b=new SolidBrush(Color.FromArgb(235,239,226))) g.DrawString("GUNNER / RUN + FIRE",f,b,22,18);
    using(var f=new Font("Arial",11)) using(var b=new SolidBrush(Color.FromArgb(158,178,179))) {
     g.DrawString(left?"FACING LEFT":"FACING RIGHT",f,b,22,48);
     g.DrawString("Cutout motion study / not a Unity capture",f,b,22,478);
    }
    using(var b=new SolidBrush(Color.FromArgb(100,0,0,0)))g.FillEllipse(b,186,353,146,23);
    var root=g.Save();g.TranslateTransform(256,268);if(left)g.ScaleTransform(-1,1);
    Leg(g,false,phase+Math.PI,-13,39);
    Part(g,new Rectangle(487,929,205,179),-39,-94+bob,67,48,(float)Math.Sin(phase)*5,.85f,.4f);
    Part(g,new Rectangle(824,222,242,185),0,-8+bob,87,65,-2,.5f,.5f);
    Part(g,new Rectangle(1127,277,217,125),0,30+bob,84,44,0,.5f,.5f);
    Leg(g,true,phase,13,39);
    Part(g,new Rectangle(450,89,333,322),-1,-42+bob,134,130,-2-recoil*.15f,.5f,.85f);
    var aim=g.Save();g.TranslateTransform(-recoil,-8+bob);
    Part(g,new Rectangle(147,465,124,194),-23,0,35,47,-52,.5f,.16f);
    Part(g,new Rectangle(435,496,252,148),-13,20,63,35,-8,.12f,.5f);
    Part(g,new Rectangle(35,918,373,190),12,7,113,58,-recoil*.45f,.36f,.5f);
    Part(g,new Rectangle(832,467,170,188),-20,0,40,45,-58,.5f,.15f);
    Part(g,new Rectangle(1112,520,232,137),-17,20,58,34,-6,.12f,.5f);
    if(shot<.07) {
     using(var b=new SolidBrush(Color.FromArgb(255,220,104)))g.FillPolygon(b,new PointF[]{new PointF(86,-3),new PointF(102,-14),new PointF(98,-3),new PointF(119,4),new PointF(97,8),new PointF(103,20),new PointF(86,12)});
     using(var pen=new Pen(Color.FromArgb(245,185,70),3))g.DrawLine(pen,123,5,166,5);
    }
    g.Restore(aim);g.Restore(root);
    bmp.Save(Path.Combine(output,"frame-"+i.ToString("D3")+".png"),ImageFormat.Png);
   }
  }
  sheet.Dispose();
 }
}
'@
Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
[GunnerPreview]::Render($Sheet,$Output)
