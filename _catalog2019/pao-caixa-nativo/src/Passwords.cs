using System;
using System.Security.Cryptography;
using System.Text;
namespace PaoCaixa {
 // RFC 7914 scrypt. Exactly compatible with Node's previous UTF-8 salt and N/r/p.
 // Runs on the worker thread, never on the Windows message loop.
 public static class Passwords {
  public static string Salt(){var b=new byte[16];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(b);return Hex(b);}
  public static string Hex(byte[] b){return BitConverter.ToString(b).Replace("-","").ToLowerInvariant();}
  static byte[] Pbkdf(byte[] key,byte[] salt,int size){using(var h=new HMACSHA256(key)){var output=new byte[size];var data=new byte[salt.Length+4];Buffer.BlockCopy(salt,0,data,0,salt.Length);for(int b=1,o=0;o<size;b++,o+=32){data[salt.Length]=(byte)(b>>24);data[salt.Length+1]=(byte)(b>>16);data[salt.Length+2]=(byte)(b>>8);data[salt.Length+3]=(byte)b;var hash=h.ComputeHash(data);Buffer.BlockCopy(hash,0,output,o,Math.Min(32,size-o));}return output;}}
  static uint Rot(uint x,int n){return (x<<n)|(x>>(32-n));}
  static void Salsa(uint[] b,uint[] x){Array.Copy(b,x,16);unchecked{for(int i=0;i<8;i+=2){
   x[4]^=Rot(x[0]+x[12],7);x[8]^=Rot(x[4]+x[0],9);x[12]^=Rot(x[8]+x[4],13);x[0]^=Rot(x[12]+x[8],18);
   x[9]^=Rot(x[5]+x[1],7);x[13]^=Rot(x[9]+x[5],9);x[1]^=Rot(x[13]+x[9],13);x[5]^=Rot(x[1]+x[13],18);
   x[14]^=Rot(x[10]+x[6],7);x[2]^=Rot(x[14]+x[10],9);x[6]^=Rot(x[2]+x[14],13);x[10]^=Rot(x[6]+x[2],18);
   x[3]^=Rot(x[15]+x[11],7);x[7]^=Rot(x[3]+x[15],9);x[11]^=Rot(x[7]+x[3],13);x[15]^=Rot(x[11]+x[7],18);
   x[1]^=Rot(x[0]+x[3],7);x[2]^=Rot(x[1]+x[0],9);x[3]^=Rot(x[2]+x[1],13);x[0]^=Rot(x[3]+x[2],18);
   x[6]^=Rot(x[5]+x[4],7);x[7]^=Rot(x[6]+x[5],9);x[4]^=Rot(x[7]+x[6],13);x[5]^=Rot(x[4]+x[7],18);
   x[11]^=Rot(x[10]+x[9],7);x[8]^=Rot(x[11]+x[10],9);x[9]^=Rot(x[8]+x[11],13);x[10]^=Rot(x[9]+x[8],18);
   x[12]^=Rot(x[15]+x[14],7);x[13]^=Rot(x[12]+x[15],9);x[14]^=Rot(x[13]+x[12],13);x[15]^=Rot(x[14]+x[13],18);
  }for(int i=0;i<16;i++)b[i]+=x[i];}}
  static void Mix(uint[] input,uint[] output,uint[] block,uint[] scratch,int r){Array.Copy(input,(2*r-1)*16,block,0,16);for(int i=0;i<2*r;i++){for(int j=0;j<16;j++)block[j]^=input[i*16+j];Salsa(block,scratch);Array.Copy(block,0,output,(i%2==0?i/2:r+i/2)*16,16);}}
  public static string Hash(string password,string salt,int n=131072,int r=8,int p=1){
   byte[] key=Encoding.UTF8.GetBytes(password??"");byte[] bytes=Pbkdf(key,Encoding.UTF8.GetBytes(salt),128*r*p);int length=32*r;var v=new uint[n*length];var a=new uint[length];var b=new uint[length];var block=new uint[16];var scratch=new uint[16];
   try{for(int k=0;k<p;k++){for(int j=0;j<length;j++)a[j]=BitConverter.ToUInt32(bytes,k*length*4+j*4);for(int i=0;i<n;i++){Array.Copy(a,0,v,i*length,length);Mix(a,b,block,scratch,r);var swap=a;a=b;b=swap;}for(int i=0;i<n;i++){int index=(int)(a[(2*r-1)*16]&((uint)n-1));for(int j=0;j<length;j++)a[j]^=v[index*length+j];Mix(a,b,block,scratch,r);var swap=a;a=b;b=swap;}Buffer.BlockCopy(a,0,bytes,k*length*4,length*4);}return Hex(Pbkdf(key,bytes,64));}
   finally{Array.Clear(key,0,key.Length);Array.Clear(bytes,0,bytes.Length);Array.Clear(v,0,v.Length);Array.Clear(a,0,a.Length);Array.Clear(b,0,b.Length);}
  }
  public static bool Verify(string password,User user){if(password==null||password.Length>128)return false;string actual=Hash(password,user.salt),expected=user.password_hash??"";int diff=actual.Length^expected.Length;for(int i=0;i<actual.Length;i++)diff|=actual[i]^(i<expected.Length?expected[i]:0);return diff==0;}
  public static void Validate(string s){if(s==null||s.Length<8||s.Length>128)throw new Exception("A senha deve ter entre 8 e 128 caracteres.");}
 }
}
