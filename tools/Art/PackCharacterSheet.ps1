param(
    [Parameter(Mandatory=$true)][string]$Source,
    [Parameter(Mandatory=$true)][string]$Destination,
    [int]$FrameWidth=32,
    [int]$FrameHeight=32,
    [int]$Columns=10,
    [int]$Rows=3,
    [int]$HorizontalPadding=3,
    [int]$VerticalPadding=4
)

# Normalize separated generated poses into the import grid. This does not author new art.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not ('NeoSheetPacker' -as [type])) {
$drawingReferences = @([System.Drawing.Bitmap].Assembly.Location, [System.Drawing.Point].Assembly.Location)
if (Test-Path -LiteralPath "$PSHOME/ref") {
    $drawingReferences += @(Get-ChildItem "$PSHOME/ref/*.dll" | Select-Object -ExpandProperty FullName)
    $drawingReferences += @(Get-ChildItem "$PSHOME/System.Private.Windows*.dll" | Select-Object -ExpandProperty FullName)
}
Add-Type -ReferencedAssemblies ($drawingReferences | Select-Object -Unique) -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class NeoSheetPacker {
    static List<Point> Bands(bool[] mask) {
        var result = new List<Point>(); int start = -1;
        for (int i=0; i<=mask.Length; i++) {
            bool filled = i<mask.Length && mask[i];
            if (filled && start<0) start=i;
            if (!filled && start>=0) { result.Add(new Point(start,i-1)); start=-1; }
        }
        return result;
    }
    public static string Pack(string source, string destination, int fw, int fh, int cols, int rows, int padX, int padY) {
        using (var image=new Bitmap(source)) {
            var ys=new bool[image.Height];
            for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++)
                if(image.GetPixel(x,y).A>=64) {ys[y]=true;break;}
            var rowBands=Bands(ys);
            if(rowBands.Count!=rows) throw new Exception("Expected "+rows+" separated pose rows, got "+rowBands.Count);
            var rects=new List<Rectangle>(); int maxW=0,maxH=0;
            foreach(var band in rowBands) {
                var xs=new bool[image.Width];
                for(int x=0;x<image.Width;x++) for(int y=band.X;y<=band.Y;y++)
                    if(image.GetPixel(x,y).A>=64) {xs[x]=true;break;}
                var columns=Bands(xs);
                if(columns.Count!=cols) throw new Exception("Expected "+cols+" separated poses in row, got "+columns.Count);
                foreach(var column in columns) {
                    int top=band.Y,bottom=band.X;
                    for(int x=column.X;x<=column.Y;x++) for(int y=band.X;y<=band.Y;y++)
                        if(image.GetPixel(x,y).A>=64) {top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
                    // Include the faint outer edge, while staying inside the transparent separation.
                    var rect=Rectangle.FromLTRB(Math.Max(0,column.X-1),Math.Max(0,top-1),Math.Min(image.Width,column.Y+2),Math.Min(image.Height,bottom+2));
                    rects.Add(rect); maxW=Math.Max(maxW,rect.Width); maxH=Math.Max(maxH,rect.Height);
                }
            }
            double scale=Math.Min((fw-padX*2.0)/maxW,(fh-padY*2.0)/maxH);
            using(var output=new Bitmap(fw*cols,fh*rows,PixelFormat.Format32bppArgb)) {
                using(var g=Graphics.FromImage(output)) {
                    g.Clear(Color.Transparent); g.InterpolationMode=InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode=PixelOffsetMode.Half; g.CompositingMode=CompositingMode.SourceCopy;
                    for(int i=0;i<rects.Count;i++) {
                        int width=(int)Math.Round(rects[i].Width*scale),height=(int)Math.Round(rects[i].Height*scale);
                        var cell=new Rectangle(i%cols*fw,i/cols*fh,fw,fh); g.SetClip(cell);
                        var target=new Rectangle(cell.X+(fw-width)/2,cell.Bottom-padY-height,width,height);
                        g.DrawImage(image,target,rects[i],GraphicsUnit.Pixel);
                    }
                }
                output.Save(destination,ImageFormat.Png);
            }
            return rects.Count+" complete poses packed at a uniform scale; transparent cell margins and aligned feet.";
        }
    }
}
'@
}
[NeoSheetPacker]::Pack((Resolve-Path -LiteralPath $Source).Path, [System.IO.Path]::GetFullPath($Destination),$FrameWidth,$FrameHeight,$Columns,$Rows,$HorizontalPadding,$VerticalPadding)
