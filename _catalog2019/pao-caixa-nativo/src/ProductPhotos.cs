using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PaoCaixa {
 // Uma única imagem de catálogo embutida no EXE, sem acesso a rede e
 // sem carregar 46 bitmaps separados na memória dos computadores fracos.
 public static class ProductPhotos {
  const int Columns=8,TileWidth=180,TileHeight=126;
  static readonly Dictionary<string,int> Slots=BuildSlots();
  static readonly Dictionary<string,string> ExistingNames=new Dictionary<string,string>(StringComparer.CurrentCultureIgnoreCase){
   {"Pão francês","seed-01"},{"Pão de queijo","seed-02"},{"Croissant tradicional","seed-03"},
   {"Pão na chapa","seed-04"},{"Café espresso","seed-05"},{"Café com leite","seed-06"},
   {"Coxinha de frango","seed-07"},{"Bolo de cenoura","seed-08"},{"Sonho de creme","seed-09"},
   {"Suco de laranja","seed-10"},{"Misto quente","seed-11"},{"Água mineral 500 ml","seed-12"},
   {"Baguete artesanal","seed-13"},{"Cookie de chocolate","seed-14"},
   {"Empada de frango","seed-15"},{"Pão integral","seed-16"}
  };
  static readonly object Sync=new object();
  static Image atlas;
  static bool attempted;
  static Dictionary<string,int> BuildSlots(){
   var d=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
   for(int i=0;i<16;i++)d["seed-"+(i+1).ToString("00")]=i;
   for(int i=0;i<30;i++)d["ex-"+(101+i)]=16+i;
   return d;
  }
  public static bool HasPhoto(Product product){
   if(product==null)return false;
   int ignored;return Slots.TryGetValue(ResolveKey(product),out ignored);
  }
  static string ResolveKey(Product product){
   if(!string.IsNullOrEmpty(product.photoKey))return product.photoKey;
   string name=product.name??"";
   string key;return ExistingNames.TryGetValue(name,out key)?key:"";
  }
  static Image Atlas(){
   if(attempted)return atlas;
   lock(Sync){
    if(attempted)return atlas;
    try{
     using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("catalog-photos.jpg")){
      if(stream!=null)using(var image=Image.FromStream(stream))atlas=new Bitmap(image);
     }
    }catch{atlas=null;}
    attempted=true;
    return atlas;
   }
  }
  public static bool Draw(Graphics g,Product product,Rectangle box){
   int slot;
   if(product==null||box.Width<=0||box.Height<=0||!Slots.TryGetValue(ResolveKey(product),out slot))return false;
   var image=Atlas();
   if(image==null)return false;
   var source=new Rectangle(slot%Columns*TileWidth,slot/Columns*TileHeight,TileWidth,TileHeight);
   if(source.Right>image.Width||source.Bottom>image.Height)return false;
   var state=g.Save();
   try{
    g.SetClip(box);
    g.InterpolationMode=InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode=PixelOffsetMode.HighQuality;
    g.DrawImage(image,box,source,GraphicsUnit.Pixel);
   }finally{g.Restore(state);}
   return true;
  }
 }
}