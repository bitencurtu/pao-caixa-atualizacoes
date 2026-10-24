using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PaoCaixa;
class Performance {
 static void Main(string[] args){var sw=Stopwatch.StartNew();using(var db=new Store(args[0])){Console.WriteLine("Primeira migração e validação de 10000 vendas: "+sw.ElapsedMilliseconds+" ms");sw.Restart();db.Login(Store.OwnerEmail,"Teste-Pdv-2026");Console.WriteLine("Login scrypt compatível: "+sw.ElapsedMilliseconds+" ms");var p=db.Products().First(x=>x.code=="002");sw.Restart();for(int i=0;i<20;i++)db.Finish(new List<Line>{new Line{productId=p.id,quantity=1000}},0,new List<Payment>{new Payment{method="dinheiro",amount=p.price}},p.price,"");Console.WriteLine("20 vendas sobre histórico de 10000: "+sw.ElapsedMilliseconds+" ms");sw.Restart();var page=db.Sales(DateTime.Today.AddYears(-1),DateTime.Today.AddDays(1));Console.WriteLine("Consulta de uma página: "+sw.ElapsedMilliseconds+" ms / "+page.Count+" registros");Console.WriteLine("Memória gerenciada depois de GC: "+(GC.GetTotalMemory(true)/1024/1024)+" MiB");}sw.Restart();using(var db=new Store(args[0]))Console.WriteLine("Reabertura de banco nativo: "+sw.ElapsedMilliseconds+" ms");}
}
