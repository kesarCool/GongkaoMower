param([string]$Sheet, [string]$Output)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$source = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
public class GunnerPreviewV2 {
 static Bitmap sheet;
 static void Part(Graphics g, Rectangle src, float x, float y, float w, float h, float angle, float px, float py) {
  var s=g.Save(); g.TranslateTransform(x,y); g.RotateTransform(angle);
  g.DrawImage(sheet,new RectangleF(-px*w,-py*h,w,h),src,GraphicsUnit.Pixel); g.Restore(s);
 }
 static void Leg(Graphics g,bool near,double phase,float bx,float by) {
  // Foot target: 58% grounded stance, followed by an arcing recovery.
  // Solve the knee from the hip/ankle triangle instead of swinging it independently.
  double p=phase/(Math.PI*2); p-=Math.Floor(p);
  double stance=.58, u=p<stance?p/stance:(p-stance)/(1-stance);
  float fx=(float)(p<stance?18-36*u:-18+36*(u*u*(3-2*u)));
  float lift=p<stance?0:(float)(Math.Sin(u*Math.PI)*18);
  float fy=83-lift;
  double dx=fx-bx, dy=fy-by, l1=27, l2=25;
  double d=Math.Min(l1+l2-.1,Math.Max(5,Math.Sqrt(dx*dx+dy*dy)));
  double axis=Math.Atan2(dy,dx);
  double spread=Math.Acos(Math.Max(-1,Math.Min(1,(l1*l1+d*d-l2*l2)/(2*l1*d))));
  double theta=axis-spread;
  float kx=bx+(float)(Math.Cos(theta)*l1), ky=by+(float)(Math.Sin(theta)*l1);
  float thighAngle=(float)(theta*180/Math.PI-90);
  float shinAngle=(float)(Math.Atan2(fy-ky,fx-kx)*180/Math.PI-90);
  Part(g,near?new Rectangle(837,739,154,153):new Rectangle(115,728,173,184),bx,by,29,33,thighAngle,.5f,.1f);
  Part(g,near?new Rectangle(1152,738,151,92):new Rectangle(493,748,140,88),kx,ky,26,29,shinAngle,.5f,.08f);
  // Independent boot keeps the sole level while the knee bends.
  float toe=p<stance?(u>.8?(float)((u-.8)/.2*-12):0):(float)(Math.Sin(u*Math.PI)*-10);
  Part(g,near?new Rectangle(1138,823,209,86):new Rectangle(476,818,184,91),fx,fy,39,18,toe,.5f,.35f);
 }
 public static void Render(string input,string output) {
  sheet=new Bitmap(input); Directory.CreateDirectory(output);
  for(int i=0;i<144;i++) {
   double t=i/24.0; bool left=i>=72; double phase=t*2*Math.PI*2;
   double shot=(t%.5); float recoil=shot<.16?(float)(Math.Exp(-shot*22)*7):0;
   float bob=(float)(Math.Cos(phase*2)*2.8);
   using(var bmp=new Bitmap(512,512)) using(var g=Graphics.FromImage(bmp)) {
    g.SmoothingMode=SmoothingMode.AntiAlias; g.InterpolationMode=InterpolationMode.HighQualityBicubic;
    g.Clear(Color.FromArgb(29,38,43));
    using(var pen=new Pen(Color.FromArgb(43,55,59),1)) for(int n=0;n<512;n+=32){g.DrawLine(pen,n,75,n,450);g.DrawLine(pen,0,n,512,n);}
    using(var f=new Font("Arial",17,FontStyle.Bold)) using(var b=new SolidBrush(Color.FromArgb(235,239,226))) g.DrawString("GUNNER / FOOT IK STUDY",f,b,22,18);
    using(var f=new Font("Arial",11)) using(var b=new SolidBrush(Color.FromArgb(158,178,179))) {
     g.DrawString(left?"FACING LEFT":"FACING RIGHT",f,b,22,48);
     g.DrawString("Cutout motion study / not a Unity capture",f,b,22,478);
    }
    using(var b=new SolidBrush(Color.FromArgb(100,0,0,0)))g.FillEllipse(b,194,354,124,14);
    using(var pen=new Pen(Color.FromArgb(103,125,124),2))g.DrawLine(pen,90,363,422,363);
    var root=g.Save();g.TranslateTransform(256,268);if(left)g.ScaleTransform(-1,1);
    Leg(g,false,phase+Math.PI,-8,39+bob);
    Part(g,new Rectangle(487,929,205,179),-39,-94+bob,67,48,(float)Math.Sin(phase)*5,.85f,.4f);
    Part(g,new Rectangle(824,222,242,185),0,-8+bob,87,65,-2,.5f,.5f);
    Part(g,new Rectangle(1127,277,217,125),0,30+bob,84,44,0,.5f,.5f);
    Leg(g,true,phase,8,39+bob);
    Part(g,new Rectangle(450,89,333,322),2-recoil*.28f,-42+bob,130,126,-3-recoil*.3f,.5f,.85f);
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
[GunnerPreviewV2]::Render($Sheet,$Output)
