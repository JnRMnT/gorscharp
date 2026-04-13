# Katkıda Bulunma Rehberi

Gör# projesine katkıda bulunmak istediğiniz için teşekkürler!

## Başlarken

1. Depoyu fork edin
2. Özellik dalı oluşturun: `git checkout -b feature/yeni-ozellik`
3. Gerekli araçları kurun:
   - [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
   - Java yalnızca `grammar/GorSharp.g4` değiştiğinde ANTLR çıktısını yeniden üretmek için gerekir. Bu durumda [Oracle JDK 21+](https://www.oracle.com/java/technologies/downloads/) veya [OpenJDK 21+](https://openjdk.org/projects/jdk/21/) kullanabilirsiniz.

## Geliştirme Kuralları

### Kodlama Stili
- **Gör# sözdizimi**: Türkçe anahtar kelimeler ve tanımlayıcılar
- **C# iç yapı**: İngilizce sınıf, metot ve değişken adları (transpiler tarafında)
- **Hata mesajları**: Her zaman Türkçe
- **sozluk.json**: Anahtar kelime eşleştirmelerinin tek kaynağı — asla kodda sabit kodlamayın

### Türkçe Karakter Desteği
`ö`, `ü`, `ş`, `ç`, `ı`, `ğ`, `İ` karakterleri tanımlayıcılarda ve anahtar kelimelerde her yerde çalışmalıdır.

### Testler
- Her dilbilgisi kuralının transpile testi + derleme testi olmalıdır
- xUnit ile Theory/InlineData kullanın
- Testleri çalıştırın: `dotnet test GorSharp.slnx`
- Yeni dil özelliği Türkçe ek, hâl veya doğal dil biçimi içeriyorsa önce Zemberek gerekip gerekmediğini ve Zemberek paket/kaynak güncellemesi gerektirip gerektirmediğini değerlendirin

### Dilbilgisi Değişiklikleri
1. `grammar/GorSharp.g4` dosyasını düzenleyin
2. ANTLR parser'ı derleme sırasında otomatik olarak yeniden üretilir
3. `AstBuildingVisitor.cs` dosyasını yeni kural bağlamlarıyla güncelleyin
4. `CSharpEmitter.cs` dosyasına yeni Visit metotları ekleyin
5. Test ekleyin

### AST Düğümleri
- Kaynak konum bilgisi (satır, sütun) taşımalıdır
- Mirror özelliği için `/* gör:SATIR */` yorumları eklenir

## Pull Request Süreci

1. Tüm testlerin geçtiğinden emin olun: `dotnet test GorSharp.slnx`
2. Derlemenin geçtiğinden emin olun: `dotnet build GorSharp.slnx`
3. Değişikliklerinizi açık Türkçe commit mesajlarıyla commitleyin
4. PR açın ve değişiklikleri açıklayın

## Sorun Bildirme

GitHub Issues kullanarak hata raporu veya özellik isteği oluşturabilirsiniz.
