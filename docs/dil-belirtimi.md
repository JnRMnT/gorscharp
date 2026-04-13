# <img src="../assets/logo.png" width="36" height="36" align="center" /> Gör# Dil Belirtimi

## Genel Bakış

Gör# (GörSharp), başlangıç seviyesindeki geliştiriciler için tasarlanmış, anlaşılır bir sözdizimiyle C# semantiği sunan eğitim amaçlı bir programlama dilidir.

## Sözcük Sırası

### ÖNY (Özne-Nesne-Yüklem) — Birincil
Türkçe dil yapısına uygun olarak yüklem sonda gelir:
```
"Merhaba" yazdır;          // Console.Write("Merhaba")
x yeniSatıraYazdır;        // Console.WriteLine(x)
```

### ÖYN (Özne-Yüklem-Nesne) — Fonksiyon Çağrıları
Eğitim amacıyla, fonksiyon çağrıları C#'a yakın formda da yazılabilir:
```
topla(3, 5);              // topla(3, 5);
```

## Veri Türleri

| Gör# | C# | Açıklama |
|---|---|---|
| `sayı` | `int` | 32-bit tam sayı |
| `metin` | `string` | Karakter dizisi |
| `mantık` | `bool` | Doğru/yanlış |
| `ondalık` | `double` | 64-bit kayan noktalı sayı |
| `karakter` | `char` | Tek karakter |

## Değişmez Değerler (Literals)

| Gör# | C# |
|---|---|
| `doğru` | `true` |
| `yanlış` | `false` |
| `boş` | `null` |
| `42` | `42` |
| `3.14` | `3.14` |
| `"metin"` | `"metin"` |

## Atama

```
// Tür çıkarımı ile bildirim
x 5 olsun;                      // var x = 5;

// Açık tür ile bildirim
x: sayı 5 olsun;                // int x = 5;

// Yeniden atama
x = 10;                         // x = 10;
```

## Giriş/Çıkış

```
"Merhaba" yazdır;              // Console.Write("Merhaba");
"Merhaba" yeniSatıraYazdır;    // Console.WriteLine("Merhaba");
```

## Operatörler

### Aritmetik
`+`, `-`, `*`, `/`, `%`

### Karşılaştırma
| Gör# | C# |
|---|---|
| `eşittir` | `==` |
| `eşitDeğildir` | `!=` |
| `büyüktür` | `>` |
| `küçüktür` | `<` |
| `büyükEşittir` | `>=` |
| `küçükEşittir` | `<=` |

### Mantıksal
| Gör# | C# |
|---|---|
| `ve` | `&&` |
| `veya` | `\|\|` |
| `ya da` | `\|\|` |
| `hem de` | `&&` |
| `ne ... ne de` | `!(a) && !(b)` |
| `değil` | `!` |

### Doğal Dil Ekleri

Bu sözcükler daha doğal Türkçe yazım için kabul edilir ve uygun bağlamlarda derleyici tarafından yok sayılır:

| Gör# | Davranış |
|---|---|
| `ise` | Koşul son eki, yok sayılır |
| `olursa` | Koşul son eki, yok sayılır |
| `iken` | Özellikle `döngü` koşullarında doğal bağlaç, yok sayılır |
| `mı/mi/mu/mü` | Soru parçacığı, yok sayılır |
| `şayet` | Koşul bağlacı, yok sayılır |
| `değişkeni` | Bildirim doğal eki, yok sayılır |
| `olarak` | Bildirim doğal eki, yok sayılır |
| `boyunca` | Döngü bağlacı, yok sayılır |
| `sürece` | Döngü bağlacı, yok sayılır |

Bildirim örnekleri:

```
x değişkeni 5 olsun;
x olarak 5 olsun;
x: sayı değişkeni 5 olsun;
```

### Çözümleme Profili

Gör# dil sunucusu ve CLI iki profille çalışabilir:

| Profil | Davranış |
|---|---|
| `natural` | Doğal dil ekleri açık (varsayılan) |
| `strict` | Doğal dil ekleri hata üretir (`GOR1001`) |

CLI:

```
gorsharp transpile dosya.gör --mode natural
gorsharp transpile dosya.gör --mode strict
```

### Mantıksal Sabit Eşanlamlıları

| Gör# | C# |
|---|---|
| `doğru` | `true` |
| `yanlış` | `false` |
| `evet` | `true` |
| `hayır` | `false` |

## Kontrol Yapıları

### Koşul (if/else)
```
eğer x büyüktür 5 {
    "Büyük" yazdır;
} yoksa eğer x eşittir 5 {
    "Eşit" yazdır;
} değilse {
    "Küçük" yazdır;
}
```

Doğal karşılaştırma biçimi de desteklenir:

```
eğer puan 90'dan büyük veya eşit ise {
    "AA" yeniSatıraYazdır;
}
```

Kısa doğal biçimler de geçerlidir:

```
eğer aktif mi {
    "Açık" yeniSatıraYazdır;
}

eğer doğru hem de hayır değil ise {
    "Örnek" yeniSatıraYazdır;
}
```

### Döngüler

#### While döngüsü
```
döngü x büyüktür 0 {
    x yeniSatıraYazdır;
    x = x - 1;
}
```

Doğal bağlaçlı biçim de geçerlidir:

```
döngü x büyüktür 0 iken {
    x yeniSatıraYazdır;
    x = x - 1;
}
```

#### For döngüsü
```
tekrarla (i: sayı 0 olsun; i küçüktür 10; i = i + 1) {
    i yeniSatıraYazdır;
}
```

#### Döngü kontrolü
```
kır;                             // break;
devam;                           // continue;
```

## Fonksiyonlar

```
fonksiyon topla(a: sayı, b: sayı): sayı {
    döndür a + b;
}

fonksiyon selamla() {
    "Merhaba" yeniSatıraYazdır;
}

sonuç: sayı topla(3, 5) olsun;
```

## Uçtan Uca Doğal Akış Örneği

Bu örnek, günlük Türkçe anlatıma yakın bir yazım akışını uçtan uca gösterir:

```gör
ad: metin "Deniz" olsun;
ilkSınav: sayı 45 olsun;
ikinciSınav: sayı 48 olsun;
proje: sayı 10 olsun;

fonksiyon puanHesapla(a: sayı, b: sayı, p: sayı): sayı {
    döndür a + b + p;
}

toplam: sayı puanHesapla(ilkSınav, ikinciSınav, proje) olsun;

eğer toplam 90'dan büyük veya eşit ise {
    ad yazdır;
    " dersi çok iyi geçti" yeniSatıraYazdır;
} yoksa eğer toplam 70'den büyük veya eşit ise {
    ad yazdır;
    " dersi geçti" yeniSatıraYazdır;
} değilse {
    ad yazdır;
    " ek çalışma yapmalı" yeniSatıraYazdır;
}

kalanDeneme: sayı 2 olsun;
döngü kalanDeneme büyüktür 0 iken {
    "Yeni deneme hakkı" yeniSatıraYazdır;
    kalanDeneme = kalanDeneme - 1;
}
```

Not: Doğal dil parçacıkları (`iken`, `veya`, `eşit`) okunabilirlik amacıyla desteklenir; üretim hedefi her zaman açık ve öğretici C# çıktısıdır.

## Uygulama Sınırı

Bu belge, şu anda depoda uygulanmış çekirdeği tanımlar. Sonek tabanlı metot çağrıları, sınıf sistemi, `foreach` ve gelişmiş hata yönetimi gibi başlıklar proje hedefleri içinde yer alsa da mevcut uygulamada ayrıca doğrulanmalıdır.

## IDE Öncelik Yol Haritası

Proje teslim stratejisi: önce Visual Studio tarafında tam öğrenme akışını sabitlemek, ardından VS Code deneyimini aynı seviyeye yükseltmek.

## Dosya Uzantıları

- Kaynak dosya: `.gör`
- Üretilen C#: `.cs`
