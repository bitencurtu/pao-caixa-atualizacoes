using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace PaoCaixa {
 class CatalogPhotoTests {
  static int count;
  static void Check(bool pass,string label){if(!pass)throw new Exception("CATALOG_FAILED "+label);count++;Console.WriteLine("CATALOG_PASS "+label);}
  [STAThread]static void Main(string[] args){
   var data=J.Read<State>(Store.Resource("seed.json"));
   var sample=J.Read<List<Product>>(Store.Resource("catalog-extras.json"));
   Check(data.products.Count==46,"46 bakery products on first installation");
   Check(sample.Count==30,"30 supplementary products");
   Check(data.products.Select(x=>x.code).Distinct(StringComparer.OrdinalIgnoreCase).Count()==46,"unique product codes");
   Check(data.products.Select(x=>x.photoKey).Distinct().Count()==46,"each standard product has own photo");
   Check(sample.All(x=>x.stock==0&&x.cost==0),"new products never create imaginary inventory or costs");
   foreach(var item in data.products){
    Check(ProductPhotos.HasPhoto(item),"photo mapped for "+item.name);
    using(var img=new Bitmap(180,126))
    using(var g=Graphics.FromImage(img)){
     g.Clear(Color.White);
     Check(ProductPhotos.Draw(g,item,new Rectangle(0,0,180,126)),"offline image renders for "+item.name);
     var color=img.GetPixel(80,45);
     Check(color.R!=255||color.G!=255||color.B!=255,"photo resource paints pixels for "+item.name);
    }
   }
   using(var db=new Store(args[0])){
    db.Login(Store.OwnerEmail,"Teste-Pdv-2026");
    var before=db.Products();
    Check(before.Count==16,"existing store initially has legacy catalog");
    var previous=before.First();
    long previousPrice=previous.price;
    int added=db.ImportExampleProducts();
    Check(added==30,"imports only missing 30 items");
    Check(db.Products().Count==46,"legacy store now has 46 products");
    Check(db.ImportExampleProducts()==0,"second import never duplicates products");
    var after=db.Products();
    Check(after.First(x=>x.id==previous.id).price==previousPrice,"original price untouched");
    Check(after.Where(x=>x.code.StartsWith("PXC")).All(x=>x.stock==0),"new products have zero stock");
    var chosen=after.First(x=>x.code=="PXC101");
    Check(chosen.photoKey=="ex-101","added product retains corresponding photo association");
    chosen.name="Pão de forma da casa";
    db.SaveProduct(chosen);
    Check(db.Products().First(x=>x.id==chosen.id).photoKey=="ex-101","photo association persists after rename");
   }
   Console.WriteLine("CATALOG_PHOTOS_TESTS_DONE "+count);
  }
 }
}
