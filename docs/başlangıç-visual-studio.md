# Başlangıç Rehberi: Visual Studio (Gör#)

Bu rehber, Gör# için Visual Studio kullanım akışını anlatır.

## 1. VSIX Kurulumu

1. Uzantı projesini derleyin:

```powershell
dotnet build GorSharp.slnx
```

Gerekirse yalnızca VSIX projesini ayrıca derleyebilirsiniz:

```powershell
dotnet build src/GorSharp.VisualStudio/GorSharp.VisualStudio.csproj -c Debug
```

2. Çalışma yolunda ASCII dışı karakter varsa, VSIX dosyasını ASCII güvenli bir yola kopyalayın:

```powershell
New-Item -ItemType Directory -Path C:\Users\User\Downloads\GorSharpVsix -Force | Out-Null
Copy-Item src/GorSharp.VisualStudio/artifacts/GorSharp.VisualStudio.vsix C:\Users\User\Downloads\GorSharpVsix\GorSharp.VisualStudio.vsix -Force
```

3. `C:\Users\User\Downloads\GorSharpVsix\GorSharp.VisualStudio.vsix` dosyasına çift tıklayarak kurun.

## 2. Yeni GorSharp Projesi Oluşturma

Kurulumdan sonra:

1. Visual Studio'yu açın.
2. Yeni bir proje oluşturun.
3. `GorSharp` diye aratın.
4. `GorSharp Console Project` seçin.

Şablon şunları oluşturur:
- bir `.gör` kaynak dosyası
- çalıştırılabilir bir C# projesi
- otomatik üretilen eş dosya `dosyaadi.uretilenkod.cs`

## 3. Yeni GorSharp Çözümü Oluşturma

1. Visual Studio'yu açın.
2. Yeni bir proje oluşturun.
3. `GorSharp Solution` diye aratın.
4. Çözümü oluşturun.

Bu şablon, sıfırdan bir GorSharp projesi içeren çözüm oluşturur.

## 4. Derleme ve Çalıştırma Davranışı

Yapılandırılmış her `.gör` dosyası için:
- derleme sırasında GorSharp CLI transpile işlemi çalışır
- çıktı `dosyaadi.uretilenkod.cs` dosyasına yazılır
- C# derlemesi bu üretilen dosya üzerinden yapılır

Çözüm Gezgini'nde üretilen dosyalar `.gör` dosyalarının altında iç içe görünür.

## 5. Playground Varsayılanları

Depodaki `playground` projesi çalıştırılabilir proje olarak ayarlanmıştır ve `GorSharp.slnx` için varsayılan başlangıç projesi olarak düşünülmüştür.

Mevcut derleme giriş dosyası:
- `playground/01-hello.gör`

## 6. C# -> Gor# Sağ Tık Dönüşümü

Visual Studio, `.cs` dosyaları için Çözüm Gezgini sağ tık menüsü komutu sunar:
- komut: `Convert to Gor#`
- çıktı: aynı klasörde kardeş `.gör` dosyası
- motor: GorSharp CLI `fromcs`

Davranış ayrıntıları:
- öncelikle VSIX ile gelen gömülü CLI aracı kullanılır
- `.gör` dosyaları için LSP de VSIX ile gelen gömülü sunucudan çalıştırılır
- desteklenmeyen C# satırları üretilen `.gör` dosyasına tanısal yorum satırı olarak eklenir

## 7. Bağımsız Çalışma (Önemli)

Visual Studio uzantısı, repo paylaşılmadan çalışacak şekilde paketlenmiştir:
- LSP sunucusu VSIX içinde gelir (`server`)
- CLI dönüşüm aracı VSIX içinde gelir (`tools`)
- sözlük dosyası VSIX içinde gelir (`tools/dictionaries/sozluk.json`)

Bu nedenle son kullanıcıda ek `gorsharp` araç kurulumuna gerek olmamalıdır.
