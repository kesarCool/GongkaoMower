param([string]$Sheet,[string]$Output)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$source=@'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public class GunnerKeyframesV3 {
 public static void Render(string sheetPath,string output) {
  Directory.CreateDirectory(output);
  using(var sheet=new Bitmap(sheetPath)) {
   Bitmap[] poses=new Bitmap[8]; float[] offsets={0,5,0,-9,0,5,0,-9};
   int[] fronts=new int[8]; int[] tops=new int[8];
   for(int p=0;p<8;p++) {
    int x0=(int)Math.Round((p%4)*sheet.Width/4.0),x1=(int)Math.Round((p%4+1)*sheet.Width/4.0);
    int y0=(int)Math.Round((p/4)*sheet.Height/2.0),y1=(int)Math.Round((p/4+1)*sheet.Height/2.0);
    poses[p]=sheet.Clone(new Rectangle(x0,y0,x1-x0,y1-y0),PixelFormat.Format32bppArgb);
    // Distinguish the far leg consistently between the two halves of the stride.
    int hip=(int)(poses[p].Width*.51);
    for(int yy=330;yy<poses[p].Height;yy++)for(int xx=0;xx<poses[p].Width;xx++) {
     bool far=p<4?xx<hip:xx>hip;
     Color c=poses[p].GetPixel(xx,yy);
     if(far && c.A>0)poses[p].SetPixel(xx,yy,Color.FromArgb(c.A,(int)(c.R*.73),(int)(c.G*.73),(int)(c.B*.73)));
    }
    poses[p].Save(Path.Combine(output,"pose-"+p+".png"),ImageFormat.Png);
    // Register the forehead/hood edge, excluding the gun and trailing ribbon.
    int top=poses[p].Height,front=0;
    for(int y=0;y<(int)(poses[p].Height*.43);y++) for(int x=0;x<poses[p].Width;x++) {
     if(poses[p].GetPixel(x,y).A>100) {top=Math.Min(top,y);front=Math.Max(front,x);}
    }
    tops[p]=top;fronts[p]=front;
   }
   for(int i=0;i<32;i++) {
    int p=i%8; bool left=i>=16;
    using(var bmp=new Bitmap(512,512)) using(var g=Graphics.FromImage(bmp)) {
     g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
     g.Clear(Color.FromArgb(27,36,40));
     using(var pen=new Pen(Color.FromArgb(41,52,57),1)) for(int n=0;n<512;n+=32){g.DrawLine(pen,n,80,n,450);g.DrawLine(pen,0,n,512,n);}
     using(var f=new Font("Arial",16,FontStyle.Bold)) using(var b=new SolidBrush(Color.FromArgb(232,239,223)))g.DrawString("RUN / REDRAWN KEY POSES",f,b,20,18);
     using(var f=new Font("Arial",11))using(var b=new SolidBrush(Color.FromArgb(160,181,181))) {
      g.DrawString(left?"FACING LEFT":"FACING RIGHT",f,b,20,47);
      g.DrawString("8-frame motion trial / not a Spine export",f,b,20,479);
     }
     using(var b=new SolidBrush(Color.FromArgb(85,0,0,0)))g.FillEllipse(b,173,406,168,15);
     using(var pen=new Pen(Color.FromArgb(87,109,108),1))g.DrawLine(pen,85,419,427,419);
     var state=g.Save();g.TranslateTransform(256,0);if(left)g.ScaleTransform(-1,1);
     float scale=.78f;
     float x=-74-(fronts[0])*scale;
     // Face front remains just to the right of the root, with intentional weight shift.
     x+=174;
     float y=113-tops[0]*scale+offsets[p];
     // Keep the identity, face, hands and firearm locked to one painted reference.
     // Only the newly painted lower-body poses change; both share the same waist anchor.
     int seam=330;
     g.DrawImage(poses[p],new RectangleF(x,y+seam*scale,poses[p].Width*scale,(poses[p].Height-seam)*scale),
       new Rectangle(0,seam,poses[p].Width,poses[p].Height-seam),GraphicsUnit.Pixel);
     g.DrawImage(poses[0],new RectangleF(x,y,poses[0].Width*scale,340*scale),
       new Rectangle(0,0,poses[0].Width,340),GraphicsUnit.Pixel);
     g.Restore(state);
     bmp.Save(Path.Combine(output,"frame-"+i.ToString("D3")+".png"),ImageFormat.Png);
    }
   }
   foreach(var p in poses)p.Dispose();
  }
 }
}
'@
Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
[GunnerKeyframesV3]::Render($Sheet,$Output)
